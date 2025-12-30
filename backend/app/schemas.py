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
    AGENT_RESPONSE = "agent_response"
    TICK = "tick"
    SYSTEM = "system"
    ERROR = "error"


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
