"""Tests for memory_service.py - Agent long-term memory management."""

import pytest
from unittest.mock import patch
from contextlib import contextmanager
from sqlalchemy.orm import sessionmaker
from sqlalchemy import create_engine

from app.database import Base
from app.models import Agent
from app.memory_service import MemoryService


@pytest.fixture
def memory_test_db():
    """Create in-memory SQLite DB for memory service tests."""
    engine = create_engine("sqlite:///:memory:", connect_args={"check_same_thread": False})
    Base.metadata.create_all(bind=engine)
    TestSession = sessionmaker(bind=engine, expire_on_commit=False)

    @contextmanager
    def test_session():
        db = TestSession()
        try:
            yield db
            db.commit()
        except Exception:
            db.rollback()
            raise
        finally:
            db.close()

    return test_session


def _create_agent(test_session):
    with test_session() as db:
        agent = Agent(name="Jack", personality="Brave")
        db.add(agent)
        db.flush()
        agent_id = agent.id
    return agent_id


class TestMemoryServiceAddMemory:
    @pytest.mark.asyncio
    async def test_add_memory_creates_record(self, memory_test_db, monkeypatch):
        from app import memory_service as ms_module
        monkeypatch.setattr(ms_module, "get_db_session", memory_test_db)

        agent_id = _create_agent(memory_test_db)
        svc = MemoryService()
        mem = await svc.add_memory(agent_id, "Jack saw a wild boar", importance=8)
        assert mem.id is not None
        assert mem.agent_id == agent_id
        assert mem.description == "Jack saw a wild boar"

    @pytest.mark.asyncio
    async def test_add_memory_sets_importance(self, memory_test_db, monkeypatch):
        from app import memory_service as ms_module
        monkeypatch.setattr(ms_module, "get_db_session", memory_test_db)

        agent_id = _create_agent(memory_test_db)
        svc = MemoryService()
        mem = await svc.add_memory(agent_id, "Important event", importance=10)
        assert mem.importance == 10

    @pytest.mark.asyncio
    async def test_add_memory_default_importance(self, memory_test_db, monkeypatch):
        from app import memory_service as ms_module
        monkeypatch.setattr(ms_module, "get_db_session", memory_test_db)

        agent_id = _create_agent(memory_test_db)
        svc = MemoryService()
        mem = await svc.add_memory(agent_id, "Regular event")
        assert mem.importance == 1


class TestMemoryServiceGetRelevantMemories:
    @pytest.mark.asyncio
    async def test_returns_high_importance_recent(self, memory_test_db, monkeypatch):
        from app import memory_service as ms_module
        monkeypatch.setattr(ms_module, "get_db_session", memory_test_db)

        agent_id = _create_agent(memory_test_db)
        svc = MemoryService()
        await svc.add_memory(agent_id, "Important memory", importance=8)
        await svc.add_memory(agent_id, "Trivial memory", importance=1)

        memories = await svc.get_relevant_memories(agent_id, "test context", limit=5)
        assert len(memories) >= 1
        assert any("Important memory" in m for m in memories)

    @pytest.mark.asyncio
    async def test_returns_empty_when_no_memories(self, memory_test_db, monkeypatch):
        from app import memory_service as ms_module
        monkeypatch.setattr(ms_module, "get_db_session", memory_test_db)

        agent_id = _create_agent(memory_test_db)
        svc = MemoryService()
        memories = await svc.get_relevant_memories(agent_id, "nothing", limit=5)
        assert memories == []

    @pytest.mark.asyncio
    async def test_respects_limit_parameter(self, memory_test_db, monkeypatch):
        from app import memory_service as ms_module
        monkeypatch.setattr(ms_module, "get_db_session", memory_test_db)

        agent_id = _create_agent(memory_test_db)
        svc = MemoryService()
        for i in range(10):
            await svc.add_memory(agent_id, f"Memory {i}", importance=8)

        memories = await svc.get_relevant_memories(agent_id, "test", limit=3)
        assert len(memories) <= 3

    @pytest.mark.asyncio
    async def test_filters_low_importance(self, memory_test_db, monkeypatch):
        from app import memory_service as ms_module
        monkeypatch.setattr(ms_module, "get_db_session", memory_test_db)

        agent_id = _create_agent(memory_test_db)
        svc = MemoryService()
        await svc.add_memory(agent_id, "Low importance", importance=2)
        await svc.add_memory(agent_id, "Also low", importance=3)

        memories = await svc.get_relevant_memories(agent_id, "test", limit=10)
        assert len(memories) == 0  # importance < 5 is filtered out
