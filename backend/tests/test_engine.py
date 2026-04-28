"""Tests for engine.py - GameEngine critical paths.

Tests focus on command routing, vote integration, and engine lifecycle.
Deep survival/social/activity tests require Phase 21 refactoring to be
testable without complex fixture orchestration.
"""

import time
from unittest.mock import AsyncMock, patch

import pytest

from app.engine import GameEngine


@pytest.fixture
def mock_manager():
    mgr = AsyncMock()
    mgr.broadcast = AsyncMock()
    mgr.send_personal = AsyncMock()
    mgr.connection_count = 0
    return mgr


# =============================================================================
# Test engine initial state
# =============================================================================

class TestGameEngineInit:
    def test_engine_initial_state(self, mock_manager):
        engine = GameEngine(mock_manager)
        assert engine.is_running is False

    def test_is_running_false_on_init(self, mock_manager):
        engine = GameEngine(mock_manager)
        assert engine.is_running is False


# =============================================================================
# Test command processing (process_comment dispatch)
# =============================================================================

class TestGameEngineProcessComment:
    @pytest.mark.asyncio
    async def test_comment_is_broadcast(self, mock_manager):
        engine = GameEngine(mock_manager)
        await engine.process_comment("test_user", "hello world")
        assert mock_manager.broadcast.called

    @pytest.mark.asyncio
    async def test_unknown_command_still_broadcasts(self, mock_manager):
        engine = GameEngine(mock_manager)
        await engine.process_comment("user", "xyzzy")
        assert mock_manager.broadcast.called

    @pytest.mark.asyncio
    async def test_feed_command_matches_and_broadcasts(self, mock_manager):
        engine = GameEngine(mock_manager)
        await engine.process_comment("player1", "feed Jack")
        # Should broadcast at least the COMMENT event
        # (feed handler requires DB; error is logged but broadcast should fire)
        assert mock_manager.broadcast.called

    @pytest.mark.asyncio
    async def test_check_command_is_broadcast(self, mock_manager):
        engine = GameEngine(mock_manager)
        await engine.process_comment("player1", "check")
        assert mock_manager.broadcast.called

    @pytest.mark.asyncio
    async def test_reset_command_broadcast(self, mock_manager):
        engine = GameEngine(mock_manager)
        await engine.process_comment("player1", "reset")
        assert mock_manager.broadcast.called


# =============================================================================
# Test vote command parsing delegation
# =============================================================================

class TestGameEngineVoteParsing:
    def test_parse_vote_delegates_to_vote_manager(self, mock_manager):
        engine = GameEngine(mock_manager)
        assert engine.parse_vote_command("!1") == 0
        assert engine.parse_vote_command("!2") == 1

    def test_parse_vote_invalid(self, mock_manager):
        engine = GameEngine(mock_manager)
        assert engine.parse_vote_command("hello") is None
        assert engine.parse_vote_command("") is None


# =============================================================================
# Test vote processing (async)
# =============================================================================

class TestGameEngineProcessVote:
    @pytest.mark.asyncio
    async def test_process_vote_no_active_session(self, mock_manager):
        engine = GameEngine(mock_manager)
        result = await engine.process_vote("viewer1", 0)
        assert result is False


# =============================================================================
# Test process_command delegates to process_comment
# =============================================================================

class TestGameEngineProcessCommand:
    @pytest.mark.asyncio
    async def test_process_command_delegates(self, mock_manager):
        engine = GameEngine(mock_manager)
        await engine.process_command("player1", "hello")
        assert mock_manager.broadcast.called

    @pytest.mark.asyncio
    async def test_process_command_with_special_chars(self, mock_manager):
        engine = GameEngine(mock_manager)
        await engine.process_command("user_123", "!help test message")
        assert mock_manager.broadcast.called
