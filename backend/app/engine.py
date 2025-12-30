"""
Core Game Engine - The "Heartbeat" of the game.
Manages game state, processes commands, and coordinates broadcasts.
"""

import asyncio
import logging
import re
import time
from typing import TYPE_CHECKING

from .schemas import GameEvent, EventType
from .models import Player, Boss

if TYPE_CHECKING:
    from .server import ConnectionManager

logger = logging.getLogger(__name__)

# Command patterns for string matching
ATTACK_PATTERN = re.compile(r"(attack|攻击|打|砍|杀)", re.IGNORECASE)
HEAL_PATTERN = re.compile(r"(heal|治疗|回血|加血|恢复)", re.IGNORECASE)
STATUS_PATTERN = re.compile(r"(status|查询|状态|信息)", re.IGNORECASE)

# Game constants
ATTACK_DAMAGE = 10
ATTACK_GOLD_REWARD = 10
HEAL_AMOUNT = 10
BOSS_COUNTER_DAMAGE = 15  # Boss反击伤害


class GameEngine:
    """
    The core game engine that manages RPG state and game loop.

    Manages players, boss, and processes commands through string matching.
    """

    def __init__(self, connection_manager: "ConnectionManager") -> None:
        """
        Initialize the game engine with state storage.

        Args:
            connection_manager: The WebSocket connection manager for broadcasting
        """
        self._manager = connection_manager
        self._running = False
        self._tick_count = 0
        self._tick_interval = 2.0  # seconds

        # Game state
        self.players: dict[str, Player] = {}
        self.boss = Boss(name="Dragon", hp=1000, max_hp=1000)

    @property
    def is_running(self) -> bool:
        """Check if the engine is currently running."""
        return self._running

    def _get_or_create_player(self, username: str) -> Player:
        """
        Get existing player or create new one.

        Args:
            username: The player's username

        Returns:
            Player instance
        """
        if username not in self.players:
            self.players[username] = Player(name=username)
            logger.info(f"New player registered: {username}")
        return self.players[username]

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

    async def _broadcast_boss_update(self) -> None:
        """Broadcast current boss status to all clients."""
        await self._broadcast_event(
            EventType.BOSS_UPDATE,
            {
                "boss_name": self.boss.name,
                "boss_hp": self.boss.hp,
                "boss_max_hp": self.boss.max_hp,
                "boss_hp_percentage": self.boss.hp_percentage
            }
        )

    async def _handle_attack(self, player: Player) -> None:
        """
        Handle attack command. Boss will counter-attack!

        Args:
            player: The attacking player
        """
        if not self.boss.is_alive:
            await self._broadcast_event(
                EventType.SYSTEM,
                {"message": f"Boss is already defeated! Waiting for respawn..."}
            )
            return

        # Player attacks boss
        damage = self.boss.take_damage(ATTACK_DAMAGE)
        player.add_gold(ATTACK_GOLD_REWARD)

        # Boss counter-attacks player
        counter_damage = player.take_damage(BOSS_COUNTER_DAMAGE)

        await self._broadcast_event(
            EventType.ATTACK,
            {
                "user": player.name,
                "damage": damage,
                "counter_damage": counter_damage,
                "gold_earned": ATTACK_GOLD_REWARD,
                "boss_hp": self.boss.hp,
                "boss_max_hp": self.boss.max_hp,
                "player_hp": player.hp,
                "player_max_hp": player.max_hp,
                "player_gold": player.gold,
                "message": f"⚔️ {player.name} attacked {self.boss.name}! "
                          f"Dealt {damage} dmg, received {counter_damage} counter-attack. "
                          f"HP: {player.hp}/{player.max_hp} | Boss: {self.boss.hp}/{self.boss.max_hp}"
            }
        )

        await self._broadcast_boss_update()

        # Check if player died
        if not player.is_alive:
            await self._handle_player_death(player)
            return

        # Check if boss defeated
        if not self.boss.is_alive:
            await self._handle_boss_defeated()

    async def _handle_player_death(self, player: Player) -> None:
        """Handle player death - respawn with full HP but lose half gold."""
        lost_gold = player.gold // 2
        player.gold -= lost_gold
        player.hp = player.max_hp  # Respawn with full HP

        await self._broadcast_event(
            EventType.SYSTEM,
            {
                "user": player.name,
                "player_hp": player.hp,
                "player_max_hp": player.max_hp,
                "player_gold": player.gold,
                "message": f"💀 {player.name} was slain by {self.boss.name}! "
                          f"Lost {lost_gold} gold. Respawned with full HP."
            }
        )

    async def _handle_boss_defeated(self) -> None:
        """Handle boss defeat and reset."""
        await self._broadcast_event(
            EventType.BOSS_DEFEATED,
            {
                "boss_name": self.boss.name,
                "message": f"🎉 {self.boss.name} has been defeated! A new boss will spawn soon..."
            }
        )

        # Reset boss after short delay
        await asyncio.sleep(3.0)
        self.boss.reset()
        await self._broadcast_event(
            EventType.SYSTEM,
            {"message": f"⚔️ {self.boss.name} has respawned with full HP!"}
        )
        await self._broadcast_boss_update()

    async def _handle_heal(self, player: Player) -> None:
        """
        Handle heal command.

        Args:
            player: The healing player
        """
        healed = player.heal(HEAL_AMOUNT)

        await self._broadcast_event(
            EventType.HEAL,
            {
                "user": player.name,
                "healed": healed,
                "player_hp": player.hp,
                "player_max_hp": player.max_hp,
                "message": f"{player.name} healed themselves. "
                          f"Restored {healed} HP. HP: {player.hp}/{player.max_hp}"
            }
        )

    async def _handle_status(self, player: Player) -> None:
        """
        Handle status query command.

        Args:
            player: The querying player
        """
        await self._broadcast_event(
            EventType.STATUS,
            {
                "user": player.name,
                "player_hp": player.hp,
                "player_max_hp": player.max_hp,
                "player_gold": player.gold,
                "boss_name": self.boss.name,
                "boss_hp": self.boss.hp,
                "boss_max_hp": self.boss.max_hp,
                "message": f"📊 {player.name}'s Status - HP: {player.hp}/{player.max_hp}, "
                          f"Gold: {player.gold} | Boss {self.boss.name}: {self.boss.hp}/{self.boss.max_hp}"
            }
        )

    async def process_comment(self, user: str, message: str) -> None:
        """
        Process a comment through string matching command system.

        Args:
            user: Username of the commenter
            message: The comment text
        """
        # Get or create player
        player = self._get_or_create_player(user)

        # Broadcast the incoming comment first
        await self._broadcast_event(
            EventType.COMMENT,
            {"user": user, "message": message}
        )

        # Process command through string matching
        if ATTACK_PATTERN.search(message):
            await self._handle_attack(player)
        elif HEAL_PATTERN.search(message):
            await self._handle_heal(player)
        elif STATUS_PATTERN.search(message):
            await self._handle_status(player)
        # If no command matched, treat as regular chat (no action needed)

    async def _game_loop(self) -> None:
        """
        The main game loop - runs continuously while engine is active.

        Every tick broadcasts current game state.
        """
        logger.info("Game loop started")

        # Broadcast initial boss state
        await self._broadcast_boss_update()

        while self._running:
            self._tick_count += 1

            # Broadcast tick event with game state
            await self._broadcast_event(
                EventType.TICK,
                {
                    "tick": self._tick_count,
                    "boss_hp": self.boss.hp,
                    "boss_max_hp": self.boss.max_hp,
                    "player_count": len(self.players)
                }
            )

            logger.debug(f"Tick {self._tick_count}: Boss HP {self.boss.hp}/{self.boss.max_hp}")

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
