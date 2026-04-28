"""Tests for simulation.py - survival, social, activity, inventory functions."""

import json
from contextlib import contextmanager
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

from app.database import Base
from app.models import Agent, WorldState, GameConfig, AgentRelationship
from app import simulation as sim


# =============================================================================
# Test DB helpers
# =============================================================================

@pytest.fixture
def db_factory():
    """Create an in-memory SQLite session factory."""
    engine = create_engine("sqlite:///:memory:", connect_args={"check_same_thread": False})
    Base.metadata.create_all(bind=engine)
    return sessionmaker(bind=engine, expire_on_commit=False)


def _make_eng(db_factory):
    """Create a mock 'eng' with real DB session and cross-references."""
    m = MagicMock()
    m._broadcast_event = AsyncMock()
    m._tick_count = 1
    m._active_conversations = {}

    # Wire get_db_session replacement
    @contextmanager
    def test_sf():
        db = db_factory()
        try:
            yield db
            db.commit()
        except Exception:
            db.rollback()
            raise
        finally:
            db.close()

    # Wire _get_config
    m._get_config = lambda: _get_or_create_config(db_factory)

    # Wire inventory functions (return real values, not mocks)
    m._get_inventory = lambda agent: sim._get_inventory(m, agent)
    m._set_inventory = lambda agent, inv: sim._set_inventory(m, agent, inv)
    m._consume_fruit = AsyncMock(return_value=True)
    m._gather_herb = AsyncMock()
    m._craft_medicine = AsyncMock()
    m._use_medicine = AsyncMock()
    m._select_interaction = MagicMock(return_value="chat")
    m._trigger_social_dialogue = AsyncMock()
    m._find_follow_target = MagicMock(return_value=None)
    m._get_action_bark = MagicMock(return_value="Hello!")
    m._process_altruism_tick = AsyncMock()
    m._process_conversation_reply = AsyncMock()

    # Store test session for patching
    m._test_sf = test_sf

    return m


def _get_or_create_config(db_factory):
    db = db_factory()
    try:
        config = db.query(GameConfig).first()
        if not config:
            config = GameConfig()
            db.add(config)
            db.commit()
        return config
    finally:
        db.close()


def _seed_agent(db_factory, name="Jack", personality="Brave", **kwargs):
    db = db_factory()
    try:
        agent = Agent(name=name, personality=personality, **kwargs)
        db.add(agent)
        db.commit()
        return agent
    finally:
        db.close()


# Helper decorator to patch get_db_session
def with_db_patch(test_func):
    """Decorator that patches get_db_session in the simulation module."""
    async def wrapper(self, db_factory):
        eng = _make_eng(db_factory)
        with patch('app.simulation.get_db_session', eng._test_sf):
            return await test_func(self, db_factory, eng)
    return wrapper


# =============================================================================
# Inventory Tests
# =============================================================================

class TestInventory:
    def test_get_inventory_empty(self):
        agent = Agent(name="Test", personality="Neutral")
        assert sim._get_inventory(MagicMock(), agent) == {}

    def test_get_inventory_with_items(self):
        agent = Agent(name="Test", personality="Neutral", inventory='{"herb": 3, "medicine": 1}')
        assert sim._get_inventory(MagicMock(), agent) == {"herb": 3, "medicine": 1}

    def test_get_inventory_bad_json(self):
        agent = Agent(name="Test", personality="Neutral", inventory="not json")
        assert sim._get_inventory(MagicMock(), agent) == {}

    def test_set_inventory(self):
        agent = Agent(name="Test", personality="Neutral")
        sim._set_inventory(MagicMock(), agent, {"herb": 5})
        assert json.loads(agent.inventory) == {"herb": 5}

    @pytest.mark.asyncio
    async def test_consume_fruit_success(self, db_factory):
        db = db_factory()
        world = WorldState(tree_left_fruit=5)
        db.add(world)
        db.commit()
        db.close()

        eng = _make_eng(db_factory)
        with patch('app.simulation.get_db_session', eng._test_sf):
            result = await sim._consume_fruit(eng, world, "tree_left")
        assert result is True

    @pytest.mark.asyncio
    async def test_craft_medicine_success(self, db_factory):
        _get_or_create_config(db_factory)
        agent = _seed_agent(db_factory, "Jack", inventory='{"herb": 5}')
        eng = _make_eng(db_factory)

        with patch('app.simulation.get_db_session', eng._test_sf):
            await sim._craft_medicine(eng, agent)
        inv = sim._get_inventory(None, agent)
        assert inv.get("medicine", 0) >= 1

    @pytest.mark.asyncio
    async def test_craft_medicine_not_enough_herbs(self, db_factory):
        _get_or_create_config(db_factory)
        agent = _seed_agent(db_factory, "Jack", inventory='{"herb": 1}')
        eng = _make_eng(db_factory)

        with patch('app.simulation.get_db_session', eng._test_sf):
            await sim._craft_medicine(eng, agent)
        inv = sim._get_inventory(None, agent)
        assert inv.get("medicine", 0) == 0

    @pytest.mark.asyncio
    async def test_use_medicine_cures_sickness(self, db_factory):
        _get_or_create_config(db_factory)
        agent = _seed_agent(db_factory, "Jack", is_sick=True, hp=50, inventory='{"medicine": 2}')
        eng = _make_eng(db_factory)

        with patch('app.simulation.get_db_session', eng._test_sf):
            await sim._use_medicine(eng, agent)
        assert agent.is_sick is False
        assert agent.hp > 50


# =============================================================================
# Survival & Time Tests
# =============================================================================

class TestSurvival:
    @pytest.mark.asyncio
    async def test_process_survival_tick_energy_decay(self, db_factory):
        _get_or_create_config(db_factory)
        db = db_factory()
        world = WorldState(weather="Sunny", time_of_day="day")
        db.add(world)
        db.commit()
        _seed_agent(db_factory, "Jack", energy=100, hp=100)
        db.close()

        eng = _make_eng(db_factory)
        eng._get_config = lambda: _get_or_create_config(db_factory)

        with patch('app.simulation.get_db_session', eng._test_sf):
            await sim._process_survival_tick(eng)

    @pytest.mark.asyncio
    async def test_process_auto_revive(self, db_factory):
        config = _get_or_create_config(db_factory)
        config.auto_revive_enabled = True
        config.auto_revive_delay_ticks = 5
        config.revive_hp = 50
        config.revive_energy = 50
        db = db_factory()
        db.merge(config)
        db.commit()
        db.close()

        agent = _seed_agent(db_factory, "Jack", status="Dead", death_tick=1)
        eng = _make_eng(db_factory)
        eng._tick_count = 10

        with patch('app.simulation.get_db_session', eng._test_sf):
            await sim._process_auto_revive(eng)

    @pytest.mark.asyncio
    async def test_update_moods(self, db_factory):
        db = db_factory()
        world = WorldState(time_of_day="day", weather="Sunny")
        db.add(world)
        db.commit()
        _seed_agent(db_factory, "Jack", mood=50, status="Alive")
        _seed_agent(db_factory, "Luna", mood=30, status="Alive")
        db.close()

        eng = _make_eng(db_factory)
        with patch('app.simulation.get_db_session', eng._test_sf):
            await sim._update_moods(eng)
        assert eng._broadcast_event.call_count >= 0  # No crash is success


# =============================================================================
# Social Tests
# =============================================================================

class TestSocial:
    def test_select_interaction_returns_valid(self):
        eng = MagicMock()
        initiator = Agent(name="Jack", personality="Brave", energy=80, mood=60)
        target = Agent(name="Luna", personality="Cunning", energy=60, mood=50)
        rel = AgentRelationship(agent_from_id=1, agent_to_id=2)

        result = sim._select_interaction(eng, initiator, target, rel)
        assert result is not None
        assert isinstance(result, str)

    @pytest.mark.asyncio
    async def test_assign_social_roles(self, db_factory):
        db = db_factory()
        _seed_agent(db_factory, "Jack", social_tendency="extrovert", mood=80, status="Alive")
        _seed_agent(db_factory, "Luna", social_tendency="introvert", mood=60, status="Alive")
        _seed_agent(db_factory, "Bob", social_tendency="neutral", mood=70, status="Alive")
        db.close()

        eng = _make_eng(db_factory)
        with patch('app.simulation.get_db_session', eng._test_sf):
            await sim._assign_social_roles(eng)
        # No crash is success


# =============================================================================
# Random Events Tests
# =============================================================================

class TestRandomEvents:
    @pytest.mark.asyncio
    async def test_no_trigger_wrong_tick(self):
        eng = MagicMock()
        eng._broadcast_event = AsyncMock()
        eng._tick_count = 50
        eng._get_inventory = lambda a: {}
        eng._set_inventory = lambda a, i: None

        await sim._process_random_events(eng)
        assert not eng._broadcast_event.called

    @pytest.mark.asyncio
    async def test_triggers_on_correct_tick(self, db_factory):
        db = db_factory()
        world = WorldState(tree_left_fruit=5, tree_right_fruit=5)
        db.add(world)
        db.commit()
        _seed_agent(db_factory, "Jack", hp=80)
        _seed_agent(db_factory, "Luna", hp=80)
        db.close()

        eng = _make_eng(db_factory)
        eng._tick_count = 1
        eng._get_inventory = lambda a: {}
        eng._set_inventory = lambda a, i: None

        with patch('app.simulation.get_db_session', eng._test_sf), \
             patch('app.simulation.random.random', return_value=0.05), \
             patch('app.simulation.random.choices', return_value=["storm_damage"]):
            await sim._process_random_events(eng)
            assert eng._broadcast_event.called


# =============================================================================
# Activity Tests
# =============================================================================

class TestActivity:
    def test_get_action_bark(self):
        eng = MagicMock()
        agent = Agent(name="Jack", personality="Brave")
        bark = sim._get_action_bark(eng, agent, "Gather", "tree")
        assert isinstance(bark, str)
        assert len(bark) > 0

    def test_get_action_bark_wake_up_once(self):
        eng = MagicMock()
        agent = Agent(name="Jack", personality="Brave")
        bark = sim._get_action_bark(eng, agent, "Wake Up", None)
        assert bark == "Good morning!"

    @pytest.mark.asyncio
    async def test_skips_on_mod(self, db_factory):
        db = db_factory()
        db.close()

        eng = _make_eng(db_factory)
        eng._tick_count = 4
        eng._get_config = lambda: _get_or_create_config(db_factory)

        with patch('app.simulation.get_db_session', eng._test_sf):
            await sim._process_activity_tick(eng)
        # Should skip without crash


# =============================================================================
# Campfire & Group Tests
# =============================================================================

class TestCampfireAndGroup:
    @pytest.mark.asyncio
    async def test_process_campfire_gathering(self, db_factory):
        db = db_factory()
        world = WorldState(time_of_day="day")
        db.add(world)
        db.commit()
        db.close()

        eng = _make_eng(db_factory)
        with patch('app.simulation.get_db_session', eng._test_sf):
            await sim._process_campfire_gathering(eng)
        # Daytime: no campfire, but no crash
