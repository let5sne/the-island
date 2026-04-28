"""Simulation systems for The Island - survival, social, activity, crafting."""

import asyncio
import json
import logging
import random
from typing import Optional

from .config import (
    TICK_INTERVAL, BASE_ENERGY_DECAY_PER_TICK, BASE_HP_DECAY_WHEN_STARVING,
    ENCOURAGE_MOOD_BOOST, LOVE_MOOD_BOOST,
    INITIAL_USER_GOLD, IDLE_CHAT_PROBABILITY,
    DIRECTOR_TRIGGER_INTERVAL, DIRECTOR_MIN_ALIVE_AGENTS,
    TICKS_PER_DAY, DAY_PHASES, PHASE_MODIFIERS,
    WEATHER_TYPES, WEATHER_TRANSITIONS, WEATHER_MIN_DURATION, WEATHER_MAX_DURATION,
    SOCIAL_INTERACTIONS, INITIAL_AGENTS, BUILDING_TYPES,
)
from .database import get_db_session
from .models import Agent, WorldState, GameConfig, AgentRelationship, Building
from .schemas import EventType

logger = logging.getLogger(__name__)

RANDOM_EVENTS = {
    "storm_damage": {"weight": 30, "description": "A sudden storm damages the island!"},
    "treasure_found": {"weight": 25, "description": "Someone found a buried treasure!"},
    "beast_attack": {"weight": 20, "description": "A wild beast attacks the camp!"},
    "rumor_spread": {"weight": 25, "description": "A rumor starts spreading..."},
}

class _AgentSnapshot:
    __slots__ = ("id", "name", "personality", "hp", "energy", "mood", "is_sheltered")
    def __init__(self, id, name, personality, hp, energy, mood, is_sheltered=False):
        self.id = id; self.name = name; self.personality = personality
        self.hp = hp; self.energy = energy; self.mood = mood
        self.is_sheltered = is_sheltered

# --- _advance_time ---
async def _advance_time(eng) -> Optional[dict]:

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

            

            await eng._broadcast_event(EventType.DAY_CHANGE, {

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


# --- _update_weather ---
async def _update_weather(eng) -> Optional[dict]:

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



# --- _update_moods ---
async def _update_moods(eng) -> None:

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


# --- _assign_social_roles ---
async def _assign_social_roles(eng) -> None:

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



# --- _process_clique_behavior ---
async def _process_clique_behavior(eng) -> None:

    """Leaders influence followers' actions."""

    # Run occasionally

    if eng._tick_count % 10 != 0:

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

                    await eng._broadcast_event(EventType.COMMENT, {

                        "user": "System",

                        "message": f"{follower.name} follows {leader.name}'s lead!"

                    })


# =========================================================================

# Survival mechanics

# =========================================================================


# --- _process_survival_tick ---
async def _process_survival_tick(eng) -> None:

    """Process survival mechanics with difficulty modifiers."""

    config = eng._get_config()

    deaths = []


    with get_db_session() as db:

        world = db.query(WorldState).first()

        phase_mod = PHASE_MODIFIERS.get(world.time_of_day if world else "day", {})

        weather_mod = WEATHER_TYPES.get(world.weather if world else "Sunny", {})


        alive_agents = db.query(Agent).filter(Agent.status == "Alive").all()


        for agent in alive_agents:

            # 1. Contracting Sickness

            # Phase 20-B: Check if sheltered

            agent.is_sheltered = agent.location in ["tree_left", "tree_right"]

            

            if not agent.is_sick:

                sickness_chance = 0.01 # Base 1% per tick (every 5s)

                

                # Weather impact

                current_weather = world.weather if world else "Sunny"

                if current_weather == "Rainy":

                    sickness_chance += 0.05

                elif current_weather == "Stormy":

                    sickness_chance += 0.10

                

                # Phase 20-B: Shelter mitigation (Reduce sickness chance by 80%)

                if agent.is_sheltered and current_weather in ["Rainy", "Stormy"]:

                    sickness_chance *= 0.2

                

                # Immunity impact (Higher immunity = lower chance)

                # Immunity 50 -> -2.5%, Immunity 100 -> -5%

                sickness_chance -= (agent.immunity / 2000.0) 

                

                if random.random() < sickness_chance:

                    agent.is_sick = True

                    agent.mood -= 20

                    logger.info(f"Agent {agent.name} has fallen sick!")

                    # We could broadcast a specific event, but AGENTS_UPDATE will handle visual state

                    # Just log it or maybe a system message?

                    await eng._broadcast_event(EventType.COMMENT, {

                        "user": "System", 

                        "message": f"{agent.name} is looking pale... (Sick)"

                    })


            # 2. Sickness Effects

            if agent.is_sick:

                # Decay HP and Energy faster

                agent.hp = max(0, agent.hp - 2)

                agent.energy = max(0, agent.energy - 2)

                

                # Lower mood over time

                if eng._tick_count % 5 == 0:

                    agent.mood = max(0, agent.mood - 1)


            # --- End Sickness ---


            # Calculate energy decay with all modifiers

            base_decay = BASE_ENERGY_DECAY_PER_TICK

            decay = base_decay * config.energy_decay_multiplier

            decay *= phase_mod.get("energy_decay", 1.0)

            

            weather_decay_mod = weather_mod.get("energy_modifier", 1.0)

            # Phase 20-B: Shelter mitigation (Reduce weather energy penalty by 80%)

            if agent.is_sheltered and weather_decay_mod > 1.0:

                weather_decay_mod = 1.0 + (weather_decay_mod - 1.0) * 0.2

                

            decay *= weather_decay_mod


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

                agent.death_tick = eng._tick_count

                if agent.is_sick:

                    # Clear sickness on death

                    agent.is_sick = False

                deaths.append({"name": agent.name, "personality": agent.personality})

                logger.info(f"Agent {agent.name} has died!")


    # Broadcast death events

    for death in deaths:

        await eng._broadcast_event(EventType.AGENT_DIED, {

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



# --- _process_auto_revive ---
async def _process_auto_revive(eng) -> None:

    """Auto-revive dead agents in casual mode."""

    config = eng._get_config()

    if not config.auto_revive_enabled:

        return


    with get_db_session() as db:

        dead_agents = db.query(Agent).filter(Agent.status == "Dead").all()


        for agent in dead_agents:

            if agent.death_tick is None:

                continue


            ticks_dead = eng._tick_count - agent.death_tick

            if ticks_dead >= config.auto_revive_delay_ticks:

                agent.status = "Alive"

                agent.hp = config.revive_hp

                agent.energy = config.revive_energy

                agent.mood = 50

                agent.mood_state = "neutral"

                agent.death_tick = None


                await eng._broadcast_event(EventType.AUTO_REVIVE, {

                    "agent_name": agent.name,

                    "message": f"{agent.name} has been revived!"

                })

                logger.info(f"Agent {agent.name} auto-revived")


# =========================================================================

# Social system (Phase 5)

# =========================================================================


# --- _process_social_tick ---
async def _process_social_tick(eng) -> None:

    """Process autonomous social interactions between agents."""

    config = eng._get_config()

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

        interaction_type = eng._select_interaction(initiator, target, relationship)

        if not interaction_type:

            return


        # Apply interaction effects

        effects = SOCIAL_INTERACTIONS[interaction_type]

        affection_change = random.randint(*effects["affection"])

        trust_change = random.randint(*effects.get("trust", (0, 0)))


        relationship.affection = max(-100, min(100, relationship.affection + affection_change))

        relationship.trust = max(-100, min(100, relationship.trust + trust_change))

        relationship.interaction_count += 1

        relationship.last_interaction_tick = eng._tick_count

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

    asyncio.create_task(eng._trigger_social_dialogue(

        interaction_data, weather, time_of_day

    ))



# --- _select_interaction ---
def _select_interaction(eng, initiator: Agent, target: Agent, relationship: AgentRelationship) -> Optional[str]:

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



# --- _trigger_social_dialogue ---
async def _trigger_social_dialogue(eng, interaction_data: dict, weather: str, time_of_day: str) -> None:

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


        await eng._broadcast_event(EventType.SOCIAL_INTERACTION, {

            **interaction_data,

            "dialogue": dialogue

        })

        

        # Phase 22: Contextual Dialogue - Store context for responder

        # Initiator just spoke. Target needs to respond next tick.

        initiator_id = interaction_data["initiator_id"]

        target_id = interaction_data["target_id"]

        

        # 50% chance to continue the conversation (A -> B -> A)

        should_continue = True # For the first response (A->B), almost always yes unless "argue" maybe?

        

        if should_continue:

            eng._active_conversations[target_id] = {

                "partner_id": initiator_id,

                "last_text": dialogue,

                "topic": interaction_data["interaction_type"], # Rough topic

                "expires_at_tick": eng._tick_count + 5 # Must respond within 5 ticks

            }


    except Exception as e:

        logger.error(f"Error in social dialogue: {e}")


# =========================================================================

# Economy / Altruism (Phase 23)

# =========================================================================


# --- _process_altruism_tick ---
async def _process_altruism_tick(eng) -> None:

    """Process altruistic item sharing based on need."""

    if random.random() > 0.5: # 50% chance per tick to check

         return


    with get_db_session() as db:

        agents = db.query(Agent).filter(Agent.status == "Alive").all()

        # Shuffle to avoid priority bias

        random.shuffle(agents)


        for giver in agents:

            giver_inv = eng._get_inventory(giver)

            

            # Check surplus

            item_to_give = None

            # Give Herb if have plenty

            if giver_inv.get("herb", 0) >= 3:

                 item_to_give = "herb"

            # Give Food if have plenty and energy is high

            elif giver_inv.get("food", 0) >= 1 and giver.energy > 80:

                 item_to_give = "food"

            

            if not item_to_give:

                 continue


            # Find needy neighbor

            for candidate in agents:

                if candidate.id == giver.id: continue

                

                cand_inv = eng._get_inventory(candidate)

                score = 0

                

                if item_to_give == "herb":

                    # High priority: Sick and no herbs

                    if candidate.is_sick and cand_inv.get("herb", 0) == 0:

                        score = 100

                elif item_to_give == "food":

                     # High priority: Starving and no food

                     if candidate.energy < 30 and cand_inv.get("food", 0) == 0:

                        score = 50

                

                if score > 0:

                    # Check relationship (don't give to enemies)

                     rel = db.query(AgentRelationship).filter(

                         AgentRelationship.agent_from_id == giver.id, 

                         AgentRelationship.agent_to_id == candidate.id

                     ).first()

                     type_ = rel.relationship_type if rel else "stranger"

                     if type_ in ["rival", "enemy"]:

                         continue

                     

                     # Execute Give

                     giver_inv[item_to_give] -= 1

                     eng._set_inventory(giver, giver_inv)

                     

                     cand_inv[item_to_give] = cand_inv.get(item_to_give, 0) + 1

                     eng._set_inventory(candidate, cand_inv)

                     

                     # Update Relationship (Giver -> Receiver)

                     if not rel:

                         rel = AgentRelationship(agent_from_id=giver.id, agent_to_id=candidate.id)

                         db.add(rel)

                     

                     rel.affection = min(100, rel.affection + 10)

                     rel.trust = min(100, rel.trust + 5)

                     rel.interaction_count += 1

                     rel.update_relationship_type()

                     

                     # Update Relationship (Receiver -> Giver)

                     rel2 = db.query(AgentRelationship).filter(

                         AgentRelationship.agent_from_id == candidate.id,

                         AgentRelationship.agent_to_id == giver.id

                     ).first()

                     if not rel2:

                          rel2 = AgentRelationship(agent_from_id=candidate.id, agent_to_id=giver.id)

                          db.add(rel2)

                     rel2.affection = min(100, rel2.affection + 8)

                     rel2.trust = min(100, rel2.trust + 3)

                     rel2.update_relationship_type()


                     # Broadcast

                     await eng._broadcast_event(EventType.GIVE_ITEM, {

                         "from_id": giver.id,

                         "to_id": candidate.id,

                         "item_type": item_to_give,

                         "message": f"{giver.name} gave 1 {item_to_give} to {candidate.name}."

                     })

                     logger.info(f"{giver.name} gave {item_to_give} to {candidate.name}")

                     

                     # One action per agent per tick

                     break


# --- _process_activity_tick ---
async def _process_activity_tick(eng) -> None:

    """Decide and execute autonomous agent actions."""

    # Only process activity every few ticks to avoid chaotic movement

    if eng._tick_count % 3 != 0:

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


            # Phase 22: Handle Pending Conversations (High Priority)

            if agent.id in eng._active_conversations:

                 pending = eng._active_conversations[agent.id]

                 # Check expiry

                 if eng._tick_count > pending["expires_at_tick"]:

                     del eng._active_conversations[agent.id]

                 else:

                     # Force response

                     new_action = "Chat"

                     new_location = agent.location # Stay put

                     should_update = True

                     

                     # Generate Response Immediately

                     partner = db.query(Agent).filter(Agent.id == pending["partner_id"]).first()

                     if partner:

                         target_name = partner.name

                         # Generate reply

                         # We consume the pending state so we don't loop forever

                         previous_text = pending["last_text"]

                         del eng._active_conversations[agent.id]

                         

                         # Maybe add a chance for A to respond back to B (A-B-A)?

                         # For simplicity, let's just do A-B for now, or 50% chance for A-B-A

                         should_reply_back = random.random() < 0.5


                         # Extract values before async task (avoid detached session issues)

                         asyncio.create_task(eng._process_conversation_reply(

                             agent.id, agent.name, partner.id, partner.name,

                             previous_text, pending["topic"], should_reply_back

                         ))

                     else:

                         del eng._active_conversations[agent.id] 


            # 1. Critical Needs (Override everything)

            elif world.time_of_day == "night":

                if agent.current_action != "Sleep":

                    new_action = "Sleep"

                    new_location = "campfire"

                    should_update = True

            elif agent.energy < 30:

                if agent.current_action != "Gather":

                    new_action = "Gather"

                    new_location = random.choice(["tree_left", "tree_right"])

                    should_update = True

            

            # Phase 20-B: Seek Shelter during Storms

            elif world.weather == "Stormy" and not agent.is_sheltered:

                if agent.current_action != "Seek Shelter":

                    new_action = "Seek Shelter"

                    new_location = random.choice(["tree_left", "tree_right"])

                    should_update = True

            

            # 1.5. Sickness Handling (Phase 16)

            elif agent.is_sick:

                inv = eng._get_inventory(agent)

                if inv.get("medicine", 0) > 0:

                    # Use medicine immediately

                    await eng._use_medicine(agent)

                    new_action = "Use Medicine"

                    new_location = agent.location

                    should_update = True

                elif inv.get("herb", 0) >= 3:

                    # Craft medicine

                    await eng._craft_medicine(agent)

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

                    target_name = friend.name

                    should_update = True

            

            # Phase 21-C: Advanced Social Locomotion (Follow)

            # If "follower" role (or just feeling social), follow a friend/leader

            elif agent.current_action not in ["Sleep", "Gather", "Dance", "Follow"] and random.random() < 0.15:

                target = eng._find_follow_target(db, agent)

                if target:

                    new_action = "Follow"

                    new_location = "agent"

                    target_name = target.name

                    should_update = True

            # If Happy (>80) and near others, chance to start dancing

            elif agent.mood > 80 and agent.current_action != "Dance":

                # Check for nearby agents (same location)

                nearby_count = 0

                for other in agents:

                     if other.id != agent.id and other.status == "Alive" and other.location == agent.location:

                        nearby_count += 1

                

                # Dance Party Trigger! (Need at least 1 friend, 10% chance)

                if nearby_count >= 1 and random.random() < 0.10:

                    new_action = "Dance"

                    # Keep location same

                    new_location = agent.location

                    should_update = True


            # 2. Idle Behavior (Default)

            elif agent.current_action not in ["Sleep", "Chat", "Dance"]:

                # Random chance to move nearby or chat

                if random.random() < 0.3:

                    new_action = "Wander"

                    new_location = "nearby" # Will be randomized in Unity/GameManager mapping

                    should_update = True

                # Phase 23: Altruism - Give Item if needed (50% chance per tick to check)

                if random.random() < 0.5:

                    await eng._process_altruism_tick()

                elif random.random() < 0.1:

                    new_action = "Idle"

                    should_update = True

            

            # 4. Finish Tasks (Simulation)

            elif agent.current_action == "Gather" and agent.energy >= 90:

                new_action = "Idle"

                new_location = "center"

                should_update = True

            elif agent.current_action == "Gather" and agent.location in ["tree_left", "tree_right"]:

                # Phase 17-A: Consume fruit when gathering

                fruit_available = await eng._consume_fruit(world, agent.location)

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

                        await eng._broadcast_event(EventType.COMMENT, {

                            "user": "System",

                            "message": f"{agent.name} can't find any fruit! The trees are empty..."

                        })

            elif agent.current_action == "Sleep" and world.time_of_day != "night":

                new_action = "Wake Up"

                new_location = "center"

                should_update = True

            elif agent.current_action == "Gather Herb":

                # Simulate herb gathering (add herbs)

                await eng._gather_herb(agent)

                new_action = "Idle"

                new_location = "center"

                should_update = True


            # Execute Update

            if should_update:

                agent.current_action = new_action

                agent.location = new_location

                

                # Generate simple thought/bark

                dialogue = eng._get_action_bark(agent, new_action, target_name)


                await eng._broadcast_event(EventType.AGENT_ACTION, {

                    "agent_id": agent.id,

                    "agent_name": agent.name,

                    "action_type": new_action,

                    "location": new_location,

                    "target_name": target_name,

                    "dialogue": dialogue

                })



# --- _get_action_bark ---
def _get_action_bark(eng, agent: Agent, action: str, target: str = None) -> str:

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

    elif action == "Dance":

        return random.choice(["Party time!", "Let's dance!", "Woo!"])

    elif action == "Follow":

        return f"Wait for me, {target}!"

    return ""



# --- _find_follow_target ---
def _find_follow_target(eng, db, agent: Agent) -> Optional[Agent]:

    """Find a suitable target to follow (Leader or Friend)."""

    # 1. Prefer Leaders

    leader = db.query(Agent).filter(

        Agent.social_role == "leader", 

        Agent.status == "Alive",

        Agent.id != agent.id

    ).first()

    

    if leader and random.random() < 0.7:

        return leader


    # 2. Fallback to Close Friends

    rels = db.query(AgentRelationship).filter(

        AgentRelationship.agent_from_id == agent.id,

        AgentRelationship.relationship_type.in_(["close_friend", "friend"])

    ).all()

    

    if rels:

        r = random.choice(rels)

        target = db.query(Agent).filter(Agent.id == r.agent_to_id, Agent.status == "Alive").first()

        return target

        

    return None


# =========================================================================

# Inventory & Crafting (Phase 16)

# =========================================================================


# --- _get_inventory ---
def _get_inventory(eng, agent: Agent) -> dict:

    """Parse agent inventory JSON."""

    import json

    try:

        return json.loads(agent.inventory) if agent.inventory else {}

    except json.JSONDecodeError:

        return {}



# --- _set_inventory ---
def _set_inventory(eng, agent: Agent, inv: dict) -> None:

    """Set agent inventory from dict."""

    import json

    agent.inventory = json.dumps(inv)



# --- _consume_fruit ---
async def _consume_fruit(eng, world: WorldState, location: str) -> bool:

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



# --- _gather_herb ---
async def _gather_herb(eng, agent: Agent) -> None:

    """Agent gathers herbs."""

    inv = eng._get_inventory(agent)

    herbs_found = random.randint(1, 2)

    inv["herb"] = inv.get("herb", 0) + herbs_found

    eng._set_inventory(agent, inv)

    

    await eng._broadcast_event(EventType.AGENT_ACTION, {

        "agent_id": agent.id,

        "agent_name": agent.name,

        "action_type": "Gather Herb",

        "location": "herb_patch",

        "dialogue": f"Found {herbs_found} herbs!"

    })

    logger.info(f"Agent {agent.name} gathered {herbs_found} herbs. Total: {inv['herb']}")



# --- _craft_medicine ---
async def _craft_medicine(eng, agent: Agent) -> None:

    """Agent crafts medicine from herbs."""

    inv = eng._get_inventory(agent)

    if inv.get("herb", 0) >= 3:

        inv["herb"] -= 3

        inv["medicine"] = inv.get("medicine", 0) + 1

        eng._set_inventory(agent, inv)

        

        await eng._broadcast_event(EventType.CRAFT, {

            "agent_id": agent.id,

            "agent_name": agent.name,

            "item": "medicine",

            "ingredients": {"herb": 3}

        })

        logger.info(f"Agent {agent.name} crafted medicine. Inventory: {inv}")



# --- _use_medicine ---
async def _use_medicine(eng, agent: Agent) -> None:

    """Agent uses medicine to cure sickness."""

    inv = eng._get_inventory(agent)

    if inv.get("medicine", 0) > 0:

        inv["medicine"] -= 1

        eng._set_inventory(agent, inv)

        

        agent.is_sick = False

        agent.hp = min(100, agent.hp + 20)

        agent.mood = min(100, agent.mood + 10)

        

        await eng._broadcast_event(EventType.USE_ITEM, {

            "agent_id": agent.id,

            "agent_name": agent.name,

            "item": "medicine",

            "effect": "cured sickness"

        })

        logger.info(f"Agent {agent.name} used medicine and is cured!")


# =========================================================================

# Phase 24: Group Activities & Rituals

# =========================================================================


# --- _process_campfire_gathering ---
async def _process_campfire_gathering(eng) -> None:

    """Encourage agents to gather at campfire at night."""

    with get_db_session() as db:

        world = db.query(WorldState).first()

        if not world or world.time_of_day != "night":

            return

            

        # Only run check occasionally to avoid spamming decision logic every tick if not needed

        if eng._tick_count % 5 != 0:

            return


        agents = db.query(Agent).filter(Agent.status == "Alive").all()

        for agent in agents:

            # If agent is critical, they will prioritize self-preservation in _process_activity_tick

            # But if they are just idle or wandering, we nudge them to campfire

            if agent.hp < 30 or agent.energy < 20 or agent.is_sick:

                continue

            

            # If already there, stay

            if agent.location == "campfire":

                continue

                

            # Force move to campfire "ritual"

            # We update their "current_action" so the next tick they don't override it immediately

            # But _process_activity_tick runs based on priorities. 

            # To make this sticky, we might need a "GroupActivity" state or just rely on 

            # tweaking the decision logic. For now, let's just forcefully set target if Idle.

            if agent.current_action in ["Idle", "Wander"]:

                agent.current_action = "Gathering"

                agent.location = "campfire" # Teleport logic or Move logic? 

                # Actually, our decision logic sets location.

                # Let's just update location for simplicity as 'walking' is handled by frontend interpolation 

                # if the distance is small, but massive jumps might look weird. 

                # Ideally we set a goal. But for this engine, setting location IS the action result usually.

                pass 



# --- _process_group_activity ---
async def _process_group_activity(eng) -> None:

    """Trigger storytelling if enough agents are at the campfire."""

    # Only at night

    with get_db_session() as db:

        world = db.query(WorldState).first()

        if not world or world.time_of_day != "night":

            return

            

        # Low probability check (don't spam stories)

        if random.random() > 0.05:

            return

            

        # Check who is at campfire

        agents_at_fire = db.query(Agent).filter(

            Agent.status == "Alive",

            Agent.location == "campfire"

        ).all()

        

        if len(agents_at_fire) < 2:

            return

            

        # Select Storyteller (Highest Mood or Extrovert)

        storyteller = max(agents_at_fire, key=lambda a: a.mood + (20 if a.social_tendency == 'extrovert' else 0))

        listeners = [a for a in agents_at_fire if a.id != storyteller.id]

        

        # Generate Story

        topics = ["the ghost ship", "the ancient ruins", "a strange dream", "the day we arrived"]

        topic = random.choice(topics)

        

        story_content = await llm_service.generate_story(storyteller.name, topic)

        

        # Broadcast Event

        await eng._broadcast_event(EventType.GROUP_ACTIVITY, {

            "activity_type": "storytelling",

            "storyteller_id": storyteller.id,

            "storyteller_name": storyteller.name,

            "listener_ids": [l.id for l in listeners],

            "content": story_content,

            "topic": topic

        })

        

        # Boost Mood for everyone involved

        storyteller.mood = min(100, storyteller.mood + 10)

        for listener in listeners:

            listener.mood = min(100, listener.mood + 5)


# =========================================================================

# LLM-powered agent speech

# =========================================================================


# --- _trigger_agent_speak ---
async def _trigger_agent_speak(

    self, agent_id: int, agent_name: str, agent_personality: str,

    agent_hp: int, agent_energy: int, agent_mood: int,

    event_description: str, event_type: str = "feed"

) -> None:

    """Fire-and-forget LLM call to generate agent speech."""

    try:

        agent_snapshot = _AgentSnapshot(

            agent_id, agent_name, agent_personality, agent_hp, agent_energy, agent_mood

        )


        text = await llm_service.generate_reaction(agent_snapshot, event_description, event_type)


        await eng._broadcast_event(EventType.AGENT_SPEAK, {

            "agent_id": agent_id,

            "agent_name": agent_name,

            "text": text

        })


    except Exception as e:

        logger.error(f"Error in agent speak: {e}")



# --- _trigger_idle_chat ---
async def _trigger_idle_chat(eng) -> None:

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

            "mood_state": agent.mood_state, "is_sheltered": agent.is_sheltered

        }


    try:

        agent_snapshot = _AgentSnapshot(

            agent_data["id"], agent_data["name"], agent_data["personality"],

            agent_data["hp"], agent_data["energy"], agent_data["mood"],

            agent_data.get("is_sheltered", False)

        )


        text = await llm_service.generate_idle_chat(agent_snapshot, weather, time_of_day)


        await eng._broadcast_event(EventType.AGENT_SPEAK, {

            "agent_id": agent_data["id"],

            "agent_name": agent_data["name"],

            "text": text

        })


    except Exception as e:

        logger.error(f"Error in idle chat: {e}")


# =========================================================================


# --- _process_random_events ---
async def _process_random_events(eng) -> None:

    """Process random events (10% chance per day at dawn)."""

    # Only trigger at dawn (once per day)

    if eng._tick_count % 100 != 1:  # Roughly once every ~100 ticks

        return

    

    if random.random() > 0.10:  # 10% chance

        return


    # Pick random event

    events = list(eng.RANDOM_EVENTS.keys())

    weights = [eng.RANDOM_EVENTS[e]["weight"] for e in events]

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

            inv = eng._get_inventory(lucky)

            inv["medicine"] = inv.get("medicine", 0) + 2

            inv["herb"] = inv.get("herb", 0) + 3

            eng._set_inventory(lucky, inv)

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


        await eng._broadcast_event(EventType.RANDOM_EVENT, event_data)

        logger.info(f"Random event triggered: {event_type}")


# =========================================================================

# Game loop

# =========================================================================


# --- _process_conversation_reply ---
async def _process_conversation_reply(

    self, responder_id: int, responder_name: str, partner_id: int, partner_name: str,

    previous_text: str, topic: str, should_reply_back: bool

) -> None:

    """Handle the secondary turn of a conversation."""

    try:

         # Relationship

         with get_db_session() as db:

             rel = db.query(AgentRelationship).filter(

                 AgentRelationship.agent_from_id == responder_id,

                 AgentRelationship.agent_to_id == partner_id

             ).first()

             rel_type = rel.relationship_type if rel else "acquaintance"


             # Basic world info

             world = db.query(WorldState).first()

             weather = world.weather if world else "Sunny"

             time_of_day = world.time_of_day if world else "day"


         # Generate reply

         # We use the same generate_social_interaction but with previous_dialogue set

         # 'interaction_type' is reused as topic

         reply = await llm_service.generate_social_interaction(

             initiator_name=responder_name,

             target_name=partner_name,

             interaction_type=topic,

             relationship_type=rel_type,

             weather=weather,

             time_of_day=time_of_day,

             previous_dialogue=previous_text

         )


         # Broadcast response

         await eng._broadcast_event(EventType.SOCIAL_INTERACTION, {

            "initiator_id": responder_id,

            "initiator_name": responder_name,

            "target_id": partner_id,

            "target_name": partner_name,

            "interaction_type": "reply",

            "relationship_type": rel_type,

            "dialogue": reply

        })


         # Chain next turn?

         if should_reply_back:

             eng._active_conversations[partner_id] = {

                "partner_id": responder_id,

                "last_text": reply,

                "topic": topic,

                "expires_at_tick": eng._tick_count + 5 # Must respond within 5 ticks

            }


    except Exception as e:

        logger.error(f"Error in conversation reply: {e}")


# =============================================================================
# Building Construction Processing
# =============================================================================

async def _process_building_construction(eng) -> None:
    """Advance construction progress for all incomplete buildings."""
    with get_db_session() as db:
        buildings = db.query(Building).filter(Building.is_complete == False).all()
        if not buildings:
            return

        for b in buildings:
            bt = BUILDING_TYPES.get(b.building_type, {})
            ticks_needed = bt.get("construction_ticks", 10)
            progress_per_tick = 100 // max(ticks_needed, 1)
            b.construction_progress = min(100, b.construction_progress + progress_per_tick)

            if b.construction_progress >= 100:
                b.is_complete = True
                await eng._broadcast_event("building_completed", {
                    "building_id": b.id,
                    "building_type": b.building_type,
                    "name": b.name,
                    "built_by": b.built_by,
                    "message": f"The {b.name} has been completed!",
                })
                logger.info(f"Building completed: {b.name} by {b.built_by}")


# =============================================================================
# Trading System
# =============================================================================

async def _process_trade(eng, from_agent: Agent, to_agent: Agent, item: str, quantity: int) -> bool:
    """Agent-to-agent item trading."""
    from_inv = eng._get_inventory(from_agent)
    to_inv = eng._get_inventory(to_agent)

    if from_inv.get(item, 0) < quantity:
        return False

    from_inv[item] = from_inv[item] - quantity
    to_inv[item] = to_inv.get(item, 0) + quantity

    eng._set_inventory(from_agent, from_inv)
    eng._set_inventory(to_agent, to_inv)

    from_agent.mood = min(100, from_agent.mood + 3)  # Generosity boost
    to_agent.mood = min(100, to_agent.mood + 5)      # Receiving boost

    await eng._broadcast_event("give_item", {
        "from_agent_id": from_agent.id,
        "from_name": from_agent.name,
        "to_agent_id": to_agent.id,
        "to_name": to_agent.name,
        "item": item,
        "quantity": quantity,
        "message": f"{from_agent.name} gave {quantity}x {item} to {to_agent.name}!",
    })
    return True


async def _process_rumor(eng, message: str, username: str) -> dict | None:
    """Process a chat message as a 'rumor' that influences AI opinions."""
    try:
        return await _process_rumor_impl(eng, message, username)
    except Exception:
        return None


async def _process_rumor_impl(eng, message: str, username: str) -> dict | None:
    with get_db_session() as db:
        agents = db.query(Agent).filter(Agent.status == "Alive").all()
        mentioned = [a for a in agents if a.name.lower() in message.lower()]
        if len(mentioned) < 1:
            return None

        negative_words = ["steal", "lie", "cheat", "bad", "evil", "kill", "hate", "betray"]
        positive_words = ["help", "good", "kind", "share", "friend", "hero", "save", "love"]
        is_negative = any(w in message.lower() for w in negative_words)
        is_positive = any(w in message.lower() for w in positive_words)
        shift = -5 if is_negative else 5 if is_positive else 2

        effects = []
        target = mentioned[0]
        for other in agents:
            if other.id != target.id:
                rel = db.query(AgentRelationship).filter(
                    AgentRelationship.agent_from_id == other.id,
                    AgentRelationship.agent_to_id == target.id,
                ).first()
                if rel:
                    rel.trust = max(-100, min(100, rel.trust + shift))
                    effects.append(f"{other.name} trust {target.name}: {'+' if shift > 0 else ''}{shift}")

        target.mood = max(0, min(100, target.mood + (3 if is_positive else -3)))

        await eng._broadcast_event("rumor_effect", {
            "username": username, "message": message,
            "target": target.name, "shift": shift, "effects": effects,
        })
        return {"target": target.name, "shift": shift}


# =============================================================================
# Daily Exile Vote
# =============================================================================

EXILE_VOTE_ACTIVE = False
EXILE_VOTES: dict[int, int] = {}  # voter_agent_id -> target_agent_id
EXILE_VOTE_TICK_STARTED = 0
EXILE_CONDEMNED: str | None = None
EXILE_PARDON_WINDOW = 30  # ticks to wait for pardon


async def _check_and_start_exile_vote(eng) -> bool:
    """Start daily exile vote at dusk if conditions met."""
    global EXILE_VOTE_ACTIVE, EXILE_VOTES, EXILE_VOTE_TICK_STARTED, EXILE_CONDEMNED

    with get_db_session() as db:
        world = db.query(WorldState).first()
        if not world or world.time_of_day != "dusk":
            return False

        alive = db.query(Agent).filter(Agent.status == "Alive").all()
        if len(alive) < 3:
            return False

    EXILE_VOTE_ACTIVE = True
    EXILE_VOTES = {}
    EXILE_VOTE_TICK_STARTED = eng._tick_count
    EXILE_CONDEMNED = None

    await eng._broadcast_event(EventType.EXILE_VOTE_START, {
        "message": "Dusk falls. The agents gather around the campfire to decide who shall be exiled...",
        "alive_count": len(alive),
    })
    return True


async def _process_exile_vote(eng) -> dict | None:
    """Each tick, one agent casts their exile vote."""
    global EXILE_VOTE_ACTIVE, EXILE_VOTES, EXILE_CONDEMNED

    if not EXILE_VOTE_ACTIVE:
        return None

    with get_db_session() as db:
        alive = db.query(Agent).filter(Agent.status == "Alive").all()
        if len(alive) < 3:
            EXILE_VOTE_ACTIVE = False
            return None

        # Find an agent who hasn't voted yet
        for agent in alive:
            if agent.id not in EXILE_VOTES:
                # Vote for whoever has lowest relationship or lowest contribution
                candidates = [a for a in alive if a.id != agent.id]
                if not candidates:
                    continue

                # Pick target based on relationships (negative affection = more likely target)
                target = random.choice(candidates)
                for c in candidates:
                    rel = db.query(AgentRelationship).filter(
                        AgentRelationship.agent_from_id == agent.id,
                        AgentRelationship.agent_to_id == c.id,
                    ).first()
                    if rel and rel.affection < -10:
                        target = c
                        break  # Vote against someone they dislike

                EXILE_VOTES[agent.id] = target.id
                await eng._broadcast_event(EventType.EXILE_VOTE_CAST, {
                    "voter": agent.name,
                    "target": target.name,
                    "reason": f"{agent.name} votes to exile {target.name}.",
                })
                break  # Process one vote per tick

        # Check if all have voted
        if len(EXILE_VOTES) >= len(alive):
            return await _finalize_exile_vote(eng)

    return None


async def _finalize_exile_vote(eng) -> dict:
    """Count votes and determine condemned agent."""
    global EXILE_VOTE_ACTIVE, EXILE_CONDEMNED

    vote_counts: dict[int, int] = {}
    for target_id in EXILE_VOTES.values():
        vote_counts[target_id] = vote_counts.get(target_id, 0) + 1

    if not vote_counts:
        EXILE_VOTE_ACTIVE = False
        return {"condemned": None, "tallies": {}}

    max_votes = max(vote_counts.values())
    condemned_id = max(vote_counts, key=vote_counts.get)

    with get_db_session() as db:
        condemned = db.query(Agent).filter(Agent.id == condemned_id).first()

    if condemned:
        EXILE_CONDEMNED = condemned.name
        result = {
            "condemned": condemned.name,
            "condemned_id": condemned.id,
            "tallies": {a.name: vote_counts.get(a.id, 0) for a in db.query(Agent).filter(Agent.status == "Alive").all()},
            "message": f"{condemned.name} has been condemned to exile with {max_votes} votes!",
        }
        await eng._broadcast_event(EventType.EXILE_VOTE_RESULT, result)

        # Generate pardon plea
        from app.llm import llm_service
        plea = await llm_service.generate_pardon_plea(condemned.name, condemned.personality)
        await eng._broadcast_event(EventType.PARDON_PLEA, {
            "agent_name": condemned.name,
            "plea": plea,
            "message": f"{condemned.name} begs: '{plea}' — Send 'pardon {condemned.name}' to save them!",
        })

        return result

    EXILE_VOTE_ACTIVE = False
    return {"condemned": None, "tallies": {}}


async def _execute_exile(eng, agent_name: str) -> bool:
    """Execute exile on the condemned agent."""
    with get_db_session() as db:
        agent = db.query(Agent).filter(
            Agent.name.ilike(agent_name), Agent.status == "Alive"
        ).first()
        if not agent:
            return False

        agent.status = "Exiled"
        agent.death_tick = eng._tick_count
        agent.current_action = "Exiled"
        agent.mood = 0

        await eng._broadcast_event(EventType.EXILE_EXECUTED, {
            "agent_id": agent.id,
            "agent_name": agent.name,
            "message": f"{agent.name} has been exiled from the island. They are gone forever.",
        })
        await eng._broadcast_event(EventType.AGENT_DIED, {
            "agent_id": agent.id,
            "agent_name": agent.name,
        })
        return True


async def _process_exile_pardon_check(eng) -> bool:
    """Check if pardon window has expired. If so, execute exile."""
    global EXILE_VOTE_ACTIVE, EXILE_CONDEMNED

    if not EXILE_VOTE_ACTIVE:
        return False

    if EXILE_CONDEMNED and eng._tick_count - EXILE_VOTE_TICK_STARTED > EXILE_PARDON_WINDOW:
        await _execute_exile(eng, EXILE_CONDEMNED)
        EXILE_VOTE_ACTIVE = False
        EXILE_CONDEMNED = None
        EXILE_VOTES.clear()
        return True

    return False


async def _pardon_agent(eng, viewer_username: str, agent_name: str) -> bool:
    """Grant pardon to a condemned agent (赎罪券)."""
    global EXILE_VOTE_ACTIVE, EXILE_CONDEMNED

    if not EXILE_VOTE_ACTIVE or not EXILE_CONDEMNED:
        return False

    if EXILE_CONDEMNED.lower() != agent_name.lower():
        return False

    with get_db_session() as db:
        agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()
        if not agent:
            return False

        agent.loyal_to = viewer_username
        agent.mood = 100
        agent.influence_score = (agent.influence_score or 0) + 50

        await eng._broadcast_event(EventType.PARDON_GRANTED, {
            "agent_name": agent.name,
            "viewer": viewer_username,
            "message": f"{viewer_username} has pardoned {agent.name}! They are eternally grateful and loyal.",
        })

    EXILE_VOTE_ACTIVE = False
    EXILE_CONDEMNED = None
    EXILE_VOTES.clear()
    return True

