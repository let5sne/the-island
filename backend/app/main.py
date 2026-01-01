"""
FastAPI entry point for the interactive live-stream game backend.
Configures the application, WebSocket routes, and lifecycle events.
"""

# Load .env file before any other imports
from dotenv import load_dotenv
load_dotenv()

import asyncio
import logging
import os
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from fastapi.responses import FileResponse

from .server import ConnectionManager
from .engine import GameEngine
from .schemas import GameEvent, ClientMessage, EventType
from .twitch_service import TwitchBot

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

# Global instances
manager = ConnectionManager()
engine = GameEngine(manager)

# Frontend path
FRONTEND_DIR = Path(__file__).parent.parent.parent / "frontend"


@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Application lifespan manager.
    Starts the game engine and Twitch bot on startup, stops them on shutdown.
    """
    logger.info("Starting application...")
    
    # Start game engine
    await engine.start()
    
    # Start Twitch bot if credentials are provided
    twitch_bot = None
    if os.getenv("TWITCH_TOKEN") and os.getenv("TWITCH_CHANNEL_NAME"):
        try:
            twitch_bot = TwitchBot(engine)
            # Start bot in background task
            asyncio.create_task(twitch_bot.start())
            logger.info("Twitch bot started in background")
        except Exception as e:
            logger.error(f"Failed to start Twitch bot: {e}")
    else:
        logger.info("Twitch credentials not provided, skipping Twitch bot")
    
    yield
    
    logger.info("Shutting down application...")
    
    # Stop Twitch bot if it was started
    if twitch_bot:
        try:
            await twitch_bot.close()
            logger.info("Twitch bot stopped")
        except Exception as e:
            logger.error(f"Error stopping Twitch bot: {e}")
    
    # Stop game engine
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
    """Serve the debug client page."""
    return FileResponse(FRONTEND_DIR / "debug_client.html")


@app.get("/health")
async def health():
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


# Mount static files (must be after all routes)
app.mount("/static", StaticFiles(directory=FRONTEND_DIR), name="static")
