"""
Twitch service for connecting to Twitch chat and handling events.
Integrates with the game engine to process chat commands and bits.

Compatible with twitchio 2.x (IRC-based)
"""

import os
import logging
from typing import TYPE_CHECKING

from twitchio.ext import commands

if TYPE_CHECKING:
    from .engine import GameEngine

logger = logging.getLogger(__name__)


class TwitchBot(commands.Bot):
    """
    Twitch bot that listens to chat messages and bits events.
    Forwards them to the game engine for processing.

    Compatible with twitchio 2.x API (IRC-based).
    """

    def __init__(self, game_engine: "GameEngine"):
        # Initialize bot with environment variables
        self._token = os.getenv("TWITCH_TOKEN")
        self._channel = os.getenv("TWITCH_CHANNEL_NAME")
        prefix = os.getenv("TWITCH_COMMAND_PREFIX", "!")

        if not self._token:
            raise ValueError("TWITCH_TOKEN environment variable is required")
        if not self._channel:
            raise ValueError("TWITCH_CHANNEL_NAME environment variable is required")

        # Store game engine reference
        self._game_engine = game_engine

        # Initialize the bot (twitchio 2.x API - IRC based)
        super().__init__(
            token=self._token,
            prefix=prefix,
            initial_channels=[self._channel]
        )

        logger.info(f"TwitchBot initialized for channel: {self._channel}")

    async def event_ready(self):
        """Called when the bot successfully connects to Twitch."""
        logger.info(f"Twitch Bot logged in as: {self.nick}")
        logger.info(f"Connected to channels: {[c.name for c in self.connected_channels]}")

    async def event_message(self, message):
        """Called when a message is received in chat."""
        # Ignore messages from the bot itself
        if message.echo:
            return

        # Handle commands first
        await self.handle_commands(message)

        # Extract user and message content
        author = message.author
        if author is None:
            return

        username = author.name
        content = message.content.strip()

        # Log the message for debugging
        logger.info(f"Twitch chat [{username}]: {content}")

        # Forward to game engine for command processing
        try:
            await self._game_engine.process_command(username, content)
        except Exception as e:
            logger.error(f"Error processing command: {e}")

        # Check for bits in the message tags
        if hasattr(message, 'tags') and message.tags:
            bits = message.tags.get('bits')
            if bits:
                try:
                    bits_amount = int(bits)
                    logger.info(f"Received {bits_amount} bits from {username}")
                    await self._game_engine.process_bits(username, bits_amount)

                    # Send special gift effect to Unity
                    await self._game_engine._broadcast_event("gift_effect", {
                        "type": "gift_effect",
                        "user": username,
                        "value": bits_amount,
                        "message": f"{username} cheered {bits_amount} bits!"
                    })
                except (ValueError, TypeError) as e:
                    logger.error(f"Error parsing bits amount: {e}")

    async def event_command_error(self, context, error):
        """Called when a command error occurs."""
        # Ignore command not found errors (most chat messages aren't commands)
        if isinstance(error, commands.CommandNotFound):
            return
        logger.error(f"Command error: {error}")

    async def event_error(self, error: Exception, data: str = None):
        """Called when an error occurs."""
        logger.error(f"Twitch bot error: {error}")
        if data:
            logger.debug(f"Error data: {data}")
