"""
Core Game Engine - The Island Survival Simulation.
Manages survival mechanics, agent states, and user interactions.
"""

import asyncio
import logging
import re
import time
from typing import TYPE_CHECKING

from .schemas import GameEvent, EventType
from .database import init_db, get_db_session
from .models import User, Agent, WorldState

if TYPE_CHECKING:
    from .server import ConnectionManager

logger = logging.getLogger(__name__)

# Command patterns
FEED_PATTERN = re.compile(r"feed\s+(\w+)", re.IGNORECASE)
CHECK_PATTERN = re.compile(r"(check|查询|状态)", re.IGNORECASE)

# Game constants
TICK_INTERVAL = 5.0          # Seconds between ticks
ENERGY_DECAY_PER_TICK = 2    # Energy lost per tick
HP_DECAY_WHEN_STARVING = 5   # HP lost when energy is 0
FEED_COST = 10               # Gold cost to feed an agent
FEED_ENERGY_RESTORE = 20     # Energy restored when fed
INITIAL_USER_GOLD = 100      # Starting gold for new users

# Initial NPC data
INITIAL_AGENTS = [
    {"name": "Jack", "personality": "勇敢"},
    {"name": "Luna", "personality": "狡猾"},
    {"name": "Bob", "personality": "老实"},
]


class GameEngine:
    """
    The core game engine for island survival simulation.
    Manages agents, users, and survival mechanics with database persistence.
    """

    def __init__(self, connection_manager: "ConnectionManager") -> None:
        """
        Initialize the game engine.

        Args:
            connection_manager: The WebSocket connection manager for broadcasting
        """
        self._manager = connection_manager
        self._running = False
        self._tick_count = 0
        self._tick_interval = TICK_INTERVAL

    @property
    def is_running(self) -> bool:
        """Check if the engine is currently running."""
        return self._running

    def _seed_initial_data(self) -> None:
        """Seed initial agents and world state if database is empty."""
        with get_db_session() as db:
            # Check if agents exist
            agent_count = db.query(Agent).count()
            if agent_count == 0:
                logger.info("Seeding initial agents...")
                for agent_data in INITIAL_AGENTS:
                    agent = Agent(
                        name=agent_data["name"],
                        personality=agent_data["personality"],
                        status="Alive",
                        hp=100,
                        energy=100
                    )
                    db.add(agent)
                logger.info(f"Created {len(INITIAL_AGENTS)} initial agents")

            # Check if world state exists
            world = db.query(WorldState).first()
            if world is None:
                logger.info("Seeding initial world state...")
                world = WorldState(day_count=1, weather="Sunny", resource_level=100)
                db.add(world)

    def _get_or_create_user(self, db, username: str) -> User:
        """Get existing user or create new one."""
        user = db.query(User).filter(User.username == username).first()
        if user is None:
            user = User(username=username, gold=INITIAL_USER_GOLD)
            db.add(user)
            db.flush()  # Get the ID without committing
            logger.info(f"New user registered: {username}")
        return user

    async def _broadcast_event(self, event_type: str, data: dict) -> None:
        """Create and broadcast a game event."""
        event = GameEvent(
            event_type=event_type,
            timestamp=time.time(),
            data=data
        )
        await self._manager.broadcast(event)

    async def _broadcast_agents_status(self) -> None:
        """Broadcast all agents' current status."""
        with get_db_session() as db:
            agents = db.query(Agent).all()
            agents_data = [agent.to_dict() for agent in agents]

        await self._broadcast_event(
            EventType.AGENTS_UPDATE,
            {"agents": agents_data}
        )

    async def _process_survival_tick(self) -> None:
        """
        Process survival mechanics for all alive agents.
        - Decrease energy
        - If energy <= 0, decrease HP
        - If HP <= 0, mark as Dead
        """
        deaths = []

        with get_db_session() as db:
            alive_agents = db.query(Agent).filter(Agent.status == "Alive").all()

            for agent in alive_agents:
                # Energy decay
                agent.energy = max(0, agent.energy - ENERGY_DECAY_PER_TICK)

                # Starvation damage
                if agent.energy <= 0:
                    agent.hp = max(0, agent.hp - HP_DECAY_WHEN_STARVING)

                # Check for death
                if agent.hp <= 0:
                    agent.status = "Dead"
                    deaths.append({
                        "name": agent.name,
                        "personality": agent.personality
                    })
                    logger.info(f"Agent {agent.name} has died!")

        # Broadcast death events
        for death in deaths:
            await self._broadcast_event(
                EventType.AGENT_DIED,
                {
                    "agent_name": death["name"],
                    "message": f"💀 {death['name']}（{death['personality']}）因饥饿而死亡..."
                }
            )

    async def _handle_feed(self, username: str, agent_name: str) -> None:
        """
        Handle feed command.

        Args:
            username: The user feeding the agent
            agent_name: Name of the agent to feed
        """
        # Variables to store data for broadcasting (outside session)
        feed_result = None

        with get_db_session() as db:
            user = self._get_or_create_user(db, username)

            # Find the agent
            agent = db.query(Agent).filter(
                Agent.name.ilike(agent_name)
            ).first()

            if agent is None:
                await self._broadcast_event(
                    EventType.ERROR,
                    {"message": f"找不到名为 {agent_name} 的角色"}
                )
                return

            if agent.status != "Alive":
                await self._broadcast_event(
                    EventType.ERROR,
                    {"message": f"{agent.name} 已经死亡，无法投喂"}
                )
                return

            if user.gold < FEED_COST:
                await self._broadcast_event(
                    EventType.ERROR,
                    {
                        "user": username,
                        "message": f"金币不足！需要 {FEED_COST} 金币，当前只有 {user.gold} 金币"
                    }
                )
                return

            # Perform feed
            user.gold -= FEED_COST
            old_energy = agent.energy
            agent.energy = min(100, agent.energy + FEED_ENERGY_RESTORE)
            actual_restore = agent.energy - old_energy

            # Store data for broadcasting before session closes
            feed_result = {
                "agent_name": agent.name,
                "actual_restore": actual_restore,
                "agent_energy": agent.energy,
                "user_gold": user.gold
            }

        # Broadcast outside of session
        if feed_result:
            await self._broadcast_event(
                EventType.FEED,
                {
                    "user": username,
                    "agent_name": feed_result["agent_name"],
                    "energy_restored": feed_result["actual_restore"],
                    "agent_energy": feed_result["agent_energy"],
                    "user_gold": feed_result["user_gold"],
                    "message": f"🍖 {username} 投喂了 {feed_result['agent_name']}！"
                              f"恢复 {feed_result['actual_restore']} 点体力（当前: {feed_result['agent_energy']}/100）"
                }
            )

            await self._broadcast_event(
                EventType.USER_UPDATE,
                {
                    "user": username,
                    "gold": feed_result["user_gold"]
                }
            )

    async def _handle_check(self, username: str) -> None:
        """Handle check/status command."""
        with get_db_session() as db:
            user = self._get_or_create_user(db, username)
            agents = db.query(Agent).all()
            world = db.query(WorldState).first()

            # Extract data before session closes
            user_data = {"username": user.username, "gold": user.gold}
            agents_data = [agent.to_dict() for agent in agents]
            world_data = world.to_dict() if world else {}
            message = f"📊 {username} 的状态 - 金币: {user_data['gold']}"

        await self._broadcast_event(
            EventType.CHECK,
            {
                "user": user_data,
                "agents": agents_data,
                "world": world_data,
                "message": message
            }
        )

    async def process_comment(self, user: str, message: str) -> None:
        """
        Process a comment through command matching.

        Args:
            user: Username of the commenter
            message: The comment text
        """
        # Broadcast the incoming comment
        await self._broadcast_event(
            EventType.COMMENT,
            {"user": user, "message": message}
        )

        # Match commands
        feed_match = FEED_PATTERN.search(message)
        if feed_match:
            agent_name = feed_match.group(1)
            await self._handle_feed(user, agent_name)
            return

        if CHECK_PATTERN.search(message):
            await self._handle_check(user)
            return

        # No command matched - treat as regular chat

    async def _game_loop(self) -> None:
        """
        The main game loop - survival simulation.

        Every tick:
        1. Process survival mechanics (energy/HP decay)
        2. Broadcast agent states
        """
        logger.info("Game loop started - Island survival simulation")

        # Initial broadcast
        await self._broadcast_agents_status()

        while self._running:
            self._tick_count += 1

            # Process survival mechanics
            await self._process_survival_tick()

            # Broadcast current state
            await self._broadcast_agents_status()

            # Broadcast tick event
            with get_db_session() as db:
                alive_count = db.query(Agent).filter(Agent.status == "Alive").count()
                world = db.query(WorldState).first()
                day = world.day_count if world else 1

            await self._broadcast_event(
                EventType.TICK,
                {
                    "tick": self._tick_count,
                    "day": day,
                    "alive_agents": alive_count
                }
            )

            logger.debug(f"Tick {self._tick_count}: {alive_count} agents alive")

            await asyncio.sleep(self._tick_interval)

        logger.info("Game loop stopped")

    async def start(self) -> None:
        """Start the game engine."""
        if self._running:
            logger.warning("Engine already running")
            return

        # Initialize database and seed data
        logger.info("Initializing database...")
        init_db()
        self._seed_initial_data()

        self._running = True
        asyncio.create_task(self._game_loop())
        logger.info("Game engine started - The Island awaits...")

    async def stop(self) -> None:
        """Stop the game engine."""
        self._running = False
        logger.info("Game engine stopping...")
