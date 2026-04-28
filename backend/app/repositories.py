"""Repository pattern for database access.

Decouples business logic from raw SQLAlchemy queries,
enabling testability and reducing duplication.
"""

from typing import Optional

from .models import Agent, WorldState, GameConfig, AgentRelationship


class AgentRepository:
    """Data access for Agent entities."""

    def __init__(self, session_factory):
        self._sf = session_factory

    def get_all_alive(self) -> list[Agent]:
        with self._sf() as db:
            return db.query(Agent).filter(Agent.status == "Alive").all()

    def get_all(self) -> list[Agent]:
        with self._sf() as db:
            return db.query(Agent).all()

    def get_by_id(self, agent_id: int) -> Optional[Agent]:
        with self._sf() as db:
            return db.query(Agent).filter(Agent.id == agent_id).first()

    def get_by_name(self, name: str) -> Optional[Agent]:
        with self._sf() as db:
            return db.query(Agent).filter(Agent.name.ilike(name)).first()

    def get_dead(self) -> list[Agent]:
        with self._sf() as db:
            return db.query(Agent).filter(Agent.status == "Dead").all()

    def count_alive(self) -> int:
        with self._sf() as db:
            return db.query(Agent).filter(Agent.status == "Alive").count()

    def get_leaders(self) -> list[Agent]:
        with self._sf() as db:
            return db.query(Agent).filter(
                Agent.status == "Alive", Agent.social_role == "leader"
            ).all()

    def get_followers(self) -> list[Agent]:
        with self._sf() as db:
            return db.query(Agent).filter(
                Agent.status == "Alive", Agent.social_role == "follower"
            ).all()

    def get_at_location(self, location: str) -> list[Agent]:
        with self._sf() as db:
            return db.query(Agent).filter(
                Agent.status == "Alive", Agent.location == location
            ).all()

    def save(self, agent: Agent) -> None:
        with self._sf() as db:
            db.merge(agent)


class WorldStateRepository:
    """Data access for WorldState singleton."""

    def __init__(self, session_factory):
        self._sf = session_factory

    def get(self) -> Optional[WorldState]:
        with self._sf() as db:
            return db.query(WorldState).first()

    def save(self, world: WorldState) -> None:
        with self._sf() as db:
            db.merge(world)

    def create_if_absent(self) -> WorldState:
        with self._sf() as db:
            ws = db.query(WorldState).first()
            if ws is None:
                ws = WorldState()
                db.add(ws)
                db.flush()
            return ws


class GameConfigRepository:
    """Data access for GameConfig singleton."""

    def __init__(self, session_factory):
        self._sf = session_factory

    def get(self) -> GameConfig:
        with self._sf() as db:
            config = db.query(GameConfig).first()
            if config is None:
                config = GameConfig()
            db.expunge(config)
            return config

    def save(self, config: GameConfig) -> None:
        with self._sf() as db:
            db.merge(config)


class RelationshipRepository:
    """Data access for AgentRelationship entities."""

    def __init__(self, session_factory):
        self._sf = session_factory

    def get(self, from_id: int, to_id: int) -> Optional[AgentRelationship]:
        with self._sf() as db:
            return db.query(AgentRelationship).filter(
                AgentRelationship.agent_from_id == from_id,
                AgentRelationship.agent_to_id == to_id,
            ).first()

    def get_for_agent(self, agent_id: int, exclude_stranger: bool = True) -> list[AgentRelationship]:
        with self._sf() as db:
            query = db.query(AgentRelationship).filter(
                AgentRelationship.agent_from_id == agent_id,
            )
            if exclude_stranger:
                query = query.filter(AgentRelationship.relationship_type != "stranger")
            return query.all()

    def get_friends(self, agent_id: int) -> list[AgentRelationship]:
        with self._sf() as db:
            return db.query(AgentRelationship).filter(
                AgentRelationship.agent_from_id == agent_id,
                AgentRelationship.relationship_type.in_(["close_friend", "friend"]),
            ).all()

    def save(self, rel: AgentRelationship) -> None:
        with self._sf() as db:
            db.merge(rel)

    def get_or_create(self, from_id: int, to_id: int) -> AgentRelationship:
        with self._sf() as db:
            rel = db.query(AgentRelationship).filter(
                AgentRelationship.agent_from_id == from_id,
                AgentRelationship.agent_to_id == to_id,
            ).first()
            if rel is None:
                rel = AgentRelationship(agent_from_id=from_id, agent_to_id=to_id)
                db.add(rel)
                db.flush()
            return rel
