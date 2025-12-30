"""
Core Game Engine - The "Heartbeat" of the game.
Runs the main game loop and coordinates between comments and agents.
"""

import asyncio
import logging
import random
import time
from typing import TYPE_CHECKING

from .schemas import GameEvent, EventType
from .agents import BaseAgent, RuleBasedAgent

if TYPE_CHECKING:
    from .server import ConnectionManager

logger = logging.getLogger(__name__)


class GameEngine:
    """
    The core game engine that runs the main game loop.

    Simulates live comments, processes them through agents,
    and broadcasts responses to connected clients.
    """

    def __init__(self, connection_manager: "ConnectionManager") -> None:
        """
        Initialize the game engine.

        Args:
            connection_manager: The WebSocket connection manager for broadcasting
        """
        self._manager = connection_manager
        self._agent: BaseAgent = RuleBasedAgent(name="Guardian")
        self._running = False
        self._tick_count = 0
        self._tick_interval = 2.0  # seconds

        # Mock comment templates
        self._mock_users = ["User123", "GamerPro", "DragonSlayer", "NightOwl", "StarGazer"]
        self._mock_actions = ["Attack!", "Heal me!", "Run away!", "Help!", "Fire spell!", "Magic blast!"]

    @property
    def is_running(self) -> bool:
        """Check if the engine is currently running."""
        return self._running

    def _generate_mock_comment(self) -> tuple[str, str]:
        """
        Generate a mock live comment.

        Returns:
            Tuple of (username, comment_text)
        """
        user = random.choice(self._mock_users)
        action = random.choice(self._mock_actions)
        return user, action

    async def _broadcast_event(self, event_type: str, data: dict) -> None:
        """
        Create and broadcast a game event.

        Args:
            event_type: Type of the event
            data: Event payload data
        """
        event = GameEvent(
            event_type=event_type,
            timestamp=time.time(),
            data=data
        )
        await self._manager.broadcast(event)

    async def process_comment(self, user: str, message: str) -> None:
        """
        Process a comment (real or mock) through the agent pipeline.

        Args:
            user: Username of the commenter
            message: The comment text
        """
        # Broadcast the incoming comment
        await self._broadcast_event(
            EventType.COMMENT,
            {"user": user, "message": message}
        )

        # Process through agent
        full_comment = f"{user}: {message}"
        response = self._agent.process_input(full_comment)

        # Broadcast agent response
        await self._broadcast_event(
            EventType.AGENT_RESPONSE,
            {"agent": self._agent.name, "response": response}
        )

    async def _game_loop(self) -> None:
        """
        The main game loop - runs continuously while engine is active.

        Every tick:
        1. Simulates a mock live comment
        2. Passes it to the agent
        3. Broadcasts the response
        """
        logger.info("Game loop started")

        while self._running:
            self._tick_count += 1

            # Broadcast tick event
            await self._broadcast_event(
                EventType.TICK,
                {"tick": self._tick_count}
            )

            # Generate and process mock comment
            user, message = self._generate_mock_comment()
            logger.info(f"Tick {self._tick_count}: {user} says '{message}'")

            await self.process_comment(user, message)

            # Wait for next tick
            await asyncio.sleep(self._tick_interval)

        logger.info("Game loop stopped")

    async def start(self) -> None:
        """Start the game engine loop as a background task."""
        if self._running:
            logger.warning("Engine already running")
            return

        self._running = True
        asyncio.create_task(self._game_loop())
        logger.info("Game engine started")

    async def stop(self) -> None:
        """Stop the game engine loop."""
        self._running = False
        logger.info("Game engine stopping...")
