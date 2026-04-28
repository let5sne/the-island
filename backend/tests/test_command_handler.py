"""Tests for command_handler.py - User command processing."""

from contextlib import contextmanager
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

from app.database import Base
from app import command_handler as ch_module
from app import simulation as sim_module
from app.command_handler import CommandHandler


@pytest.fixture
def test_db():
    """Create in-memory SQLite for command handler tests."""
    engine = create_engine("sqlite:///:memory:", connect_args={"check_same_thread": False})
    Base.metadata.create_all(bind=engine)
    Session = sessionmaker(bind=engine, expire_on_commit=False)

    @contextmanager
    def test_session():
        db = Session()
        try:
            yield db
            db.commit()
        except Exception:
            db.rollback()
            raise
        finally:
            db.close()

    return test_session


@pytest.fixture(autouse=True)
def patch_db(test_db, monkeypatch):
    """Auto-patch get_db_session in command_handler and simulation modules."""
    monkeypatch.setattr(ch_module, "get_db_session", test_db)
    monkeypatch.setattr(sim_module, "get_db_session", test_db)


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

    @pytest.mark.asyncio
    async def test_build_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "build shelter")
        assert mock_broadcast.called

    @pytest.mark.asyncio
    async def test_pardon_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "pardon Jack")
        assert mock_broadcast.called

    @pytest.mark.asyncio
    async def test_dreamwalk_command_matched(self, handler, mock_broadcast):
        await handler.handle("player1", "dream Jack")
        assert mock_broadcast.called


class TestCommandExecution:
    @pytest.mark.asyncio
    async def test_feed_agent_not_found(self, mock_broadcast, mock_vfx, mock_speak):
        handler = CommandHandler(
            broadcast_callback=mock_broadcast,
            broadcast_vfx_callback=mock_vfx,
            trigger_agent_speak_callback=mock_speak,
        )
        await handler.handle("player1", "feed Nonexistent")
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
