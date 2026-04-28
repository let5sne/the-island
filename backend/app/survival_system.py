"""Survival system - HP, energy, sickness, weather, time, and random events."""

import logging
import random

from .config import (
    BASE_ENERGY_DECAY_PER_TICK, BASE_HP_DECAY_WHEN_STARVING,
    TICKS_PER_DAY, DAY_PHASES, PHASE_MODIFIERS,
    WEATHER_TYPES, WEATHER_TRANSITIONS, WEATHER_MIN_DURATION, WEATHER_MAX_DURATION,
)
from .database import get_db_session
from .models import Agent, WorldState, GameConfig
from .schemas import EventType

logger = logging.getLogger(__name__)

RANDOM_EVENTS = {
    "storm_damage": {"weight": 30, "description": "A sudden storm damages the island!"},
    "treasure_found": {"weight": 25, "description": "Someone found a buried treasure!"},
    "beast_attack": {"weight": 20, "description": "A wild beast attacks the camp!"},
    "rumor_spread": {"weight": 25, "description": "A rumor starts spreading..."},
}


class SurvivalSystem:
    """Manages HP, energy, sickness, weather, time, and random events."""

    def __init__(self, broadcast_callback, memory_service=None):
        self._broadcast = broadcast_callback
        self._memory = memory_service

    async def advance_time(self) -> dict | None:
        with get_db_session() as db:
            world = db.query(WorldState).first()
            if not world:
                return None
            world.current_tick_in_day += 1
            old_phase = world.time_of_day
            new_phase = old_phase
            new_day = False
            tick_in = world.current_tick_in_day

            for phase, (start, end) in DAY_PHASES.items():
                if start <= tick_in <= end:
                    new_phase = phase
                    break
            else:
                world.current_tick_in_day = 0
                world.day_count += 1
                new_phase = "dawn"
                new_day = True
                if world.tree_left_fruit < 5:
                    world.tree_left_fruit = min(5, world.tree_left_fruit + 1)
                if world.tree_right_fruit < 5:
                    world.tree_right_fruit = min(5, world.tree_right_fruit + 1)

            world.time_of_day = new_phase
            if new_phase != old_phase or new_day:
                return {"old_phase": old_phase, "new_phase": new_phase, "day": world.day_count}
            return None

    async def update_weather(self) -> dict | None:
        with get_db_session() as db:
            world = db.query(WorldState).first()
            if not world:
                return None
            world.weather_duration += 1
            if world.weather_duration >= random.randint(WEATHER_MIN_DURATION, WEATHER_MAX_DURATION):
                old = world.weather
                transitions = WEATHER_TRANSITIONS.get(old, {"Sunny": 1.0})
                new = random.choices(list(transitions.keys()), weights=list(transitions.values()), k=1)[0]
                if new != old:
                    world.weather = new
                    world.weather_duration = 0
                    return {"old_weather": old, "new_weather": new}
            return None

    async def update_moods(self) -> None:
        with get_db_session() as db:
            world = db.query(WorldState).first()
            if not world:
                return
            phase = world.time_of_day
            weather = world.weather
            phase_mod = PHASE_MODIFIERS.get(phase, {})
            weather_type = WEATHER_TYPES.get(weather, {})
            agent_mod = weather_type.get("mood_change", 0)
            phase_mod_val = phase_mod.get("mood_change", 0)
            total_mod = agent_mod + phase_mod_val
            agents = db.query(Agent).filter(Agent.status == "Alive").all()
            for agent in agents:
                agent.mood = max(0, min(100, agent.mood + total_mod))
                if agent.mood >= 70:
                    agent.mood_state = "happy"
                elif agent.mood >= 40:
                    agent.mood_state = "neutral"
                elif agent.mood >= 20:
                    agent.mood_state = "sad"
                else:
                    agent.mood_state = "anxious"

    async def process_survival_tick(self) -> None:
        """Process energy decay, sickness, HP recovery, starvation, and death."""
        with get_db_session() as db:
            config = db.query(GameConfig).first()
            energy_mult = config.energy_decay_multiplier if config else 0.5
            hp_mult = config.hp_decay_multiplier if config else 0.5
            world = db.query(WorldState).first()
            weather = (world.weather if world else "Sunny").lower()
            weather_energy_mod = WEATHER_TYPES.get(world.weather if world else "Sunny", {}).get("energy_modifier", 1.0)
            agents = db.query(Agent).filter(Agent.status == "Alive").all()

            for agent in agents:
                is_sheltered = agent.is_sheltered or (agent.location == "campfire" and world and world.time_of_day == "night")
                # Sickness check
                sickness_chance = 0.01
                if weather in ("rainy",):
                    sickness_chance += 0.05
                elif weather in ("stormy",):
                    sickness_chance += 0.10
                if is_sheltered:
                    sickness_chance *= 0.2
                immunity_factor = max(0, agent.immunity / 2000.0)
                sickness_chance = max(0, sickness_chance - immunity_factor)
                if random.random() < sickness_chance and not agent.is_sick:
                    agent.is_sick = True
                    await self._broadcast("system", {"message": f"{agent.name} has fallen ill!"})

                # Energy decay
                energy_loss = int(BASE_ENERGY_DECAY_PER_TICK * energy_mult * weather_energy_mod)
                if agent.is_sick:
                    energy_loss += 2
                if is_sheltered:
                    energy_loss = max(0, energy_loss - 1)
                agent.energy = max(0, agent.energy - energy_loss)

                # HP recovery at high energy
                if agent.energy >= 60 and not agent.is_sick:
                    agent.hp = min(100, agent.hp + 1)

                # Starvation damage
                if agent.energy <= 0:
                    hp_loss = int(BASE_HP_DECAY_WHEN_STARVING * hp_mult)
                    agent.hp = max(0, agent.hp - hp_loss)
                    if agent.is_sick:
                        agent.hp = max(0, agent.hp - 2)

                # Death check
                if agent.hp <= 0:
                    agent.status = "Dead"
                    agent.death_tick = agent.death_tick or 0  # Will be set by caller
                    agent.current_action = "Dead"
                    await self._broadcast("agent_died", {
                        "agent_id": agent.id, "agent_name": agent.name,
                        "message": f"{agent.name} has died."
                    })
                    if self._memory:
                        await self._memory.add_memory(agent.id, f"I died from survival struggles", importance=10)

    async def process_auto_revive(self, tick_count: int) -> None:
        with get_db_session() as db:
            config = db.query(GameConfig).first()
            if not config or not config.auto_revive_enabled:
                return
            delay = config.auto_revive_delay_ticks or 12
            dead = db.query(Agent).filter(Agent.status == "Dead").all()
            for agent in dead:
                if agent.death_tick and tick_count - agent.death_tick >= delay:
                    agent.status = "Alive"
                    agent.hp = config.revive_hp or 50
                    agent.energy = config.revive_energy or 50
                    agent.mood = 50
                    agent.is_sick = False
                    agent.current_action = "Idle"
                    agent.death_tick = None
                    await self._broadcast("auto_revive", {
                        "agent_id": agent.id, "agent_name": agent.name,
                        "message": f"{agent.name} has been revived."
                    })

    async def process_random_events(self, tick_count: int, get_inventory_fn, set_inventory_fn) -> None:
        if tick_count % 100 != 1:
            return
        if random.random() > 0.10:
            return
        events = list(RANDOM_EVENTS.keys())
        weights = [RANDOM_EVENTS[e]["weight"] for e in events]
        event_type = random.choices(events, weights=weights)[0]
        with get_db_session() as db:
            agents = db.query(Agent).filter(Agent.status == "Alive").all()
            if not agents:
                return
            world = db.query(WorldState).first()
            event_data = {"event_type": event_type, "message": ""}

            if event_type == "storm_damage":
                for agent in agents:
                    agent.hp = max(0, agent.hp - 15)
                if world:
                    world.tree_left_fruit = max(0, world.tree_left_fruit - 2)
                    world.tree_right_fruit = max(0, world.tree_right_fruit - 2)
                event_data["message"] = "A violent storm hits! Everyone injured, fruit damaged."

            elif event_type == "treasure_found":
                lucky = random.choice(agents)
                inv = get_inventory_fn(lucky)
                inv["medicine"] = inv.get("medicine", 0) + 2
                inv["herb"] = inv.get("herb", 0) + 3
                set_inventory_fn(lucky, inv)
                event_data["message"] = f"{lucky.name} found buried treasure!"
                event_data["agent_name"] = lucky.name

            elif event_type == "beast_attack":
                victim = random.choice(agents)
                victim.hp = max(0, victim.hp - 25)
                victim.mood = max(0, victim.mood - 20)
                event_data["message"] = f"A wild beast attacked {victim.name}!"
                event_data["agent_name"] = victim.name

            elif event_type == "rumor_spread":
                if len(agents) >= 2:
                    a1, a2 = random.sample(list(agents), 2)
                    a1.mood = max(0, a1.mood - 10)
                    a2.mood = max(0, a2.mood - 10)
                    event_data["message"] = f"Rumors spread about {a1.name} and {a2.name}..."

            await self._broadcast(EventType.RANDOM_EVENT, event_data)
            logger.info(f"Random event: {event_type}")
