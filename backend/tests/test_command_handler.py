"""Tests for command_handler.py - User command processing."""

from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from app.command_handler import CommandHandler
from app.database import get_db_session


@pytest.fixture
def mock_broadcast():
    return AsyncMock()


@pytest.fixture
def mock_vfx():
    return AsyncMock()


@pytest.fixture
def mock_speak():
    return AsyncMock()


@pytest.fixture
def handler(mock_broadcast, mock_vfx, mock_speak):
    return CommandHandler(
        broadcast_callback=mock_broadcast,
        broadcast_vfx_callback=mock_vfx,
        trigger_agent_speak_callback=mock_speak,
    )


# =============================================================================
# Test command routing (handle)
# =============================================================================

class TestCommandRouting:
    @pytest.mark.asyncio
    async def test_comment_is_broadcast(self, handler, mock_broadcast):
        await handler.handle("player1", "hello world")
        assert mock_broadcast.called

    @pytest.mark.asyncio
    async def test_feed_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "feed Jack")
        assert mock_broadcast.called

    @pytest.mark.asyncio
    async def test_heal_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "heal Jack")
        assert mock_broadcast.called

    @pytest.mark.asyncio
    async def test_encourage_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "encourage Luna")
        assert mock_broadcast.called

    @pytest.mark.asyncio
    async def test_love_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "love Bob")
        assert mock_broadcast.called

    @pytest.mark.asyncio
    async def test_talk_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "talk Jack about survival")
        assert mock_broadcast.called

    @pytest.mark.asyncio
    async def test_revive_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "revive Jack")
        assert mock_broadcast.called

    @pytest.mark.asyncio
    async def test_check_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "check")
        assert mock_broadcast.called

    @pytest.mark.asyncio
    async def test_reset_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "reset")
        assert mock_broadcast.called


# =============================================================================
# Test command execution (with DB)
# =============================================================================

class TestCommandExecution:
    @pytest.mark.asyncio
    async def test_feed_agent_not_found(self, mock_broadcast, mock_vfx, mock_speak):
        handler = CommandHandler(
            broadcast_callback=mock_broadcast,
            broadcast_vfx_callback=mock_vfx,
            trigger_agent_speak_callback=mock_speak,
        )
        await handler.handle("player1", "feed Nonexistent")
        # Should broadcast error
        error_events = [c for c in mock_broadcast.call_args_list
                       if c[0][0] == "error"]
        assert len(error_events) >= 1

    @pytest.mark.asyncio
    async def test_check_shows_status(self, mock_broadcast, mock_vfx, mock_speak):
        handler = CommandHandler(
            broadcast_callback=mock_broadcast,
            broadcast_vfx_callback=mock_vfx,
            trigger_agent_speak_callback=mock_speak,
        )
        await handler.handle("player1", "check")
        assert mock_broadcast.called


# =============================================================================
# Test constructor wiring
# =============================================================================

class TestCommandHandlerInit:
    def test_accepts_llm_service(self, mock_broadcast, mock_vfx, mock_speak):
        mock_llm = MagicMock()
        handler = CommandHandler(
            broadcast_callback=mock_broadcast,
            broadcast_vfx_callback=mock_vfx,
            trigger_agent_speak_callback=mock_speak,
            llm_service=mock_llm,
        )
        assert handler._llm is mock_llm

    def test_accepts_memory_service(self, mock_broadcast, mock_vfx, mock_speak):
        mock_memory = MagicMock()
        handler = CommandHandler(
            broadcast_callback=mock_broadcast,
            broadcast_vfx_callback=mock_vfx,
            trigger_agent_speak_callback=mock_speak,
            memory_service=mock_memory,
        )
        assert handler._memory is mock_memory
