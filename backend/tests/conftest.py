"""Shared test fixtures for The Island backend tests."""

import sys
from pathlib import Path

# Ensure backend/app is importable
sys.path.insert(0, str(Path(__file__).parent.parent))

import pytest
import pytest_asyncio
from unittest.mock import AsyncMock, MagicMock
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

from app.database import Base, SessionLocal, get_db_session
from app.models import User, Agent, WorldState, GameConfig, AgentRelationship, AgentMemory


@pytest.fixture
def test_db():
    """Create an in-memory SQLite database for testing."""
    engine = create_engine("sqlite:///:memory:", connect_args={"check_same_thread": False})
    Base.metadata.create_all(bind=engine)
    TestSessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
    db = TestSessionLocal()
    yield db
    db.close()


@pytest.fixture
def test_db_session_factory():
    """Return a session factory for in-memory SQLite (for repository tests)."""
    engine = create_engine("sqlite:///:memory:", connect_args={"check_same_thread": False})
    Base.metadata.create_all(bind=engine)
    TestSessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
    return TestSessionLocal


@pytest.fixture
def mock_connection_manager():
    """Mock ConnectionManager for engine tests."""
    cm = AsyncMock()
    cm.broadcast = AsyncMock()
    cm.send_personal = AsyncMock()
    cm.connection_count = 0
    return cm


@pytest.fixture
def mock_llm_service():
    """Mock LLMService returning canned responses."""
    svc = AsyncMock()
    svc.generate_reaction = AsyncMock(return_value="Mock reaction text")
    svc.generate_idle_chat = AsyncMock(return_value="Mock idle chat text")
    svc.generate_conversation_response = AsyncMock(return_value="Mock conversation text")
    svc.generate_social_interaction = AsyncMock(return_value="Mock social interaction text")
    svc.generate_story = AsyncMock(return_value="Mock story text")
    svc.generate_gratitude = AsyncMock(return_value="Mock gratitude text")
    svc.is_mock_mode = True
    svc.model = "test-model"
    return svc


@pytest.fixture
def event_collector():
    """Collect all broadcasted GameEvent data dicts for assertion."""
    events = []

    async def collect(event_type, data):
        events.append({"event_type": event_type, "data": data})

    return events, collect


@pytest.fixture
def seed_agents(test_db):
    """Seed test database with 3 default agents."""
    agents = [
        Agent(name="Jack", personality="Brave", hp=100, energy=100, mood=70, status="Alive"),
        Agent(name="Luna", personality="Cunning", hp=100, energy=100, mood=70, status="Alive"),
        Agent(name="Bob", personality="Honest", hp=100, energy=100, mood=70, status="Alive"),
    ]
    test_db.add_all(agents)
    test_db.flush()
    return agents


@pytest.fixture
def seed_world(test_db):
    """Seed test database with default WorldState and GameConfig."""
    world = WorldState(day_count=1, weather="Sunny", time_of_day="day")
    config = GameConfig(difficulty="casual")
    test_db.add(world)
    test_db.add(config)
    test_db.flush()
    return world, config


@pytest.fixture
def seed_relationships(test_db, seed_agents):
    """Create relationships between all seeded agents."""
    rels = []
    for a in seed_agents:
        for b in seed_agents:
            if a.id != b.id:
                rel = AgentRelationship(
                    agent_from_id=a.id,
                    agent_to_id=b.id,
                    affection=20,
                    trust=20,
                    relationship_type="acquaintance",
                )
                test_db.add(rel)
                rels.append(rel)
    test_db.flush()
    return rels
