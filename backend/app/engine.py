"""
Core Game Engine - The Island Survival Simulation.
Manages survival mechanics, agent states, weather, time, social interactions, and user commands.
"""

import asyncio
import logging
import random
import re
import time
from typing import TYPE_CHECKING, Optional

from .schemas import GameEvent, EventType
from .database import init_db, get_db_session
from .models import User, Agent, WorldState, GameConfig, AgentRelationship
from .llm import llm_service
from .memory_service import memory_service

if TYPE_CHECKING:
    from .server import ConnectionManager

logger = logging.getLogger(__name__)

# =============================================================================
# Command patterns
# =============================================================================
FEED_PATTERN = re.compile(r"feed\s+(\w+)", re.IGNORECASE)
CHECK_PATTERN = re.compile(r"(check|查询|状态)", re.IGNORECASE)
RESET_PATTERN = re.compile(r"(reset|重新开始|重置)", re.IGNORECASE)
HEAL_PATTERN = re.compile(r"heal\s+(\w+)", re.IGNORECASE)
TALK_PATTERN = re.compile(r"talk\s+(\w+)\s*(.*)?", re.IGNORECASE)
ENCOURAGE_PATTERN = re.compile(r"encourage\s+(\w+)", re.IGNORECASE)
REVIVE_PATTERN = re.compile(r"revive\s+(\w+)", re.IGNORECASE)

# =============================================================================
# Game constants
# =============================================================================
TICK_INTERVAL = 5.0  # Seconds between ticks

# Survival (base values, modified by difficulty)
BASE_ENERGY_DECAY_PER_TICK = 2
BASE_HP_DECAY_WHEN_STARVING = 5

# Command costs and effects
FEED_COST = 10
FEED_ENERGY_RESTORE = 20
HEAL_COST = 15
HEAL_HP_RESTORE = 30
ENCOURAGE_COST = 5
ENCOURAGE_MOOD_BOOST = 15
REVIVE_COST = 10  # Casual mode cost

INITIAL_USER_GOLD = 100
IDLE_CHAT_PROBABILITY = 0.15

# =============================================================================
# Day/Night cycle
# =============================================================================
TICKS_PER_DAY = 120  # 10 minutes per day at 5s/tick

DAY_PHASES = {
    "dawn": (0, 15),      # Ticks 0-15
    "day": (16, 75),      # Ticks 16-75
    "dusk": (76, 90),     # Ticks 76-90
    "night": (91, 119)    # Ticks 91-119
}

PHASE_MODIFIERS = {
    "dawn": {"energy_decay": 0.8, "hp_recovery": 1, "mood_change": 3},
    "day": {"energy_decay": 1.0, "hp_recovery": 2, "mood_change": 2},
    "dusk": {"energy_decay": 1.2, "hp_recovery": 0, "mood_change": -2},
    "night": {"energy_decay": 1.3, "hp_recovery": 0, "mood_change": -3}
}

# =============================================================================
# Weather system
# =============================================================================
WEATHER_TYPES = {
    "Sunny": {"energy_modifier": 1.0, "mood_change": 5},
    "Cloudy": {"energy_modifier": 1.0, "mood_change": 0},
    "Rainy": {"energy_modifier": 1.2, "mood_change": -8},
    "Stormy": {"energy_modifier": 1.4, "mood_change": -15},
    "Hot": {"energy_modifier": 1.3, "mood_change": -5},
    "Foggy": {"energy_modifier": 1.1, "mood_change": -3}
}

WEATHER_TRANSITIONS = {
    "Sunny": {"Sunny": 0.5, "Cloudy": 0.3, "Hot": 0.15, "Rainy": 0.05},
    "Cloudy": {"Cloudy": 0.3, "Sunny": 0.3, "Rainy": 0.25, "Foggy": 0.1, "Stormy": 0.05},
    "Rainy": {"Rainy": 0.3, "Cloudy": 0.35, "Stormy": 0.2, "Foggy": 0.15},
    "Stormy": {"Stormy": 0.2, "Rainy": 0.5, "Cloudy": 0.3},
    "Hot": {"Hot": 0.3, "Sunny": 0.5, "Cloudy": 0.15, "Stormy": 0.05},
    "Foggy": {"Foggy": 0.2, "Cloudy": 0.4, "Rainy": 0.25, "Sunny": 0.15}
}

WEATHER_MIN_DURATION = 15
WEATHER_MAX_DURATION = 35

# =============================================================================
# Social interactions
# =============================================================================
SOCIAL_INTERACTIONS = {
    "chat": {"affection": (1, 4), "trust": (0, 2), "weight": 0.4},
    "share_food": {"affection": (3, 7), "trust": (2, 4), "weight": 0.15, "min_energy": 40},
    "help": {"affection": (4, 8), "trust": (3, 6), "weight": 0.15, "min_energy": 30},
    "comfort": {"affection": (3, 6), "trust": (2, 4), "weight": 0.2, "target_max_mood": 40},
    "argue": {"affection": (-8, -3), "trust": (-5, -2), "weight": 0.1, "max_mood": 35}
}

# Initial NPC data
INITIAL_AGENTS = [
    {"name": "Jack", "personality": "Brave", "social_tendency": "extrovert"},
    {"name": "Luna", "personality": "Cunning", "social_tendency": "neutral"},
    {"name": "Bob", "personality": "Honest", "social_tendency": "introvert"},
]


class GameEngine:
    """
    The core game engine for island survival simulation.
    Manages agents, users, time, weather, social interactions, and survival mechanics.
    """

    def __init__(self, connection_manager: "ConnectionManager") -> None:
        self._manager = connection_manager
        self._running = False
        self._tick_count = 0
        self._tick_interval = TICK_INTERVAL
        self._config: Optional[GameConfig] = None

    @property
    def is_running(self) -> bool:
        return self._running

    def _get_config(self) -> GameConfig:
        """Get or load game configuration."""
        if self._config is None:
            with get_db_session() as db:
                config = db.query(GameConfig).first()
                if config is None:
                    config = GameConfig()
                # Expunge to detach from session, then make it usable outside
                db.expunge(config)
                # Access all attributes while still valid to load them
                _ = (config.difficulty, config.energy_decay_multiplier,
                     config.hp_decay_multiplier, config.auto_revive_enabled,
                     config.auto_revive_delay_ticks, config.revive_hp,
                     config.revive_energy, config.social_interaction_probability)
                self._config = config
        return self._config

    def _seed_initial_data(self) -> None:
        """Seed initial agents, world state, and config if database is empty."""
        with get_db_session() as db:
            # Seed agents
            if db.query(Agent).count() == 0:
                logger.info("Seeding initial agents...")
                for agent_data in INITIAL_AGENTS:
                    agent = Agent(
                        name=agent_data["name"],
                        personality=agent_data["personality"],
                        social_tendency=agent_data.get("social_tendency", "neutral"),
                        status="Alive",
                        hp=100,
                        energy=100,
                        mood=70
                    )
                    db.add(agent)
                logger.info(f"Created {len(INITIAL_AGENTS)} initial agents")

            # Seed world state
            if db.query(WorldState).first() is None:
                logger.info("Seeding initial world state...")
                world = WorldState(day_count=1, weather="Sunny", resource_level=100)
                db.add(world)

            # Seed game config (casual mode by default)
            if db.query(GameConfig).first() is None:
                logger.info("Seeding game config (casual mode)...")
                config = GameConfig(difficulty="casual")
                db.add(config)

    def _get_or_create_user(self, db, username: str) -> User:
        """Get existing user or create new one."""
        user = db.query(User).filter(User.username == username).first()
        if user is None:
            user = User(username=username, gold=INITIAL_USER_GOLD)
            db.add(user)
            db.flush()
            logger.info(f"New user registered: {username}")
        return user

    # =========================================================================
    # Event broadcasting
    # =========================================================================
    async def _broadcast_event(self, event_type: str, data: dict) -> None:
        """Create and broadcast a game event."""
        event = GameEvent(event_type=event_type, timestamp=time.time(), data=data)
        await self._manager.broadcast(event)

    async def _broadcast_agents_status(self) -> None:
        """Broadcast all agents' current status."""
        with get_db_session() as db:
            agents = db.query(Agent).all()
            agents_data = [agent.to_dict() for agent in agents]
        await self._broadcast_event(EventType.AGENTS_UPDATE, {"agents": agents_data})

    async def _broadcast_world_status(self) -> None:
        """Broadcast world state."""
        with get_db_session() as db:
            world = db.query(WorldState).first()
            if world:
                await self._broadcast_event(EventType.WORLD_UPDATE, world.to_dict())

    # =========================================================================
    # Day/Night cycle (Phase 2)
    # =========================================================================
    async def _process_weather(self) -> None:
        """Process weather transitions."""
        with get_db_session() as db:
            world = db.query(WorldState).first()
            if not world:
                return

            world.weather_duration += 1
            
            # Should we transition?
            if world.weather_duration >= random.randint(WEATHER_MIN_DURATION, WEATHER_MAX_DURATION):
                old_weather = world.weather
                transitions = WEATHER_TRANSITIONS.get(old_weather, {"Sunny": 1.0})
                
                # Biased random choice
                choices = list(transitions.keys())
                weights = list(transitions.values())
                new_weather = random.choices(choices, weights=weights, k=1)[0]
                
                if new_weather != old_weather:
                    world.weather = new_weather
                    world.weather_duration = 0
                    
                    await self._broadcast_event(EventType.WEATHER_CHANGE, {
                        "old_weather": old_weather,
                        "new_weather": new_weather,
                        "message": f"The weather is changing to {new_weather}."
                    })

    async def _advance_time(self) -> Optional[dict]:
        """Advance time and return phase change info if applicable."""
        with get_db_session() as db:
            world = db.query(WorldState).first()
            if not world:
                return None

            old_phase = world.time_of_day
            world.current_tick_in_day += 1

            # New day
            if world.current_tick_in_day >= TICKS_PER_DAY:
                world.current_tick_in_day = 0
                world.day_count += 1
                
                # Phase 17-A: Regenerate resources
                world.tree_left_fruit = min(5, world.tree_left_fruit + 2)
                world.tree_right_fruit = min(5, world.tree_right_fruit + 2)
                
                await self._broadcast_event(EventType.DAY_CHANGE, {
                    "day": world.day_count,
                    "message": f"Day {world.day_count} begins! Trees have new fruit."
                })

            # Determine current phase
            tick = world.current_tick_in_day
            for phase, (start, end) in DAY_PHASES.items():
                if start <= tick <= end:
                    if world.time_of_day != phase:
                        world.time_of_day = phase
                        return {"old_phase": old_phase, "new_phase": phase, "day": world.day_count}
                    break

        return None

    # =========================================================================
    # Weather system (Phase 3)
    # =========================================================================
    async def _update_weather(self) -> Optional[dict]:
        """Update weather based on transition probabilities."""
        with get_db_session() as db:
            world = db.query(WorldState).first()
            if not world:
                return None

            world.weather_duration += 1

            # Check if weather should change
            target_duration = random.randint(WEATHER_MIN_DURATION, WEATHER_MAX_DURATION)
            if world.weather_duration >= target_duration:
                old_weather = world.weather
                transitions = WEATHER_TRANSITIONS.get(old_weather, {"Sunny": 1.0})
                new_weather = random.choices(
                    list(transitions.keys()),
                    weights=list(transitions.values())
                )[0]

                if new_weather != old_weather:
                    world.weather = new_weather
                    world.weather_duration = 0
                    return {"old_weather": old_weather, "new_weather": new_weather}

        return None

    async def _update_moods(self) -> None:
        """Update agent moods based on weather and time."""
        with get_db_session() as db:
            world = db.query(WorldState).first()
            if not world:
                return

            weather_effect = WEATHER_TYPES.get(world.weather, {}).get("mood_change", 0)
            phase_effect = PHASE_MODIFIERS.get(world.time_of_day, {}).get("mood_change", 0)

            agents = db.query(Agent).filter(Agent.status == "Alive").all()
            for agent in agents:
                # Apply mood changes (scaled down for per-tick application)
                mood_delta = (weather_effect + phase_effect) * 0.1
                agent.mood = max(0, min(100, agent.mood + mood_delta))

                # Update mood state
                if agent.mood >= 70:
                    agent.mood_state = "happy"
                elif agent.mood >= 40:
                    agent.mood_state = "neutral"
                elif agent.mood >= 20:
                    agent.mood_state = "sad"
                else:
                    agent.mood_state = "anxious"

    # =========================================================================
    # Relationship 2.0 (Phase 17-B)
    # =========================================================================
    async def _assign_social_roles(self) -> None:
        """Assign social roles based on personality and social tendency."""
        with get_db_session() as db:
            agents = db.query(Agent).filter(Agent.status == "Alive").all()
            
            for agent in agents:
                if agent.social_role != "neutral":
                    continue  # Already assigned
                
                # Role assignment based on personality and tendency
                if agent.social_tendency == "extrovert" and agent.mood > 60:
                    agent.social_role = "leader"
                elif agent.social_tendency == "introvert" and agent.mood < 40:
                    agent.social_role = "loner"
                elif random.random() < 0.3:
                    agent.social_role = "follower"
                # Otherwise stays neutral

    async def _process_clique_behavior(self) -> None:
        """Leaders influence followers' actions."""
        # Run occasionally
        if self._tick_count % 10 != 0:
            return
            
        with get_db_session() as db:
            leaders = db.query(Agent).filter(
                Agent.status == "Alive",
                Agent.social_role == "leader"
            ).all()
            
            followers = db.query(Agent).filter(
                Agent.status == "Alive", 
                Agent.social_role == "follower"
            ).all()
            
            for leader in leaders:
                # Followers near leader copy their action
                for follower in followers:
                    if follower.current_action == "Idle" and leader.current_action not in ["Idle", None]:
                        follower.current_action = leader.current_action
                        follower.location = leader.location
                        await self._broadcast_event(EventType.COMMENT, {
                            "user": "System",
                            "message": f"{follower.name} follows {leader.name}'s lead!"
                        })

    # =========================================================================
    # Survival mechanics
    # =========================================================================
    async def _process_survival_tick(self) -> None:
        """Process survival mechanics with difficulty modifiers."""
        config = self._get_config()
        deaths = []

        with get_db_session() as db:
            world = db.query(WorldState).first()
            phase_mod = PHASE_MODIFIERS.get(world.time_of_day if world else "day", {})
            weather_mod = WEATHER_TYPES.get(world.weather if world else "Sunny", {})

            alive_agents = db.query(Agent).filter(Agent.status == "Alive").all()

            for agent in alive_agents:
                # --- Sickness Mechanics (Phase 15) ---
                # 1. Contracting Sickness
                if not agent.is_sick:
                    sickness_chance = 0.01 # Base 1% per tick (every 5s)
                    
                    # Weather impact
                    current_weather = world.weather if world else "Sunny"
                    if current_weather == "Rainy":
                        sickness_chance += 0.05
                    elif current_weather == "Stormy":
                        sickness_chance += 0.10
                    
                    # Immunity impact (Higher immunity = lower chance)
                    # Immunity 50 -> -2.5%, Immunity 100 -> -5%
                    sickness_chance -= (agent.immunity / 2000.0) 
                    
                    if random.random() < sickness_chance:
                        agent.is_sick = True
                        agent.mood -= 20
                        logger.info(f"Agent {agent.name} has fallen sick!")
                        # We could broadcast a specific event, but AGENTS_UPDATE will handle visual state
                        # Just log it or maybe a system message?
                        await self._broadcast_event(EventType.COMMENT, {
                            "user": "System", 
                            "message": f"{agent.name} is looking pale... (Sick)"
                        })

                # 2. Sickness Effects
                if agent.is_sick:
                    # Decay HP and Energy faster
                    agent.hp = max(0, agent.hp - 2)
                    agent.energy = max(0, agent.energy - 2)
                    
                    # Lower mood over time
                    if self._tick_count % 5 == 0:
                        agent.mood = max(0, agent.mood - 1)

                # --- End Sickness ---

                # Calculate energy decay with all modifiers
                base_decay = BASE_ENERGY_DECAY_PER_TICK
                decay = base_decay * config.energy_decay_multiplier
                decay *= phase_mod.get("energy_decay", 1.0)
                decay *= weather_mod.get("energy_modifier", 1.0)

                agent.energy = max(0, agent.energy - int(decay))

                # HP recovery during day phases (Only if NOT sick)
                hp_recovery = phase_mod.get("hp_recovery", 0)
                if hp_recovery > 0 and agent.energy > 20 and not agent.is_sick:
                    agent.hp = min(100, agent.hp + hp_recovery)

                # Starvation damage
                if agent.energy <= 0:
                    hp_decay = BASE_HP_DECAY_WHEN_STARVING * config.hp_decay_multiplier
                    agent.hp = max(0, agent.hp - int(hp_decay))

                # Death check
                if agent.hp <= 0:
                    agent.status = "Dead"
                    agent.death_tick = self._tick_count
                    if agent.is_sick:
                        # Clear sickness on death
                        agent.is_sick = False
                    deaths.append({"name": agent.name, "personality": agent.personality})
                    logger.info(f"Agent {agent.name} has died!")

        # Broadcast death events
        for death in deaths:
            await self._broadcast_event(EventType.AGENT_DIED, {
                "agent_name": death["name"],
                "message": f"{death['name']} ({death['personality']}) has died..."
            })
            
            # Phase 14: Alive agents remember the death
            with get_db_session() as db:
                witnesses = db.query(Agent).filter(Agent.status == "Alive").all()
                for witness in witnesses:
                    await memory_service.add_memory(
                        agent_id=witness.id,
                        description=f"{death['name']} died. It was a sad day.",
                        importance=8,
                        related_entity_name=death["name"],
                        memory_type="event"
                    )

    async def _process_auto_revive(self) -> None:
        """Auto-revive dead agents in casual mode."""
        config = self._get_config()
        if not config.auto_revive_enabled:
            return

        with get_db_session() as db:
            dead_agents = db.query(Agent).filter(Agent.status == "Dead").all()

            for agent in dead_agents:
                if agent.death_tick is None:
                    continue

                ticks_dead = self._tick_count - agent.death_tick
                if ticks_dead >= config.auto_revive_delay_ticks:
                    agent.status = "Alive"
                    agent.hp = config.revive_hp
                    agent.energy = config.revive_energy
                    agent.mood = 50
                    agent.mood_state = "neutral"
                    agent.death_tick = None

                    await self._broadcast_event(EventType.AUTO_REVIVE, {
                        "agent_name": agent.name,
                        "message": f"{agent.name} has been revived!"
                    })
                    logger.info(f"Agent {agent.name} auto-revived")

    # =========================================================================
    # Social system (Phase 5)
    # =========================================================================
    async def _process_social_tick(self) -> None:
        """Process autonomous social interactions between agents."""
        config = self._get_config()
        if random.random() > config.social_interaction_probability:
            return

        with get_db_session() as db:
            alive_agents = db.query(Agent).filter(Agent.status == "Alive").all()
            if len(alive_agents) < 2:
                return

            # Select initiator (extroverts more likely)
            weights = []
            for agent in alive_agents:
                if agent.social_tendency == "extrovert":
                    weights.append(2.0)
                elif agent.social_tendency == "introvert":
                    weights.append(0.5)
                else:
                    weights.append(1.0)

            initiator = random.choices(alive_agents, weights=weights)[0]

            # Select target (not self)
            targets = [a for a in alive_agents if a.id != initiator.id]
            if not targets:
                return
            target = random.choice(targets)

            # Get or create relationship
            relationship = db.query(AgentRelationship).filter(
                AgentRelationship.agent_from_id == initiator.id,
                AgentRelationship.agent_to_id == target.id
            ).first()

            if not relationship:
                relationship = AgentRelationship(
                    agent_from_id=initiator.id,
                    agent_to_id=target.id
                )
                db.add(relationship)
                db.flush()

            # Determine interaction type
            interaction_type = self._select_interaction(initiator, target, relationship)
            if not interaction_type:
                return

            # Apply interaction effects
            effects = SOCIAL_INTERACTIONS[interaction_type]
            affection_change = random.randint(*effects["affection"])
            trust_change = random.randint(*effects.get("trust", (0, 0)))

            relationship.affection = max(-100, min(100, relationship.affection + affection_change))
            relationship.trust = max(-100, min(100, relationship.trust + trust_change))
            relationship.interaction_count += 1
            relationship.last_interaction_tick = self._tick_count
            relationship.update_relationship_type()

            # Store data for broadcasting
            interaction_data = {
                "initiator_id": initiator.id,
                "initiator_name": initiator.name,
                "target_id": target.id,
                "target_name": target.name,
                "interaction_type": interaction_type,
                "relationship_type": relationship.relationship_type
            }
            world = db.query(WorldState).first()
            weather = world.weather if world else "Sunny"
            time_of_day = world.time_of_day if world else "day"

        # Generate LLM dialogue for interaction
        asyncio.create_task(self._trigger_social_dialogue(
            interaction_data, weather, time_of_day
        ))

    def _select_interaction(self, initiator: Agent, target: Agent, relationship: AgentRelationship) -> Optional[str]:
        """Select appropriate interaction type based on conditions."""
        valid_interactions = []
        weights = []

        for itype, config in SOCIAL_INTERACTIONS.items():
            # Check conditions
            if "min_energy" in config and initiator.energy < config["min_energy"]:
                continue
            if "max_mood" in config and initiator.mood > config["max_mood"]:
                continue
            if "target_max_mood" in config and target.mood > config["target_max_mood"]:
                continue

            valid_interactions.append(itype)
            weights.append(config["weight"])

        if not valid_interactions:
            return None

        return random.choices(valid_interactions, weights=weights)[0]

    async def _trigger_social_dialogue(self, interaction_data: dict, weather: str, time_of_day: str) -> None:
        """Generate and broadcast social interaction dialogue."""
        try:
            dialogue = await llm_service.generate_social_interaction(
                initiator_name=interaction_data["initiator_name"],
                target_name=interaction_data["target_name"],
                interaction_type=interaction_data["interaction_type"],
                relationship_type=interaction_data["relationship_type"],
                weather=weather,
                time_of_day=time_of_day
            )

            await self._broadcast_event(EventType.SOCIAL_INTERACTION, {
                **interaction_data,
                "dialogue": dialogue
            })

        except Exception as e:
            logger.error(f"Error in social dialogue: {e}")

    # =========================================================================
    # Autonomous Agency (Phase 13)
    # =========================================================================
    async def _process_activity_tick(self) -> None:
        """Decide and execute autonomous agent actions."""
        # Only process activity every few ticks to avoid chaotic movement
        if self._tick_count % 3 != 0:
            return

        with get_db_session() as db:
            world = db.query(WorldState).first()
            if not world:
                return

            agents = db.query(Agent).filter(Agent.status == "Alive").all()
            
            for agent in agents:
                new_action = agent.current_action
                new_location = agent.location
                target_name = None
                should_update = False

                # 1. Critical Needs (Override everything)
                if world.time_of_day == "night":
                    if agent.current_action != "Sleep":
                        new_action = "Sleep"
                        new_location = "campfire"
                        should_update = True
                elif agent.energy < 30:
                    if agent.current_action != "Gather":
                        new_action = "Gather"
                        new_location = random.choice(["tree_left", "tree_right"])
                        should_update = True
                
                # 1.5. Sickness Handling (Phase 16)
                elif agent.is_sick:
                    inv = self._get_inventory(agent)
                    if inv.get("medicine", 0) > 0:
                        # Use medicine immediately
                        await self._use_medicine(agent)
                        new_action = "Use Medicine"
                        new_location = agent.location
                        should_update = True
                    elif inv.get("herb", 0) >= 3:
                        # Craft medicine
                        await self._craft_medicine(agent)
                        new_action = "Craft Medicine"
                        new_location = agent.location
                        should_update = True
                    elif agent.current_action != "Gather Herb":
                        # Go gather herbs
                        new_action = "Gather Herb"
                        new_location = "herb_patch"
                        should_update = True
                
                # 2. Mood / Social Needs
                elif agent.mood < 40 and agent.current_action not in ["Sleep", "Gather", "Socialize", "Gather Herb"]:
                    new_action = "Socialize"
                    potential_friends = [a for a in agents if a.id != agent.id]
                    if potential_friends:
                        friend = random.choice(potential_friends)
                        new_location = "agent"
                        target_name = friend.name
                        should_update = True
                
                # 3. Boredom / Wandering
                elif agent.current_action == "Idle" or agent.current_action is None:
                    if random.random() < 0.3:
                        new_action = "Wander"
                        new_location = "nearby"
                        should_update = True
                
                # 4. Finish Tasks (Simulation)
                elif agent.current_action == "Gather" and agent.energy >= 90:
                    new_action = "Idle"
                    new_location = "center"
                    should_update = True
                elif agent.current_action == "Gather" and agent.location in ["tree_left", "tree_right"]:
                    # Phase 17-A: Consume fruit when gathering
                    fruit_available = await self._consume_fruit(world, agent.location)
                    if fruit_available:
                        agent.energy = min(100, agent.energy + 30)
                        new_action = "Idle"
                        new_location = "center"
                        should_update = True
                    else:
                        # No fruit! Try other tree or express frustration
                        other_tree = "tree_right" if agent.location == "tree_left" else "tree_left"
                        other_fruit = world.tree_right_fruit if agent.location == "tree_left" else world.tree_left_fruit
                        if other_fruit > 0:
                            new_action = "Gather"
                            new_location = other_tree
                            should_update = True
                        else:
                            # All trees empty!
                            new_action = "Hungry"
                            new_location = "center"
                            should_update = True
                            await self._broadcast_event(EventType.COMMENT, {
                                "user": "System",
                                "message": f"{agent.name} can't find any fruit! The trees are empty..."
                            })
                elif agent.current_action == "Sleep" and world.time_of_day != "night":
                    new_action = "Wake Up"
                    new_location = "center"
                    should_update = True
                elif agent.current_action == "Gather Herb":
                    # Simulate herb gathering (add herbs)
                    await self._gather_herb(agent)
                    new_action = "Idle"
                    new_location = "center"
                    should_update = True

                # Execute Update
                if should_update:
                    agent.current_action = new_action
                    agent.location = new_location
                    
                    # Generate simple thought/bark
                    dialogue = self._get_action_bark(agent, new_action, target_name)

                    await self._broadcast_event(EventType.AGENT_ACTION, {
                        "agent_id": agent.id,
                        "agent_name": agent.name,
                        "action_type": new_action,
                        "location": new_location,
                        "target_name": target_name,
                        "dialogue": dialogue
                    })

    def _get_action_bark(self, agent: Agent, action: str, target: str = None) -> str:
        """Get a simple bark text for an action."""
        if action == "Sleep":
            return random.choice(["Yawn... sleepy...", "Time to rest.", "Zzz..."])
        elif action == "Gather":
            return random.choice(["Hungry!", "Need food.", "Looking for coconuts..."])
        elif action == "Gather Herb":
            return random.choice(["I need herbs...", "Looking for medicine plants.", "Feeling sick..."])
        elif action == "Craft Medicine":
            return random.choice(["Let me make some medicine.", "Mixing herbs...", "Almost done!"])
        elif action == "Use Medicine":
            return random.choice(["Ahh, much better!", "Medicine tastes awful but works!", "Feeling cured!"])
        elif action == "Socialize":
            return f"Looking for {target}..." if target else "Need a friend."
        elif action == "Wander":
            return random.choice(["Hmm...", "Nice weather.", "Taking a walk."])
        elif action == "Wake Up":
            return "Good morning!"
        return ""

    # =========================================================================
    # Inventory & Crafting (Phase 16)
    # =========================================================================
    def _get_inventory(self, agent: Agent) -> dict:
        """Parse agent inventory JSON."""
        import json
        try:
            return json.loads(agent.inventory) if agent.inventory else {}
        except json.JSONDecodeError:
            return {}

    def _set_inventory(self, agent: Agent, inv: dict) -> None:
        """Set agent inventory from dict."""
        import json
        agent.inventory = json.dumps(inv)

    async def _consume_fruit(self, world: WorldState, location: str) -> bool:
        """Consume fruit from a tree. Returns True if successful."""
        if location == "tree_left":
            if world.tree_left_fruit > 0:
                world.tree_left_fruit -= 1
                logger.info(f"Fruit consumed from tree_left. Remaining: {world.tree_left_fruit}")
                return True
        elif location == "tree_right":
            if world.tree_right_fruit > 0:
                world.tree_right_fruit -= 1
                logger.info(f"Fruit consumed from tree_right. Remaining: {world.tree_right_fruit}")
                return True
        return False

    async def _gather_herb(self, agent: Agent) -> None:
        """Agent gathers herbs."""
        inv = self._get_inventory(agent)
        herbs_found = random.randint(1, 2)
        inv["herb"] = inv.get("herb", 0) + herbs_found
        self._set_inventory(agent, inv)
        
        await self._broadcast_event(EventType.AGENT_ACTION, {
            "agent_id": agent.id,
            "agent_name": agent.name,
            "action_type": "Gather Herb",
            "location": "herb_patch",
            "dialogue": f"Found {herbs_found} herbs!"
        })
        logger.info(f"Agent {agent.name} gathered {herbs_found} herbs. Total: {inv['herb']}")

    async def _craft_medicine(self, agent: Agent) -> None:
        """Agent crafts medicine from herbs."""
        inv = self._get_inventory(agent)
        if inv.get("herb", 0) >= 3:
            inv["herb"] -= 3
            inv["medicine"] = inv.get("medicine", 0) + 1
            self._set_inventory(agent, inv)
            
            await self._broadcast_event(EventType.CRAFT, {
                "agent_id": agent.id,
                "agent_name": agent.name,
                "item": "medicine",
                "ingredients": {"herb": 3}
            })
            logger.info(f"Agent {agent.name} crafted medicine. Inventory: {inv}")

    async def _use_medicine(self, agent: Agent) -> None:
        """Agent uses medicine to cure sickness."""
        inv = self._get_inventory(agent)
        if inv.get("medicine", 0) > 0:
            inv["medicine"] -= 1
            self._set_inventory(agent, inv)
            
            agent.is_sick = False
            agent.hp = min(100, agent.hp + 20)
            agent.mood = min(100, agent.mood + 10)
            
            await self._broadcast_event(EventType.USE_ITEM, {
                "agent_id": agent.id,
                "agent_name": agent.name,
                "item": "medicine",
                "effect": "cured sickness"
            })
            logger.info(f"Agent {agent.name} used medicine and is cured!")

    # =========================================================================
    # LLM-powered agent speech
    # =========================================================================
    async def _trigger_agent_speak(
        self, agent_id: int, agent_name: str, agent_personality: str,
        agent_hp: int, agent_energy: int, agent_mood: int,
        event_description: str, event_type: str = "feed"
    ) -> None:
        """Fire-and-forget LLM call to generate agent speech."""
        try:
            class AgentSnapshot:
                def __init__(self, name, personality, hp, energy, mood):
                    self.name = name
                    self.personality = personality
                    self.hp = hp
                    self.energy = energy
                    self.mood = mood

            agent_snapshot = AgentSnapshot(
                agent_name, agent_personality, agent_hp, agent_energy, agent_mood
            )

            text = await llm_service.generate_reaction(agent_snapshot, event_description, event_type)

            await self._broadcast_event(EventType.AGENT_SPEAK, {
                "agent_id": agent_id,
                "agent_name": agent_name,
                "text": text
            })

        except Exception as e:
            logger.error(f"Error in agent speak: {e}")

    async def _trigger_idle_chat(self) -> None:
        """Randomly select an alive agent to say something."""
        with get_db_session() as db:
            alive_agents = db.query(Agent).filter(Agent.status == "Alive").all()
            world = db.query(WorldState).first()
            weather = world.weather if world else "Sunny"
            time_of_day = world.time_of_day if world else "day"

            if not alive_agents:
                return

            agent = random.choice(alive_agents)
            agent_data = {
                "id": agent.id, "name": agent.name, "personality": agent.personality,
                "hp": agent.hp, "energy": agent.energy, "mood": agent.mood,
                "mood_state": agent.mood_state
            }

        try:
            class AgentSnapshot:
                def __init__(self, name, personality, hp, energy, mood):
                    self.name = name
                    self.personality = personality
                    self.hp = hp
                    self.energy = energy
                    self.mood = mood

            agent_snapshot = AgentSnapshot(
                agent_data["name"], agent_data["personality"],
                agent_data["hp"], agent_data["energy"], agent_data["mood"]
            )

            text = await llm_service.generate_idle_chat(agent_snapshot, weather, time_of_day)

            await self._broadcast_event(EventType.AGENT_SPEAK, {
                "agent_id": agent_data["id"],
                "agent_name": agent_data["name"],
                "text": text
            })

        except Exception as e:
            logger.error(f"Error in idle chat: {e}")

    # =========================================================================
    # Command handlers
    # =========================================================================
    async def _handle_feed(self, username: str, agent_name: str) -> None:
        """Handle feed command."""
        feed_result = None

        with get_db_session() as db:
            user = self._get_or_create_user(db, username)
            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()

            if agent is None:
                await self._broadcast_event(EventType.ERROR, {"message": f"Agent '{agent_name}' not found"})
                return

            if agent.status != "Alive":
                await self._broadcast_event(EventType.ERROR, {"message": f"{agent.name} is dead"})
                return

            if user.gold < FEED_COST:
                await self._broadcast_event(EventType.ERROR, {
                    "user": username, "message": f"Not enough gold! Need {FEED_COST}, have {user.gold}"
                })
                return

            user.gold -= FEED_COST
            old_energy = agent.energy
            agent.energy = min(100, agent.energy + FEED_ENERGY_RESTORE)
            agent.mood = min(100, agent.mood + 5)

            feed_result = {
                "agent_id": agent.id, "agent_name": agent.name,
                "agent_personality": agent.personality, "agent_hp": agent.hp,
                "agent_energy": agent.energy, "agent_mood": agent.mood,
                "actual_restore": agent.energy - old_energy, "user_gold": user.gold
            }

        if feed_result:
            await self._broadcast_event(EventType.FEED, {
                "user": username, "agent_name": feed_result["agent_name"],
                "energy_restored": feed_result["actual_restore"],
                "agent_energy": feed_result["agent_energy"], "user_gold": feed_result["user_gold"],
                "message": f"{username} fed {feed_result['agent_name']}!"
            })
            await self._broadcast_event(EventType.USER_UPDATE, {"user": username, "gold": feed_result["user_gold"]})

            asyncio.create_task(self._trigger_agent_speak(
                feed_result["agent_id"], feed_result["agent_name"],
                feed_result["agent_personality"], feed_result["agent_hp"],
                feed_result["agent_energy"], feed_result["agent_mood"],
                f"User {username} gave you food!", "feed"
            ))

    async def _handle_heal(self, username: str, agent_name: str) -> None:
        """Handle heal command."""
        with get_db_session() as db:
            user = self._get_or_create_user(db, username)
            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()

            if agent is None:
                await self._broadcast_event(EventType.ERROR, {"message": f"Agent '{agent_name}' not found"})
                return

            if agent.status != "Alive":
                await self._broadcast_event(EventType.ERROR, {"message": f"{agent.name} is dead"})
                return

            if user.gold < HEAL_COST:
                await self._broadcast_event(EventType.ERROR, {
                    "user": username, "message": f"Not enough gold! Need {HEAL_COST}, have {user.gold}"
                })
                return

            user.gold -= HEAL_COST
            old_hp = agent.hp
            was_sick = agent.is_sick
            
            agent.hp = min(100, agent.hp + HEAL_HP_RESTORE)
            agent.is_sick = False # Cure sickness

            msg = f"{username} healed {agent.name}!"
            if was_sick:
                msg = f"{username} cured {agent.name}'s sickness!"

            await self._broadcast_event(EventType.HEAL, {
                "user": username, "agent_name": agent.name,
                "hp_restored": agent.hp - old_hp, "agent_hp": agent.hp,
                "user_gold": user.gold, "message": msg
            })
            await self._broadcast_event(EventType.USER_UPDATE, {"user": username, "gold": user.gold})

            asyncio.create_task(self._trigger_agent_speak(
                agent.id, agent.name, agent.personality, agent.hp, agent.energy, agent.mood,
                f"User {username} healed you!", "heal"
            ))

    async def _handle_encourage(self, username: str, agent_name: str) -> None:
        """Handle encourage command."""
        with get_db_session() as db:
            user = self._get_or_create_user(db, username)
            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()

            if agent is None:
                await self._broadcast_event(EventType.ERROR, {"message": f"Agent '{agent_name}' not found"})
                return

            if agent.status != "Alive":
                await self._broadcast_event(EventType.ERROR, {"message": f"{agent.name} is dead"})
                return

            if user.gold < ENCOURAGE_COST:
                await self._broadcast_event(EventType.ERROR, {
                    "user": username, "message": f"Not enough gold! Need {ENCOURAGE_COST}, have {user.gold}"
                })
                return

            user.gold -= ENCOURAGE_COST
            old_mood = agent.mood
            agent.mood = min(100, agent.mood + ENCOURAGE_MOOD_BOOST)

            await self._broadcast_event(EventType.ENCOURAGE, {
                "user": username, "agent_name": agent.name,
                "mood_boost": agent.mood - old_mood, "agent_mood": agent.mood,
                "user_gold": user.gold, "message": f"{username} encouraged {agent.name}!"
            })
            await self._broadcast_event(EventType.USER_UPDATE, {"user": username, "gold": user.gold})

            asyncio.create_task(self._trigger_agent_speak(
                agent.id, agent.name, agent.personality, agent.hp, agent.energy, agent.mood,
                f"User {username} encouraged you!", "encourage"
            ))

    async def _handle_talk(self, username: str, agent_name: str, topic: str = "") -> None:
        """Handle talk command - free conversation with agent."""
        with get_db_session() as db:
            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()

            if agent is None:
                await self._broadcast_event(EventType.ERROR, {"message": f"Agent '{agent_name}' not found"})
                return

            if agent.status != "Alive":
                await self._broadcast_event(EventType.ERROR, {"message": f"{agent.name} is dead"})
                return

            agent_data = {
                "id": agent.id, "name": agent.name, "personality": agent.personality,
                "hp": agent.hp, "energy": agent.energy, "mood": agent.mood
            }

        # Generate conversation response
        try:
            response = await llm_service.generate_conversation_response(
                agent_name=agent_data["name"],
                agent_personality=agent_data["personality"],
                agent_mood=agent_data["mood"],
                username=username,
                topic=topic or "just chatting"
            )

            await self._broadcast_event(EventType.TALK, {
                "user": username, "agent_name": agent_data["name"],
                "topic": topic, "response": response
            })

        except Exception as e:
            logger.error(f"Error in talk: {e}")

    async def _handle_revive(self, username: str, agent_name: str) -> None:
        """Handle revive command (casual mode)."""
        config = self._get_config()

        with get_db_session() as db:
            user = self._get_or_create_user(db, username)
            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()

            if agent is None:
                await self._broadcast_event(EventType.ERROR, {"message": f"Agent '{agent_name}' not found"})
                return

            if agent.status == "Alive":
                await self._broadcast_event(EventType.ERROR, {"message": f"{agent.name} is already alive!"})
                return

            if user.gold < REVIVE_COST:
                await self._broadcast_event(EventType.ERROR, {
                    "user": username, "message": f"Not enough gold! Need {REVIVE_COST}, have {user.gold}"
                })
                return

            user.gold -= REVIVE_COST
            agent.status = "Alive"
            agent.hp = config.revive_hp
            agent.energy = config.revive_energy
            agent.mood = 50
            agent.mood_state = "neutral"
            agent.death_tick = None

            await self._broadcast_event(EventType.REVIVE, {
                "user": username, "agent_name": agent.name,
                "user_gold": user.gold, "message": f"{username} revived {agent.name}!"
            })
            await self._broadcast_event(EventType.USER_UPDATE, {"user": username, "gold": user.gold})

    async def _handle_check(self, username: str) -> None:
        """Handle check/status command."""
        with get_db_session() as db:
            user = self._get_or_create_user(db, username)
            agents = db.query(Agent).all()
            world = db.query(WorldState).first()
            config = db.query(GameConfig).first()

            user_data = {"username": user.username, "gold": user.gold}
            agents_data = [agent.to_dict() for agent in agents]
            world_data = world.to_dict() if world else {}
            config_data = config.to_dict() if config else {}

        await self._broadcast_event(EventType.CHECK, {
            "user": user_data, "agents": agents_data,
            "world": world_data, "config": config_data,
            "message": f"{username}'s status - Gold: {user_data['gold']}"
        })

    async def _handle_reset(self, username: str) -> None:
        """Handle reset command - reset all agents."""
        with get_db_session() as db:
            agents = db.query(Agent).all()
            for agent in agents:
                agent.hp = 100
                agent.energy = 100
                agent.mood = 70
                agent.mood_state = "neutral"
                agent.status = "Alive"
                agent.death_tick = None

            world = db.query(WorldState).first()
            if world:
                world.day_count = 1
                world.current_tick_in_day = 0
                world.time_of_day = "day"
                world.weather = "Sunny"
                world.weather_duration = 0

        await self._broadcast_event(EventType.SYSTEM, {
            "message": f"{username} triggered a restart! All survivors have been revived."
        })
        await self._broadcast_agents_status()

    async def process_comment(self, user: str, message: str) -> None:
        """Process a comment through command matching."""
        await self._broadcast_event(EventType.COMMENT, {"user": user, "message": message})

        # Match commands in priority order
        if match := FEED_PATTERN.search(message):
            await self._handle_feed(user, match.group(1))
            return

        if match := HEAL_PATTERN.search(message):
            await self._handle_heal(user, match.group(1))
            return

        if match := TALK_PATTERN.search(message):
            topic = match.group(2) or ""
            await self._handle_talk(user, match.group(1), topic.strip())
            return

        if match := ENCOURAGE_PATTERN.search(message):
            await self._handle_encourage(user, match.group(1))
            return

        if match := REVIVE_PATTERN.search(message):
            await self._handle_revive(user, match.group(1))
            return

        if CHECK_PATTERN.search(message):
            await self._handle_check(user)
            return

        if RESET_PATTERN.search(message):
            await self._handle_reset(user)
            return

    # =========================================================================
    # Random Events (Phase 17-C)
    # =========================================================================
    RANDOM_EVENTS = {
        "storm_damage": {"weight": 30, "description": "A sudden storm damages the island!"},
        "treasure_found": {"weight": 25, "description": "Someone found a buried treasure!"},
        "beast_attack": {"weight": 20, "description": "A wild beast attacks the camp!"},
        "rumor_spread": {"weight": 25, "description": "A rumor starts spreading..."},
    }

    async def _process_random_events(self) -> None:
        """Process random events (10% chance per day at dawn)."""
        # Only trigger at dawn (once per day)
        if self._tick_count % 100 != 1:  # Roughly once every ~100 ticks
            return
        
        if random.random() > 0.10:  # 10% chance
            return

        # Pick random event
        events = list(self.RANDOM_EVENTS.keys())
        weights = [self.RANDOM_EVENTS[e]["weight"] for e in events]
        event_type = random.choices(events, weights=weights)[0]

        with get_db_session() as db:
            world = db.query(WorldState).first()
            agents = db.query(Agent).filter(Agent.status == "Alive").all()
            
            if not agents:
                return

            event_data = {"event_type": event_type, "message": ""}

            if event_type == "storm_damage":
                # All agents lose HP and resources depleted
                for agent in agents:
                    agent.hp = max(0, agent.hp - 15)
                if world:
                    world.tree_left_fruit = max(0, world.tree_left_fruit - 2)
                    world.tree_right_fruit = max(0, world.tree_right_fruit - 2)
                event_data["message"] = "A violent storm hits! Everyone is injured and fruit trees are damaged."

            elif event_type == "treasure_found":
                # Random agent finds treasure (bonus herbs/medicine)
                lucky = random.choice(agents)
                inv = self._get_inventory(lucky)
                inv["medicine"] = inv.get("medicine", 0) + 2
                inv["herb"] = inv.get("herb", 0) + 3
                self._set_inventory(lucky, inv)
                event_data["message"] = f"{lucky.name} found a buried treasure with medicine and herbs!"
                event_data["agent_name"] = lucky.name

            elif event_type == "beast_attack":
                # Random agent gets attacked
                victim = random.choice(agents)
                victim.hp = max(0, victim.hp - 25)
                victim.mood = max(0, victim.mood - 20)
                event_data["message"] = f"A wild beast attacked {victim.name}!"
                event_data["agent_name"] = victim.name

            elif event_type == "rumor_spread":
                # Random relationship impact
                if len(agents) >= 2:
                    a1, a2 = random.sample(list(agents), 2)
                    a1.mood = max(0, a1.mood - 10)
                    a2.mood = max(0, a2.mood - 10)
                    event_data["message"] = f"A rumor about {a1.name} and {a2.name} is spreading..."

            await self._broadcast_event(EventType.RANDOM_EVENT, event_data)
            logger.info(f"Random event triggered: {event_type}")

    # =========================================================================
    # Game loop
    # =========================================================================
    async def _game_loop(self) -> None:
        """The main game loop with all systems."""
        logger.info("Game loop started - The Island awaits...")
        await self._broadcast_agents_status()
        await self._broadcast_world_status()

        while self._running:
            self._tick_count += 1

            # 1. Advance time (Phase 2)
            phase_change = await self._advance_time()
            if phase_change:
                await self._broadcast_event(EventType.PHASE_CHANGE, {
                    "old_phase": phase_change["old_phase"],
                    "new_phase": phase_change["new_phase"],
                    "day": phase_change["day"],
                    "message": f"The {phase_change['new_phase']} begins..."
                })

            # 2. Update weather (Phase 3)
            weather_change = await self._update_weather()
            if weather_change:
                await self._broadcast_event(EventType.WEATHER_CHANGE, {
                    "old_weather": weather_change["old_weather"],
                    "new_weather": weather_change["new_weather"],
                    "message": f"Weather changed to {weather_change['new_weather']}"
                })

            # 3. Process survival (with difficulty modifiers)
            await self._process_survival_tick()

            # 4. Auto-revive check (casual mode)
            await self._process_auto_revive()

            # 5. Update moods (Phase 3)
            await self._update_moods()

            # 6. Autonomous Activity (Phase 13)
            await self._process_activity_tick()

            # 7. Social interactions (Phase 5)
            await self._process_social_tick()

            # 8. Random Events (Phase 17-C)
            await self._process_random_events()

            # 9. Clique Behavior (Phase 17-B)
            await self._assign_social_roles()
            await self._process_clique_behavior()

            # 7. Idle chat
            with get_db_session() as db:
                alive_count = db.query(Agent).filter(Agent.status == "Alive").count()

            if alive_count > 0 and random.random() < IDLE_CHAT_PROBABILITY:
                asyncio.create_task(self._trigger_idle_chat())

            # 8. Broadcast states
            await self._broadcast_agents_status()

            # Tick event
            with get_db_session() as db:
                world = db.query(WorldState).first()
                day = world.day_count if world else 1
                time_of_day = world.time_of_day if world else "day"
                weather = world.weather if world else "Sunny"

            await self._broadcast_event(EventType.TICK, {
                "tick": self._tick_count,
                "day": day,
                "time_of_day": time_of_day,
                "weather": weather,
                "alive_agents": alive_count
            })

            await asyncio.sleep(self._tick_interval)

        logger.info("Game loop stopped")

    async def start(self) -> None:
        """Start the game engine."""
        if self._running:
            logger.warning("Engine already running")
            return

        logger.info("Initializing database...")
        init_db()
        self._seed_initial_data()

        # Reload config
        self._config = None
        self._get_config()

        self._running = True
        asyncio.create_task(self._game_loop())
        logger.info("Game engine started - The Island awaits...")

    async def stop(self) -> None:
        """Stop the game engine."""
        self._running = False
        logger.info("Game engine stopping...")

    async def process_command(self, user: str, text: str) -> None:
        """Process a command from Twitch chat."""
        # Use the existing process_comment method to handle commands
        await self.process_comment(user, text)

    async def handle_gift(self, user: str, amount: int, gift_type: str = "bits") -> None:
        """
        Handle a gift/donation (bits, subscription, or test).
        
        Args:
            user: Name of the donor
            amount: Value of the gift
            gift_type: Type of gift (bits, test, etc.)
        """
        # 1. Add gold to user
        gold_added = amount
        with get_db_session() as db:
            user_obj = self._get_or_create_user(db, user)
            user_obj.gold += gold_added
            
            await self._broadcast_event(EventType.USER_UPDATE, {
                "user": user,
                "gold": user_obj.gold,
                "message": f"{user} received {gold_added} gold!"
            })

            # Check for alive agents for reaction
            alive_agents = db.query(Agent).filter(Agent.status == "Alive").all()
            agent = random.choice(alive_agents) if alive_agents else None
            # Extract data immediately to avoid DetachedInstanceError after session closes
            agent_name = agent.name if agent else "Survivor"
            agent_personality = agent.personality if agent else "friendly"

        # 2. Generate AI gratitude
        gratitude = await llm_service.generate_gratitude(
            user=user,
            amount=amount,
            agent_name=agent_name,
            agent_personality=agent_personality,
            gift_name=gift_type
        )

        # 3. Store Memory (Phase 14)
        if agent:
            memory_text = f"User {user} gave me {amount} {gift_type}. I felt grateful."
            await memory_service.add_memory(
                agent_id=agent.id,
                description=memory_text,
                importance=random.randint(6, 9), # Gifts are important
                related_entity_name=user,
                memory_type="gift"
            )
        
        # 4. Broadcast gift effect to Unity
        await self._broadcast_event("gift_effect", {
            "user": user,
            "gift_type": gift_type,
            "value": amount,
            "message": f"{user} sent {amount} {gift_type}!",
            "agent_name": agent_name if agent else None,
            "gratitude": gratitude,
            "duration": 8.0
        })
        
        logger.info(f"Processed gift: {user} -> {amount} {gift_type} (Gratitude: {gratitude})")

    async def process_bits(self, user: str, amount: int) -> None:
        """Deprecated: Use handle_gift instead."""
        await self.handle_gift(user, amount, "bits")
