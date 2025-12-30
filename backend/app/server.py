"""
WebSocket connection manager and server utilities.
Handles client connections, disconnections, and message broadcasting.
"""

import logging
from typing import Any
from fastapi import WebSocket

from .schemas import GameEvent

logger = logging.getLogger(__name__)


class ConnectionManager:
    """
    Manages WebSocket connections for real-time communication.

    Handles connection lifecycle and provides broadcast capabilities
    to all connected clients.
    """

    def __init__(self) -> None:
        """Initialize the connection manager with an empty connection list."""
        self._active_connections: list[WebSocket] = []

    @property
    def connection_count(self) -> int:
        """Return the number of active connections."""
        return len(self._active_connections)

    async def connect(self, websocket: WebSocket) -> None:
        """
        Accept and register a new WebSocket connection.

        Args:
            websocket: The WebSocket connection to accept
        """
        await websocket.accept()
        self._active_connections.append(websocket)
        logger.info(f"Client connected. Total connections: {self.connection_count}")

    def disconnect(self, websocket: WebSocket) -> None:
        """
        Remove a WebSocket connection from the active list.

        Args:
            websocket: The WebSocket connection to remove
        """
        if websocket in self._active_connections:
            self._active_connections.remove(websocket)
            logger.info(f"Client disconnected. Total connections: {self.connection_count}")

    async def broadcast(self, event: GameEvent) -> None:
        """
        Send a GameEvent to all connected clients.

        Args:
            event: The GameEvent to broadcast
        """
        if not self._active_connections:
            return

        message = event.model_dump_json()
        disconnected: list[WebSocket] = []

        for connection in self._active_connections:
            try:
                await connection.send_text(message)
            except Exception as e:
                logger.warning(f"Failed to send to client: {e}")
                disconnected.append(connection)

        # Clean up failed connections
        for conn in disconnected:
            self.disconnect(conn)

    async def send_personal(self, websocket: WebSocket, event: GameEvent) -> None:
        """
        Send a GameEvent to a specific client.

        Args:
            websocket: The target WebSocket connection
            event: The GameEvent to send
        """
        try:
            await websocket.send_text(event.model_dump_json())
        except Exception as e:
            logger.warning(f"Failed to send personal message: {e}")
            self.disconnect(websocket)
