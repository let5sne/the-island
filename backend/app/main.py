"""
FastAPI entry point for the interactive live-stream game backend.
Configures the application, WebSocket routes, and lifecycle events.
"""

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware

from .server import ConnectionManager
from .engine import GameEngine
from .schemas import GameEvent, ClientMessage, EventType

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

# Global instances
manager = ConnectionManager()
engine = GameEngine(manager)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Application lifespan manager.
    Starts the game engine on startup and stops it on shutdown.
    """
    logger.info("Starting application...")
    await engine.start()
    yield
    logger.info("Shutting down application...")
    await engine.stop()


# Create FastAPI application
app = FastAPI(
    title="The Island - Live Stream Game Backend",
    description="Commercial-grade interactive live-stream game backend",
    version="0.1.0",
    lifespan=lifespan
)

# Configure CORS for frontend access
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/")
async def root():
    """Health check endpoint."""
    return {
        "status": "running",
        "service": "The Island Game Backend",
        "connections": manager.connection_count,
        "engine_running": engine.is_running
    }


@app.websocket("/ws")
async def websocket_endpoint(websocket: WebSocket):
    """
    WebSocket endpoint for real-time game communication.

    Handles client connections and processes incoming messages.
    """
    await manager.connect(websocket)

    # Send welcome message
    welcome = GameEvent(
        event_type=EventType.SYSTEM,
        data={"message": "Connected to The Island!"}
    )
    await manager.send_personal(websocket, welcome)

    try:
        while True:
            # Receive and parse client message
            data = await websocket.receive_json()
            message = ClientMessage(**data)

            # Handle mock comment action
            if message.action == "send_comment":
                user = message.payload.get("user", "Anonymous")
                text = message.payload.get("message", "")
                await engine.process_comment(user, text)

    except WebSocketDisconnect:
        manager.disconnect(websocket)
    except Exception as e:
        logger.error(f"WebSocket error: {e}")
        manager.disconnect(websocket)
