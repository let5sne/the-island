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

from .director_service import DirectorService, GameMode, PlotPoint

from .vote_manager import VoteManager, VoteOption, VoteSnapshot

from .command_handler import CommandHandler

from . import simulation



if TYPE_CHECKING:

    from .server import ConnectionManager



logger = logging.getLogger(__name__)





class _AgentSnapshot:

    """Lightweight agent data snapshot for LLM calls, avoids DetachedInstanceError."""

    __slots__ = ("id", "name", "personality", "hp", "energy", "mood", "is_sheltered")



    def __init__(self, id, name, personality, hp, energy, mood, is_sheltered=False):

        self.id = id

        self.name = name

        self.personality = personality

        self.hp = hp

        self.energy = energy

        self.mood = mood

        self.is_sheltered = is_sheltered



from .config import (

    TICK_INTERVAL, BASE_ENERGY_DECAY_PER_TICK, BASE_HP_DECAY_WHEN_STARVING,

    FEED_COST, FEED_ENERGY_RESTORE, HEAL_COST, HEAL_HP_RESTORE,

    ENCOURAGE_COST, ENCOURAGE_MOOD_BOOST, LOVE_COST, LOVE_MOOD_BOOST,

    REVIVE_COST, INITIAL_USER_GOLD, IDLE_CHAT_PROBABILITY,

    DIRECTOR_TRIGGER_INTERVAL, DIRECTOR_MIN_ALIVE_AGENTS,

    VOTING_DURATION_SECONDS, VOTE_BROADCAST_INTERVAL,

    TICKS_PER_DAY, DAY_PHASES, PHASE_MODIFIERS,

    WEATHER_TYPES, WEATHER_TRANSITIONS, WEATHER_MIN_DURATION, WEATHER_MAX_DURATION,

    SOCIAL_INTERACTIONS, INITIAL_AGENTS,

    FEED_PATTERN, CHECK_PATTERN, RESET_PATTERN, HEAL_PATTERN,

    TALK_PATTERN, ENCOURAGE_PATTERN, LOVE_PATTERN, REVIVE_PATTERN,

)





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

        # Phase 22: Contextual Dialogue System

        # Key: agent_id (who needs to respond), Value: {partner_id, last_text, topic, expires_at_tick}

        self._active_conversations = {}



        # Phase 9: AI Director & Narrative Voting

        self._director = DirectorService()

        self._vote_manager = VoteManager(

            duration_seconds=VOTING_DURATION_SECONDS,

            broadcast_interval=VOTE_BROADCAST_INTERVAL,

        )

        self._game_mode = GameMode.SIMULATION

        self._last_narrative_tick = 0

        self._current_plot: PlotPoint | None = None

        self._mode_change_tick = 0  # Tick when mode changed



        # Set up vote broadcast callback

        self._vote_manager.set_broadcast_callback(self._on_vote_update)

        self._command_handler = CommandHandler(

            broadcast_callback=self._broadcast_event,

            broadcast_vfx_callback=self._broadcast_vfx,

            trigger_agent_speak_callback=self._trigger_agent_speak,

            llm_service=llm_service,

            memory_service=memory_service,

        )



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

        """Create and broadcast a game event. Supports private messages via data['private_to']."""

        private_to = data.pop("private_to", None) if isinstance(data, dict) else None
        event = GameEvent(event_type=event_type, timestamp=time.time(), data=data)

        if private_to:
            await self._manager.broadcast(event, private_to=private_to)
        else:
            await self._manager.broadcast(event)



    async def _broadcast_vfx(self, effect: str, target_id: int = 0, message: str = "") -> None:

        """Helper to broadcast visual effect events."""

        await self._broadcast_event(EventType.VFX_EVENT, {

            "effect": effect,

            "target_id": target_id,

            "message": message

        })



    async def _broadcast_agents_status(self) -> None:

        """Broadcast all agents' current status."""

        with get_db_session() as db:

            agents = db.query(Agent).all()

            agents_data = []

            for agent in agents:

                data = agent.to_dict()

                # Phase 21-B: Inject relationships

                data["relationships"] = self._get_agent_relationships(db, agent.id)

                agents_data.append(data)

        await self._broadcast_event(EventType.AGENTS_UPDATE, {"agents": agents_data})



    def _get_agent_relationships(self, db, agent_id: int) -> list:

        """Fetch significant relationships for an agent."""

        # Phase 21-B: Only send non-stranger relationships to save bandwidth

        rels = db.query(AgentRelationship).filter(

            AgentRelationship.agent_from_id == agent_id,

            AgentRelationship.relationship_type != "stranger"

        ).all()

        

        results = []

        for r in rels:

            results.append({

                "target_id": r.agent_to_id,

                "type": r.relationship_type,

                "affection": r.affection

            })

        return results



    async def _broadcast_world_status(self) -> None:

        """Broadcast world state."""

        with get_db_session() as db:

            world = db.query(WorldState).first()

            if world:

                await self._broadcast_event(EventType.WORLD_UPDATE, world.to_dict())



    # =========================================================================

    # AI Director & Narrative Voting (Phase 9)

    # =========================================================================

    async def _on_vote_update(self, snapshot: VoteSnapshot) -> None:

        """Callback for broadcasting vote updates."""

        await self._broadcast_event(EventType.VOTE_UPDATE, snapshot.to_dict())



    async def _set_game_mode(self, new_mode: GameMode, message: str = "") -> None:

        """Switch game mode and broadcast the change."""

        old_mode = self._game_mode

        self._game_mode = new_mode

        self._mode_change_tick = self._tick_count



        ends_at = 0.0

        if new_mode == GameMode.VOTING:

            session = self._vote_manager.current_session

            if session:

                ends_at = session.end_ts



        await self._broadcast_event(EventType.MODE_CHANGE, {

            "mode": new_mode.value,

            "old_mode": old_mode.value,

            "message": message,

            "ends_at": ends_at,

        })



        logger.info(f"Game mode changed: {old_mode.value} -> {new_mode.value}")



    def _get_world_state_for_director(self) -> dict:

        """Build world state context for the Director."""

        with get_db_session() as db:

            world = db.query(WorldState).first()

            agents = db.query(Agent).filter(Agent.status == "Alive").all()



            alive_agents = [

                {"name": a.name, "hp": a.hp, "energy": a.energy, "mood": a.mood}

                for a in agents

            ]



            mood_avg = sum(a.mood for a in agents) / len(agents) if agents else 50



            return {

                "day": world.day_count if world else 1,

                "weather": world.weather if world else "Sunny",

                "time_of_day": world.time_of_day if world else "day",

                "alive_agents": alive_agents,

                "mood_avg": mood_avg,

                "recent_events": [],  # Could be populated from event history

                "tension_level": self._director.calculate_tension_level({

                    "alive_agents": alive_agents,

                    "weather": world.weather if world else "Sunny",

                    "mood_avg": mood_avg,

                }),

            }



    async def _should_trigger_narrative(self) -> bool:

        """Check if conditions are met to trigger a narrative event."""

        # Only trigger in simulation mode

        if self._game_mode != GameMode.SIMULATION:

            return False



        # Check tick interval

        ticks_since_last = self._tick_count - self._last_narrative_tick

        if ticks_since_last < DIRECTOR_TRIGGER_INTERVAL:

            return False



        # Check minimum alive agents

        with get_db_session() as db:

            alive_count = db.query(Agent).filter(Agent.status == "Alive").count()

            if alive_count < DIRECTOR_MIN_ALIVE_AGENTS:

                return False



        return True



    async def _trigger_narrative_event(self) -> None:

        """Trigger a narrative event from the Director."""

        logger.info("Director triggering narrative event...")



        # Switch to narrative mode

        await self._set_game_mode(GameMode.NARRATIVE, "The Director intervenes...")



        # Generate plot point

        world_state = self._get_world_state_for_director()

        plot = await self._director.generate_plot_point(world_state)

        self._current_plot = plot

        self._last_narrative_tick = self._tick_count



        # Broadcast narrative event

        await self._broadcast_event(EventType.NARRATIVE_PLOT, plot.to_dict())



        logger.info(f"Narrative event: {plot.title}")



        # Start voting session

        options = [

            VoteOption(choice_id=c.choice_id, text=c.text)

            for c in plot.choices

        ]

        self._vote_manager.start_vote(options, duration_seconds=VOTING_DURATION_SECONDS)



        # Broadcast vote started

        vote_data = self._vote_manager.get_vote_started_data()

        if vote_data:

            await self._broadcast_event(EventType.VOTE_STARTED, vote_data)



        # Switch to voting mode

        await self._set_game_mode(

            GameMode.VOTING,

            f"Vote now! {plot.choices[0].text} or {plot.choices[1].text}"

        )



    async def _process_voting_tick(self) -> None:

        """Process voting phase - check if voting has ended."""

        if self._game_mode != GameMode.VOTING:

            return



        result = self._vote_manager.maybe_finalize()

        if result:

            # Voting ended

            await self._broadcast_event(EventType.VOTE_ENDED, {

                "vote_id": result.vote_id,

                "total_votes": result.total_votes,

            })

            await self._broadcast_event(EventType.VOTE_RESULT, result.to_dict())



            # Switch to resolution mode

            await self._set_game_mode(

                GameMode.RESOLUTION,

                f"The audience has spoken: {result.winning_choice_text}"

            )



            # Process resolution

            await self._process_vote_result(result)



    async def _process_vote_result(self, result) -> None:

        """Process the voting result and apply consequences."""

        if not self._current_plot:

            logger.error("No current plot for resolution")

            await self._set_game_mode(GameMode.SIMULATION, "Returning to normal...")

            return



        # Get resolution from Director

        world_state = self._get_world_state_for_director()

        resolution = await self._director.resolve_vote(

            plot_point=self._current_plot,

            winning_choice_id=result.winning_choice_id,

            world_state=world_state,

        )



        # Apply effects

        await self._apply_resolution_effects(resolution.effects)



        # Broadcast resolution

        await self._broadcast_event(EventType.RESOLUTION_APPLIED, resolution.to_dict())



        logger.info(f"Resolution applied: {resolution.message}")



        # Clear current plot

        self._director.clear_current_plot()

        self._current_plot = None



        # Return to simulation after a brief pause

        await asyncio.sleep(3.0)  # Let players read the resolution

        await self._set_game_mode(GameMode.SIMULATION, "The story continues...")



    async def _apply_resolution_effects(self, effects: dict) -> None:

        """Apply resolution effects to the game world."""

        def _coerce_int(value) -> int:

            """Safely convert LLM output (string/float/int) to int."""

            try:

                return int(value)

            except (TypeError, ValueError):

                return 0



        mood_delta = _coerce_int(effects.get("mood_delta", 0))

        hp_delta = _coerce_int(effects.get("hp_delta", 0))

        energy_delta = _coerce_int(effects.get("energy_delta", 0))



        if not any([mood_delta, hp_delta, energy_delta]):

            return



        with get_db_session() as db:

            agents = db.query(Agent).filter(Agent.status == "Alive").all()

            for agent in agents:

                if mood_delta:

                    agent.mood = max(0, min(100, agent.mood + mood_delta))

                if hp_delta:

                    agent.hp = max(0, min(100, agent.hp + hp_delta))

                if energy_delta:

                    agent.energy = max(0, min(100, agent.energy + energy_delta))



        logger.info(

            f"Applied resolution effects: mood={mood_delta}, "

            f"hp={hp_delta}, energy={energy_delta}"

        )



    async def process_vote(self, voter_id: str, choice_index: int, source: str = "twitch") -> bool:

        """

        Process a vote from Twitch or Unity.



        Args:

            voter_id: Unique identifier for the voter

            choice_index: 0-indexed choice number

            source: Vote source ("twitch" or "unity")



        Returns:

            True if vote was recorded

        """

        if self._game_mode != GameMode.VOTING:

            return False



        return self._vote_manager.cast_vote(voter_id, choice_index, source)



    def parse_vote_command(self, message: str) -> int | None:

        """Parse a message for vote commands. Returns choice index or None."""

        return self._vote_manager.parse_twitch_message(message)



    # =========================================================================

    # Day/Night cycle (Phase 2)

    # =========================================================================

    async def _advance_time(self) -> None:

        await simulation._advance_time(self)



    # =========================================================================

    # Weather system (Phase 3)

    # =========================================================================

    async def _update_weather(self) -> None:

        await simulation._update_weather(self)



    async def _update_moods(self) -> None:

        await simulation._update_moods(self)



    # =========================================================================

    # Relationship 2.0 (Phase 17-B)

    # =========================================================================

    async def _assign_social_roles(self) -> None:

        await simulation._assign_social_roles(self)



    async def _process_clique_behavior(self) -> None:

        await simulation._process_clique_behavior(self)



    # =========================================================================

    # Survival mechanics

    # =========================================================================

    async def _process_survival_tick(self) -> None:

        await simulation._process_survival_tick(self)



    async def _process_auto_revive(self) -> None:

        await simulation._process_auto_revive(self)



    # =========================================================================

    # Social system (Phase 5)

    # =========================================================================

    async def _process_social_tick(self) -> None:

        await simulation._process_social_tick(self)



    def _select_interaction(self, initiator, target, relationship) -> Optional[str]:
        return simulation._select_interaction(self, initiator, target, relationship)

    async def _trigger_social_dialogue(self, interaction_data, weather, time_of_day):
        return await simulation._trigger_social_dialogue(self, interaction_data, weather, time_of_day)

    async def _process_altruism_tick(self) -> None:

        await simulation._process_altruism_tick(self)



    async def _process_activity_tick(self) -> None:

        await simulation._process_activity_tick(self)



    def _get_action_bark(self, agent, action, target) -> str:
        return simulation._get_action_bark(self, agent, action, target)

    def _find_follow_target(self, db, agent) -> Optional[Agent]:
        return simulation._find_follow_target(self, db, agent)

    def _get_inventory(self, agent) -> dict:
        return simulation._get_inventory(self, agent)

    def _set_inventory(self, agent, inv):
        return simulation._set_inventory(self, agent, inv)

    async def _consume_fruit(self, world, location) -> bool:
        return await simulation._consume_fruit(self, world, location)

    async def _gather_herb(self, agent):
        return await simulation._gather_herb(self, agent)

    async def _craft_medicine(self, agent):
        return await simulation._craft_medicine(self, agent)

    async def _use_medicine(self, agent):
        return await simulation._use_medicine(self, agent)

    async def _process_campfire_gathering(self) -> None:

        await simulation._process_campfire_gathering(self)



    async def _process_group_activity(self) -> None:

        await simulation._process_group_activity(self)



    # =========================================================================

    # LLM-powered agent speech

    # =========================================================================

    async def _trigger_agent_speak(self, agent_id, agent_name, agent_personality, agent_hp, agent_energy, agent_mood, event_description, event_type):
        return await simulation._trigger_agent_speak(self, agent_id, agent_name, agent_personality, agent_hp, agent_energy, agent_mood, event_description, event_type)

    async def _trigger_idle_chat(self):
        return await simulation._trigger_idle_chat(self)

    async def process_comment(self, user: str, message: str) -> None:

        """Process a comment through command matching."""

        await self._command_handler.handle(user, message)



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

        await simulation._process_random_events(self)



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



            # Phase 9: Check voting phase (always runs)

            await self._process_voting_tick()



            # Phase 9: Check if we should trigger a narrative event

            if await self._should_trigger_narrative():

                await self._trigger_narrative_event()



            # Skip simulation processing during narrative/voting/resolution modes

            if self._game_mode != GameMode.SIMULATION:

                await asyncio.sleep(self._tick_interval)

                continue



            # ========== SIMULATION MODE PROCESSING ==========



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



            # Phase 24: Group Activities

            # Check for campfire time (Night)

            await self._process_campfire_gathering()

            # Check for storytelling events

            await self._process_group_activity()



            # 6. Autonomous Activity (Phase 13)

            await self._process_activity_tick()



            # 7. Social interactions (Phase 5)

            await self._process_social_tick()



            # Phase 23: Altruism (Item Exchange)

            await self._process_altruism_tick()

            # Daily Exile Vote (at dusk)
            await simulation._check_and_start_exile_vote(self)
            await simulation._process_exile_vote(self)
            await simulation._process_exile_pardon_check(self)

            # Building construction progress
            await simulation._process_building_construction(self)

            # Personality behaviors
            await simulation._process_personality_behaviors(self)

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

                "alive_agents": alive_count,

                "game_mode": self._game_mode.value  # Phase 9: Include game mode

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

        """Process a command string directly (Unity UI)."""

        await self._command_handler.handle(user, text)



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

    async def _process_conversation_reply(self, db, agent, partner_id, partner_text, topic, tick_count):
        return await simulation._process_conversation_reply(self, db, agent, partner_id, partner_text, topic, tick_count)
