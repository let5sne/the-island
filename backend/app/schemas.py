"""
Pydantic models for the JSON protocol.
Defines standardized structures for game events and messages.
"""

from enum import Enum
from typing import Any
from pydantic import BaseModel, Field
import time


class EventType(str, Enum):
    """Enumeration of all possible game event types."""
    COMMENT = "comment"
    TICK = "tick"
    SYSTEM = "system"
    ERROR = "error"

    # Island survival events
    AGENTS_UPDATE = "agents_update"      # All agents status broadcast
    AGENT_DIED = "agent_died"            # An agent has died
    AGENT_SPEAK = "agent_speak"          # Agent says something (LLM response)
    FEED = "feed"                        # User fed an agent
    USER_UPDATE = "user_update"          # User gold/status update
    WORLD_UPDATE = "world_update"        # World state update
    CHECK = "check"                      # Status check response

    # Day/Night cycle (Phase 2)
    TIME_UPDATE = "time_update"          # Time tick update
    PHASE_CHANGE = "phase_change"        # Dawn/day/dusk/night transition
    DAY_CHANGE = "day_change"            # New day started

    # Weather system (Phase 3)
    WEATHER_CHANGE = "weather_change"    # Weather changed
    MOOD_UPDATE = "mood_update"          # Agent mood changed

    # New commands (Phase 4)
    HEAL = "heal"                        # User healed an agent
    TALK = "talk"                        # User talked to an agent
    ENCOURAGE = "encourage"              # User encouraged an agent
    REVIVE = "revive"                    # User revived a dead agent

    # Social system (Phase 5)
    SOCIAL_INTERACTION = "social_interaction"  # Agents interacted
    RELATIONSHIP_CHANGE = "relationship_change"  # Relationship status changed
    AUTO_REVIVE = "auto_revive"          # Agent auto-revived (casual mode)

    # Autonomous Agency (Phase 13)
    AGENT_ACTION = "agent_action"        # Agent performs an action (move, gather, etc.)

    # Crafting System (Phase 16)
    CRAFT = "craft"                      # Agent crafted an item
    USE_ITEM = "use_item"                # Agent used an item

    # Random Events (Phase 17-C)
    RANDOM_EVENT = "random_event"        # Random event occurred

    # Economy (Phase 23)
    GIVE_ITEM = "give_item"              # Agent gives item to another

    # Group Activities (Phase 24)
    # Group Activities (Phase 24)
    GROUP_ACTIVITY = "group_activity"    # Storytelling, dancing, etc.

    # VFX & Gifts (Phase 8)
    VFX_EVENT = "vfx_event"              # Visual effect trigger
    GIFT_EFFECT = "gift_effect"          # Twitch bits/sub effect


class GameEvent(BaseModel):
    """
    Standardized game event structure for WebSocket communication.

    Attributes:
        event_type: The type of event (comment, agent_response, tick, etc.)
        timestamp: Unix timestamp when the event was created
        data: Arbitrary payload data for the event
    """
    event_type: str = Field(..., description="Type of the game event")
    timestamp: float = Field(default_factory=time.time, description="Unix timestamp")
    data: dict[str, Any] = Field(default_factory=dict, description="Event payload")

    class Config:
        json_schema_extra = {
            "example": {
                "event_type": "comment",
                "timestamp": 1704067200.0,
                "data": {"user": "User123", "message": "Attack!"}
            }
        }


class ClientMessage(BaseModel):
    """
    Message structure for client-to-server communication.

    Attributes:
        action: The action the client wants to perform
        payload: Data associated with the action
    """
    action: str = Field(..., description="Action to perform")
    payload: dict[str, Any] = Field(default_factory=dict, description="Action payload")
