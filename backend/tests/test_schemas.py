"""Tests for schemas.py - Pydantic models and EventType enum."""

import time
from app.schemas import EventType, GameEvent, ClientMessage


class TestEventType:
    def test_member_values_are_strings(self):
        for member in EventType:
            assert isinstance(member.value, str)

    def test_all_members_have_unique_values(self):
        values = [e.value for e in EventType]
        assert len(values) == len(set(values))

    def test_contains_core_events(self):
        assert EventType.COMMENT.value == "comment"
        assert EventType.TICK.value == "tick"
        assert EventType.AGENTS_UPDATE.value == "agents_update"

    def test_contains_voting_events(self):
        assert EventType.VOTE_STARTED.value == "vote_started"
        assert EventType.VOTE_UPDATE.value == "vote_update"
        assert EventType.VOTE_RESULT.value == "vote_result"


class TestGameEvent:
    def test_default_timestamp_is_set(self):
        now = time.time()
        event = GameEvent(event_type="test")
        assert event.timestamp >= now

    def test_custom_timestamp(self):
        event = GameEvent(event_type="test", timestamp=999.0)
        assert event.timestamp == 999.0

    def test_empty_data_default(self):
        event = GameEvent(event_type="test")
        assert event.data == {}

    def test_serialization(self):
        event = GameEvent(event_type="comment", data={"user": "Test", "message": "hello"})
        json_str = event.model_dump_json()
        assert "comment" in json_str
        assert "Test" in json_str

    def test_deserialization(self):
        json_str = '{"event_type":"tick","timestamp":100.0,"data":{"tick":5}}'
        event = GameEvent.model_validate_json(json_str)
        assert event.event_type == "tick"
        assert event.timestamp == 100.0
        assert event.data["tick"] == 5

    def test_rich_data_payload(self):
        event = GameEvent(
            event_type="agents_update",
            data={"agents": [{"name": "Jack", "hp": 100}, {"name": "Luna", "hp": 80}]},
        )
        assert len(event.data["agents"]) == 2

    def test_event_type_is_required(self):
        try:
            GameEvent()
            assert False, "Should have raised"
        except Exception:
            pass


class TestClientMessage:
    def test_creation(self):
        msg = ClientMessage(action="feed", payload={"agent": "Jack"})
        assert msg.action == "feed"
        assert msg.payload == {"agent": "Jack"}

    def test_empty_payload_default(self):
        msg = ClientMessage(action="check")
        assert msg.payload == {}

    def test_serialization_roundtrip(self):
        msg = ClientMessage(action="talk", payload={"agent": "Bob", "topic": "survival"})
        json_str = msg.model_dump_json()
        restored = ClientMessage.model_validate_json(json_str)
        assert restored.action == "talk"
        assert restored.payload["agent"] == "Bob"
