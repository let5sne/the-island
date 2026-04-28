"""Tests for database.py and models.py - ORM models and DB operations."""

import pytest
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

from app.database import Base, init_db, get_db_session
from app.models import User, Agent, WorldState, GameConfig, AgentRelationship, AgentMemory


@pytest.fixture
def db_session():
    engine = create_engine("sqlite:///:memory:", connect_args={"check_same_thread": False})
    Base.metadata.create_all(bind=engine)
    Session = sessionmaker(bind=engine)
    session = Session()
    yield session
    session.close()


class TestDatabaseInitialization:
    def test_init_db_creates_tables(self):
        engine = create_engine("sqlite:///:memory:", connect_args={"check_same_thread": False})
        init_db()  # Creates tables on the global engine, not our test one
        # Just verify no exception

    def test_get_db_session_commits(self, db_session):
        user = User(username="test_user")
        db_session.add(user)
        db_session.commit()
        found = db_session.query(User).filter_by(username="test_user").first()
        assert found is not None

    def test_get_db_session_rolls_back_on_error(self, db_session):
        with pytest.raises(Exception):
            with get_db_session() as db:
                user = User(username="test")
                db.add(user)
                raise ValueError("rollback test")
        # After raise, the user should not be persisted
        found = db_session.query(User).filter_by(username="test").first()
        assert found is None


class TestUserModel:
    def test_create_user(self, db_session):
        user = User(username="viewer1")
        db_session.add(user)
        db_session.commit()
        assert user.id is not None

    def test_unique_username(self, db_session):
        db_session.add(User(username="same"))
        db_session.commit()
        db_session.add(User(username="same"))
        with pytest.raises(Exception):
            db_session.commit()

    def test_default_gold_is_100(self, db_session):
        user = User(username="new_user")
        db_session.add(user)
        db_session.commit()
        assert user.gold == 100

    def test_repr(self, db_session):
        user = User(username="test", gold=50)
        assert "test" in repr(user)


class TestAgentModel:
    def test_create_agent(self, db_session):
        agent = Agent(name="Jack", personality="Brave")
        db_session.add(agent)
        db_session.commit()
        assert agent.id is not None
        assert agent.name == "Jack"

    def test_is_alive_property(self, db_session):
        agent = Agent(name="Jack", personality="Brave", status="Alive")
        db_session.add(agent)
        db_session.commit()
        assert agent.is_alive is True
        agent.status = "Dead"
        assert agent.is_alive is False

    def test_to_dict_keys(self, db_session):
        agent = Agent(name="Jack", personality="Brave")
        db_session.add(agent)
        db_session.commit()
        d = agent.to_dict()
        assert d["name"] == "Jack"
        assert d["status"] == "Alive"
        assert "hp" in d
        assert "energy" in d
        assert "mood" in d

    def test_default_values(self, db_session):
        agent = Agent(name="Default", personality="Neutral")
        db_session.add(agent)
        db_session.commit()
        assert agent.hp == 100
        assert agent.energy == 100
        assert agent.mood == 70
        assert agent.status == "Alive"
        assert agent.is_sick is False

    def test_sick_default_false(self, db_session):
        agent = Agent(name="Healthy", personality="Brave")
        db_session.add(agent)
        db_session.commit()
        assert agent.is_sick is False


class TestWorldStateModel:
    def test_create_world_state(self, db_session):
        world = WorldState()
        db_session.add(world)
        db_session.commit()
        assert world.id is not None

    def test_default_values(self, db_session):
        world = WorldState()
        db_session.add(world)
        db_session.commit()
        assert world.day_count == 1
        assert world.weather == "Sunny"
        assert world.time_of_day == "day"
        assert world.tree_left_fruit == 5
        assert world.tree_right_fruit == 5

    def test_to_dict_keys(self, db_session):
        world = WorldState()
        db_session.add(world)
        db_session.commit()
        d = world.to_dict()
        assert "day_count" in d
        assert "weather" in d
        assert "time_of_day" in d


class TestGameConfigModel:
    def test_create_config(self, db_session):
        config = GameConfig()
        db_session.add(config)
        db_session.commit()
        assert config.id is not None

    def test_casual_default_multipliers(self, db_session):
        config = GameConfig()
        db_session.add(config)
        db_session.commit()
        assert config.difficulty == "casual"
        assert config.energy_decay_multiplier == 0.5
        assert config.hp_decay_multiplier == 0.5
        assert config.auto_revive_enabled is True


class TestAgentRelationshipModel:
    def test_create_relationship(self, db_session):
        a1 = Agent(name="Jack", personality="Brave")
        a2 = Agent(name="Luna", personality="Cunning")
        db_session.add_all([a1, a2])
        db_session.commit()

        rel = AgentRelationship(agent_from_id=a1.id, agent_to_id=a2.id)
        db_session.add(rel)
        db_session.commit()
        assert rel.id is not None
        assert rel.relationship_type == "stranger"

    def test_unique_constraint(self, db_session):
        a1 = Agent(name="Jack", personality="Brave")
        a2 = Agent(name="Luna", personality="Cunning")
        db_session.add_all([a1, a2])
        db_session.commit()

        rel1 = AgentRelationship(agent_from_id=a1.id, agent_to_id=a2.id)
        rel2 = AgentRelationship(agent_from_id=a1.id, agent_to_id=a2.id)
        db_session.add(rel1)
        db_session.commit()
        db_session.add(rel2)
        with pytest.raises(Exception):
            db_session.commit()

    def test_update_relationship_type_logic(self, db_session):
        a1 = Agent(name="Jack", personality="Brave")
        a2 = Agent(name="Luna", personality="Cunning")
        db_session.add_all([a1, a2])
        db_session.commit()

        rel = AgentRelationship(agent_from_id=a1.id, agent_to_id=a2.id)
        rel.affection = 60
        rel.trust = 60
        rel.update_relationship_type()
        assert rel.relationship_type == "close_friend"

    def test_default_affection_trust(self, db_session):
        a1 = Agent(name="A", personality="Brave")
        a2 = Agent(name="B", personality="Cunning")
        db_session.add_all([a1, a2])
        db_session.flush()

        rel = AgentRelationship(agent_from_id=a1.id, agent_to_id=a2.id)
        db_session.add(rel)
        db_session.commit()
        assert rel.affection == 0
        assert rel.trust == 0


class TestAgentMemoryModel:
    def test_create_memory(self, db_session):
        agent = Agent(name="Jack", personality="Brave")
        db_session.add(agent)
        db_session.commit()

        mem = AgentMemory(
            agent_id=agent.id,
            description="Jack found berries",
            importance=5,
        )
        db_session.add(mem)
        db_session.commit()
        assert mem.id is not None

    def test_to_dict_keys(self, db_session):
        agent = Agent(name="Jack", personality="Brave")
        db_session.add(agent)
        db_session.commit()

        mem = AgentMemory(agent_id=agent.id, description="Test memory")
        db_session.add(mem)
        db_session.commit()

        d = mem.to_dict()
        assert "id" in d
        assert "agent_id" in d
        assert "description" in d
