"""User command processing for The Island game.

Handles all player commands (feed, heal, encourage, love, talk, revive,
check, reset) from Twitch and Unity clients.
"""

import logging

from .config import (
    FEED_COST, FEED_ENERGY_RESTORE, HEAL_COST, HEAL_HP_RESTORE,
    ENCOURAGE_COST, ENCOURAGE_MOOD_BOOST, LOVE_COST, LOVE_MOOD_BOOST,
    REVIVE_COST, INITIAL_USER_GOLD, BUILD_COST, BUILDING_TYPES,
    FEED_PATTERN, CHECK_PATTERN, RESET_PATTERN, HEAL_PATTERN,
    TALK_PATTERN, ENCOURAGE_PATTERN, LOVE_PATTERN, REVIVE_PATTERN,
    BUILD_PATTERN, TRADE_PATTERN,
)
from .database import get_db_session
from .models import User, Agent, WorldState, GameConfig, Building
from .schemas import GameEvent, EventType

logger = logging.getLogger(__name__)


class CommandHandler:
    """Dispatches user commands to the appropriate handler."""

    def __init__(
        self,
        broadcast_callback,
        broadcast_vfx_callback,
        trigger_agent_speak_callback,
        llm_service=None,
        memory_service=None,
    ):
        self._broadcast = broadcast_callback
        self._broadcast_vfx = broadcast_vfx_callback
        self._agent_speak = trigger_agent_speak_callback
        self._llm = llm_service
        self._memory = memory_service

    async def handle(self, user: str, message: str) -> None:
        """Parse and dispatch a command message."""
        await self._broadcast(EventType.COMMENT, {"user": user, "message": message})

        if match := FEED_PATTERN.search(message):
            await self._feed(user, match.group(1))
        elif match := HEAL_PATTERN.search(message):
            await self._heal(user, match.group(1))
        elif match := TALK_PATTERN.search(message):
            topic = match.group(2) or ""
            await self._talk(user, match.group(1), topic.strip())
        elif match := ENCOURAGE_PATTERN.search(message):
            await self._encourage(user, match.group(1))
        elif match := LOVE_PATTERN.search(message):
            await self._love(user, match.group(1))
        elif match := REVIVE_PATTERN.search(message):
            await self._revive(user, match.group(1))
        elif CHECK_PATTERN.search(message):
            await self._check(user)
        elif RESET_PATTERN.search(message):
            await self._reset(user)
        elif match := BUILD_PATTERN.search(message):
            await self._build(user, match.group(1))
        elif match := TRADE_PATTERN.search(message):
            await self._trade(user, match.group(1), match.group(2), int(match.group(3)))

    async def _feed(self, username: str, agent_name: str) -> None:
        """Feed an agent: cost gold, restore energy."""
        with get_db_session() as db:
            user = db.query(User).filter(User.username == username).first()
            if not user:
                user = User(username=username, gold=INITIAL_USER_GOLD)
                db.add(user)
                db.flush()

            if user.gold < FEED_COST:
                await self._broadcast(EventType.ERROR, {
                    "user": username,
                    "message": f"Not enough gold! Need {FEED_COST}g, have {user.gold}g."
                })
                return

            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()
            if not agent:
                await self._broadcast(EventType.ERROR, {
                    "user": username,
                    "message": f"Agent '{agent_name}' not found."
                })
                return

            if not agent.is_alive:
                await self._broadcast(EventType.ERROR, {
                    "user": username,
                    "message": f"{agent.name} is dead and cannot eat."
                })
                return

            user.gold -= FEED_COST
            agent.energy = min(100, agent.energy + FEED_ENERGY_RESTORE)
            agent.hp = min(100, agent.hp + 5)

            await self._broadcast(EventType.FEED, {
                "user": username,
                "agent_name": agent.name,
                "cost": FEED_COST,
                "remaining_gold": user.gold,
                "energy_restored": FEED_ENERGY_RESTORE,
                "message": f"{username} fed {agent.name} (+{FEED_ENERGY_RESTORE} Energy, +5 HP)"
            })

            await self._broadcast_vfx("food_poof", agent.id, f"{username} feeds {agent.name}")

            if self._agent_speak:
                await self._agent_speak(
                    agent.id, agent.name, agent.personality, agent.hp, agent.energy, agent.mood,
                    f"{username} just fed them.", "feed"
                )

    async def _heal(self, username: str, agent_name: str) -> None:
        """Heal an agent: cost gold, restore HP, cure sickness."""
        with get_db_session() as db:
            user = db.query(User).filter(User.username == username).first()
            if not user:
                user = User(username=username, gold=INITIAL_USER_GOLD)
                db.add(user)
                db.flush()

            if user.gold < HEAL_COST:
                await self._broadcast(EventType.ERROR, {
                    "user": username,
                    "message": f"Not enough gold! Need {HEAL_COST}g, have {user.gold}g."
                })
                return

            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()
            if not agent:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"Agent '{agent_name}' not found."
                })
                return

            if not agent.is_alive:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"{agent.name} is dead and cannot be healed."
                })
                return

            user.gold -= HEAL_COST
            agent.hp = min(100, agent.hp + HEAL_HP_RESTORE)
            was_sick = agent.is_sick
            agent.is_sick = False

            await self._broadcast(EventType.HEAL, {
                "user": username, "agent_name": agent.name, "cost": HEAL_COST,
                "remaining_gold": user.gold, "hp_restored": HEAL_HP_RESTORE,
                "cured_sickness": was_sick,
                "message": f"{username} healed {agent.name} (+{HEAL_HP_RESTORE} HP)"
            })

            await self._broadcast_vfx("heal_glow", agent.id, f"{username} heals {agent.name}")

            if self._agent_speak:
                await self._agent_speak(
                    agent.id, agent.name, agent.personality, agent.hp, agent.energy, agent.mood,
                    f"{username} just healed them.", "heal"
                )

    async def _encourage(self, username: str, agent_name: str) -> None:
        """Encourage an agent: cost gold, boost mood."""
        with get_db_session() as db:
            user = db.query(User).filter(User.username == username).first()
            if not user:
                user = User(username=username, gold=INITIAL_USER_GOLD)
                db.add(user)
                db.flush()

            if user.gold < ENCOURAGE_COST:
                await self._broadcast(EventType.ERROR, {
                    "user": username,
                    "message": f"Not enough gold! Need {ENCOURAGE_COST}g, have {user.gold}g."
                })
                return

            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()
            if not agent:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"Agent '{agent_name}' not found."
                })
                return

            if not agent.is_alive:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"You cannot encourage {agent.name}, they are dead."
                })
                return

            user.gold -= ENCOURAGE_COST
            agent.mood = min(100, agent.mood + ENCOURAGE_MOOD_BOOST)

            await self._broadcast(EventType.ENCOURAGE, {
                "user": username, "agent_name": agent.name, "cost": ENCOURAGE_COST,
                "remaining_gold": user.gold, "mood_boost": ENCOURAGE_MOOD_BOOST,
                "message": f"{username} encouraged {agent.name} (+{ENCOURAGE_MOOD_BOOST} Mood)"
            })

            if self._agent_speak:
                await self._agent_speak(
                    agent.id, agent.name, agent.personality, agent.hp, agent.energy, agent.mood,
                    f"{username} just encouraged them.", "encourage"
                )

    async def _love(self, username: str, agent_name: str) -> None:
        """Send love to an agent: cost gold, VFX only."""
        with get_db_session() as db:
            user = db.query(User).filter(User.username == username).first()
            if not user:
                user = User(username=username, gold=INITIAL_USER_GOLD)
                db.add(user)
                db.flush()

            if user.gold < LOVE_COST:
                await self._broadcast(EventType.ERROR, {
                    "user": username,
                    "message": f"Not enough gold! Need {LOVE_COST}g, have {user.gold}g."
                })
                return

            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()
            if not agent:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"Agent '{agent_name}' not found."
                })
                return

            if not agent.is_alive:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"You cannot show love to {agent.name}, they are dead."
                })
                return

            user.gold -= LOVE_COST
            agent.mood = min(100, agent.mood + LOVE_MOOD_BOOST)
            await self._broadcast_vfx("heart_explosion", agent.id, f"{username} shows love to {agent.name}")

    async def _talk(self, username: str, agent_name: str, topic: str = "") -> None:
        """Talk to an agent: free, generates LLM response."""
        with get_db_session() as db:
            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()
            if not agent:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"Agent '{agent_name}' not found."
                })
                return

            if not agent.is_alive:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"You cannot talk to {agent.name}, they are dead."
                })
                return

            if self._llm:
                text = await self._llm.generate_conversation_response(
                    agent.name, agent.personality, agent.mood_state or "neutral", username, topic
                )
            else:
                text = f"(No LLM available) {agent.name} looks at you silently."

            await self._broadcast(EventType.TALK, {
                "user": username, "agent_name": agent.name, "topic": topic, "response": text
            })

            await self._broadcast(EventType.AGENT_SPEAK, {
                "agent_id": agent.id, "agent_name": agent.name, "text": text
            })

    async def _revive(self, username: str, agent_name: str) -> None:
        """Revive a dead agent: cost gold."""
        with get_db_session() as db:
            user = db.query(User).filter(User.username == username).first()
            if not user:
                user = User(username=username, gold=INITIAL_USER_GOLD)
                db.add(user)
                db.flush()

            if user.gold < REVIVE_COST:
                await self._broadcast(EventType.ERROR, {
                    "user": username,
                    "message": f"Not enough gold! Need {REVIVE_COST}g, have {user.gold}g."
                })
                return

            agent = db.query(Agent).filter(Agent.name.ilike(agent_name)).first()
            if not agent:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"Agent '{agent_name}' not found."
                })
                return

            if agent.is_alive:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"{agent.name} is still alive!"
                })
                return

            user.gold -= REVIVE_COST
            agent.status = "Alive"
            agent.hp = 50
            agent.energy = 50
            agent.mood = 50
            agent.is_sick = False
            agent.death_tick = None

            await self._broadcast(EventType.REVIVE, {
                "user": username, "agent_name": agent.name, "cost": REVIVE_COST,
                "remaining_gold": user.gold,
                "message": f"{username} revived {agent.name}!"
            })

    async def _check(self, username: str) -> None:
        """Show all agent status."""
        with get_db_session() as db:
            user = db.query(User).filter(User.username == username).first()
            gold = user.gold if user else INITIAL_USER_GOLD
            agents = db.query(Agent).all()
            world = db.query(WorldState).first()

            status_lines = []
            for a in agents:
                sick_tag = " [SICK]" if a.is_sick else ""
                role_tag = f" ({a.social_role})" if a.social_role and a.social_role != "neutral" else ""
                status_lines.append(
                    f"{a.name} ({a.personality}): HP={a.hp} Energy={a.energy} Mood={a.mood}{sick_tag}{role_tag} [{a.status}]"
                )

            day_info = f"Day {world.day_count}, {world.time_of_day}, Weather: {world.weather}" if world else "N/A"

            await self._broadcast(EventType.CHECK, {
                "user": username, "gold": gold, "day_info": day_info,
                "agents": [a.to_dict() for a in agents],
                "message": "\n".join(status_lines) if status_lines else "No agents."
            })

    async def _reset(self, username: str) -> None:
        """Reset all agents to initial state."""
        with get_db_session() as db:
            agents = db.query(Agent).all()
            for a in agents:
                a.status = "Alive"
                a.hp = 100
                a.energy = 100
                a.mood = 70
                a.is_sick = False
                a.death_tick = None
                a.current_action = "Idle"
                a.location = "center"
                a.social_role = "neutral"

            world = db.query(WorldState).first()
            if world:
                world.day_count = 1
                world.weather = "Sunny"
                world.time_of_day = "day"
                world.current_tick_in_day = 0
                world.tree_left_fruit = 5
                world.tree_right_fruit = 5

        await self._broadcast(EventType.SYSTEM, {
            "message": f"{username} reset the game!"
        })

    async def _build(self, username: str, building_type: str) -> None:
        """Build a structure on the island."""
        building_type = building_type.lower()
        if building_type not in BUILDING_TYPES:
            await self._broadcast(EventType.ERROR, {
                "user": username,
                "message": f"Unknown building type '{building_type}'. Available: {', '.join(BUILDING_TYPES.keys())}"
            })
            return

        bt = BUILDING_TYPES[building_type]
        with get_db_session() as db:
            user = db.query(User).filter(User.username == username).first()
            if not user:
                user = User(username=username, gold=INITIAL_USER_GOLD)
                db.add(user)
                db.flush()

            cost = bt["cost"]
            if user.gold < cost:
                await self._broadcast(EventType.ERROR, {
                    "user": username,
                    "message": f"Not enough gold! Need {cost}g, have {user.gold}g."
                })
                return

            user.gold -= cost
            building = Building(
                building_type=building_type,
                name=bt["name"],
                description=bt["description"],
                built_by=username,
                is_complete=False,
                construction_progress=0,
            )
            db.add(building)
            db.flush()

            await self._broadcast(EventType.SYSTEM, {
                "message": f"{username} started building a {bt['name']}! ({cost}g). Construction will complete over time.",
            })
            await self._broadcast("building_started", {
                "building_id": building.id,
                "building_type": building_type,
                "name": bt["name"],
                "built_by": username,
                "construction_ticks": bt["construction_ticks"],
            })

    async def _trade(self, username: str, target_name: str, item: str, quantity: int) -> None:
        """Player-initiated trade between agents."""
        import json

        if quantity <= 0:
            await self._broadcast(EventType.ERROR, {
                "user": username, "message": "Trade quantity must be positive."
            })
            return

        with get_db_session() as db:
            from_agent = db.query(Agent).filter(
                Agent.name.ilike(username), Agent.status == "Alive"
            ).first()
            to_agent = db.query(Agent).filter(
                Agent.name.ilike(target_name), Agent.status == "Alive"
            ).first()

            if not from_agent:
                await self._broadcast(EventType.ERROR, {
                    "user": username,
                    "message": f"No alive agent named '{username}'. Use 'check' to see agents."
                })
                return

            if not to_agent:
                await self._broadcast(EventType.ERROR, {
                    "user": username, "message": f"Agent '{target_name}' not found or dead."
                })
                return

            from_inv = json.loads(from_agent.inventory) if from_agent.inventory else {}
            to_inv = json.loads(to_agent.inventory) if to_agent.inventory else {}

            if from_inv.get(item, 0) < quantity:
                await self._broadcast(EventType.ERROR, {
                    "user": username,
                    "message": f"{from_agent.name} doesn't have enough {item} (has {from_inv.get(item, 0)})."
                })
                return

            from_inv[item] -= quantity
            to_inv[item] = to_inv.get(item, 0) + quantity
            from_agent.inventory = json.dumps(from_inv)
            to_agent.inventory = json.dumps(to_inv)

            from_agent.mood = min(100, from_agent.mood + 3)
            to_agent.mood = min(100, to_agent.mood + 5)

            await self._broadcast("give_item", {
                "from_name": from_agent.name,
                "to_name": to_agent.name,
                "item": item,
                "quantity": quantity,
                "message": f"{from_agent.name} traded {quantity}x {item} to {to_agent.name}!",
            })
