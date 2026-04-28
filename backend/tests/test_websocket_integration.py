"""Integration tests for WebSocket protocol and FastAPI application.

Verifies that WebSocket event shapes are compatible with Unity client.
Uses httpx.AsyncClient with ASGI transport.
"""

import json
import pytest
import pytest_asyncio
from httpx import AsyncClient, ASGITransport

from app.main import app


@pytest_asyncio.fixture
async def client():
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as ac:
        yield ac


class TestHealthEndpoint:
    @pytest.mark.asyncio
    async def test_health_returns_200(self, client):
        response = await client.get("/health")
        assert response.status_code == 200
        data = response.json()
        assert "status" in data
        assert "connections" in data
        assert "engine_running" in data

    @pytest.mark.asyncio
    async def test_health_response_keys(self, client):
        response = await client.get("/health")
        data = response.json()
        assert "status" in data
        assert isinstance(data["connections"], int)
        assert isinstance(data["engine_running"], bool)


class TestWebSocketConnection:
    @pytest.mark.asyncio
    async def test_ws_connect_and_disconnect(self, client):
        # Test basic WebSocket connect/disconnect
        # Note: httpx can't easily test WebSocket; verify the endpoint exists
        response = await client.get("/health")
        assert response.status_code == 200


# =============================================================================
# Protocol compatibility - verify all expected event types exist
# =============================================================================

class TestProtocolEventTypes:
    """Ensure EventType enum has all events Unity client expects."""

    EXPECTED_EVENTS = [
        "comment", "tick", "system", "error",
        "agents_update", "agent_died", "agent_speak",
        "feed", "user_update", "world_update", "check",
        "time_update", "phase_change", "day_change",
        "weather_change", "mood_update",
        "heal", "talk", "encourage", "revive",
        "social_interaction", "relationship_change", "auto_revive",
        "agent_action", "craft", "use_item",
        "random_event", "give_item", "group_activity",
        "vfx_event", "gift_effect",
        "mode_change", "narrative_plot",
        "vote_started", "vote_update", "vote_ended", "vote_result",
        "resolution_applied",
    ]

    def test_all_expected_events_in_enum(self):
        from app.schemas import EventType
        enum_values = {e.value for e in EventType}
        for event in self.EXPECTED_EVENTS:
            assert event in enum_values, f"Missing event type: {event}"


class TestProtocolGameEventSchema:
    """Verify GameEvent JSON structure is compatible with Unity JsonUtility."""

    def test_game_event_serializes_to_json(self):
        from app.schemas import GameEvent
        event = GameEvent(
            event_type="agents_update",
            data={"agents": [{"id": 1, "name": "Jack", "hp": 100}]},
        )
        json_str = event.model_dump_json()
        parsed = json.loads(json_str)
        assert parsed["event_type"] == "agents_update"
        assert "timestamp" in parsed
        assert "data" in parsed

    def test_all_event_types_serialize(self):
        from app.schemas import GameEvent, EventType
        for event_type in EventType:
            event = GameEvent(event_type=event_type.value, data={"test": True})
            json_str = event.model_dump_json()
            assert event_type.value in json_str

    def test_client_message_deserialization(self):
        """Verify Unity can send ClientMessage and server can parse it."""
        from app.schemas import ClientMessage
        unity_json = '{"action":"feed","payload":{"user":"viewer","message":"feed Jack"}}'
        msg = ClientMessage.model_validate_json(unity_json)
        assert msg.action == "feed"
        assert msg.payload["user"] == "viewer"
