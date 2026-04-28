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
        self._user_connections: dict[str, WebSocket] = {}  # username -> websocket

    @property
    def connection_count(self) -> int:
        """Return the number of active connections."""
        return len(self._active_connections)

    async def connect(self, websocket: WebSocket, username: str | None = None) -> None:
        """Accept and register a new WebSocket connection."""
        await websocket.accept()
        self._active_connections.append(websocket)
        if username:
            self._user_connections[username] = websocket
        logger.info(f"Client connected. Total connections: {self.connection_count}")

    def disconnect(self, websocket: WebSocket) -> None:
        """Remove a WebSocket connection from the active list."""
        if websocket in self._active_connections:
            self._active_connections.remove(websocket)
        # Clean user mapping
        for user, ws in list(self._user_connections.items()):
            if ws == websocket:
                del self._user_connections[user]
                break
        logger.info(f"Client disconnected. Total connections: {self.connection_count}")

    async def broadcast(self, event: GameEvent, private_to: str | None = None) -> None:
        """Send a GameEvent to all connected clients, or privately to one user."""
        message = event.model_dump_json()
        disconnected: list[WebSocket] = []

        if private_to and private_to in self._user_connections:
            try:
                await self._user_connections[private_to].send_text(message)
                return
            except Exception as e:
                logger.warning(f"Failed to send to {private_to}: {e}")

        if not self._active_connections:
            return

        for connection in self._active_connections:
            try:
                await connection.send_text(message)
            except Exception as e:
                logger.warning(f"Failed to send: {e}")
                disconnected.append(connection)

        for conn in disconnected:
            self.disconnect(conn)

    async def send_personal(self, websocket: WebSocket, event: GameEvent) -> None:
        """Send a GameEvent to a specific WebSocket client."""
        try:
            await websocket.send_text(event.model_dump_json())
        except Exception as e:
            logger.warning(f"Failed to send personal: {e}")
            self.disconnect(websocket)
