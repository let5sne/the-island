"""
SQLAlchemy ORM models for The Island.
Defines User (viewers), Agent (NPCs), WorldState, GameConfig, and AgentRelationship.
"""

from datetime import datetime

from sqlalchemy import Column, Integer, String, DateTime, Float, Boolean, ForeignKey, UniqueConstraint, func

from .database import Base


class User(Base):
    """
    Represents a viewer/donor from the live stream.
    They can spend gold to influence the island.
    """
    __tablename__ = "users"

    id = Column(Integer, primary_key=True, index=True)
    username = Column(String(100), unique=True, index=True, nullable=False)
    gold = Column(Integer, default=100)  # Starting gold for new users
    created_at = Column(DateTime, default=func.now())

    def __repr__(self):
        return f"<User {self.username} gold={self.gold}>"


class Agent(Base):
    """
    Represents an NPC survivor on the island.
    Has personality, health, energy, mood, and social attributes.
    """
    __tablename__ = "agents"

    id = Column(Integer, primary_key=True, index=True)
    name = Column(String(50), unique=True, nullable=False)
    personality = Column(String(50), nullable=False)
    status = Column(String(20), default="Alive")  # Alive, Exiled, Dead
    hp = Column(Integer, default=100)
    energy = Column(Integer, default=100)
    inventory = Column(String(500), default="{}")  # JSON string

    # Mood system (Phase 3)
    mood = Column(Integer, default=70)  # 0-100 scale
    mood_state = Column(String(20), default="neutral")  # happy, neutral, sad, anxious

    # Revival tracking (Phase 1)
    death_tick = Column(Integer, nullable=True)  # Tick when agent died

    # Social attributes (Phase 5)
    social_tendency = Column(String(20), default="neutral")  # introvert, extrovert, neutral

    def __repr__(self):
        return f"<Agent {self.name} ({self.personality}) HP={self.hp} Energy={self.energy} Mood={self.mood}>"

    @property
    def is_alive(self) -> bool:
        """Check if agent is alive."""
        return self.status == "Alive"

    def to_dict(self):
        """Convert to dictionary for JSON serialization."""
        return {
            "id": self.id,
            "name": self.name,
            "personality": self.personality,
            "status": self.status,
            "hp": self.hp,
            "energy": self.energy,
            "inventory": self.inventory,
            "mood": self.mood,
            "mood_state": self.mood_state,
            "social_tendency": self.social_tendency
        }


class WorldState(Base):
    """
    Global state of the island environment.
    Tracks day count, time of day, weather, and resources.
    """
    __tablename__ = "world_state"

    id = Column(Integer, primary_key=True, index=True)
    day_count = Column(Integer, default=1)
    weather = Column(String(20), default="Sunny")
    resource_level = Column(Integer, default=100)

    # Day/Night cycle (Phase 2)
    current_tick_in_day = Column(Integer, default=0)  # 0 to TICKS_PER_DAY
    time_of_day = Column(String(10), default="day")  # dawn, day, dusk, night

    # Weather system (Phase 3)
    weather_duration = Column(Integer, default=0)  # Ticks since last weather change

    def __repr__(self):
        return f"<WorldState Day={self.day_count} {self.time_of_day} Weather={self.weather}>"

    def to_dict(self):
        """Convert to dictionary for JSON serialization."""
        return {
            "day_count": self.day_count,
            "weather": self.weather,
            "resource_level": self.resource_level,
            "current_tick_in_day": self.current_tick_in_day,
            "time_of_day": self.time_of_day
        }


class GameConfig(Base):
    """
    Game configuration for difficulty settings.
    Supports casual and normal modes.
    """
    __tablename__ = "game_config"

    id = Column(Integer, primary_key=True, index=True)
    difficulty = Column(String(20), default="casual")  # normal, casual

    # Decay multipliers (1.0 = normal, 0.5 = casual)
    energy_decay_multiplier = Column(Float, default=0.5)
    hp_decay_multiplier = Column(Float, default=0.5)

    # Revival settings
    auto_revive_enabled = Column(Boolean, default=True)
    auto_revive_delay_ticks = Column(Integer, default=12)  # 60 seconds at 5s/tick
    revive_hp = Column(Integer, default=50)
    revive_energy = Column(Integer, default=50)

    # Social settings
    social_interaction_probability = Column(Float, default=0.3)

    updated_at = Column(DateTime, default=func.now(), onupdate=func.now())

    def __repr__(self):
        return f"<GameConfig difficulty={self.difficulty}>"

    def to_dict(self):
        """Convert to dictionary for JSON serialization."""
        return {
            "difficulty": self.difficulty,
            "energy_decay_multiplier": self.energy_decay_multiplier,
            "hp_decay_multiplier": self.hp_decay_multiplier,
            "auto_revive_enabled": self.auto_revive_enabled,
            "auto_revive_delay_ticks": self.auto_revive_delay_ticks,
            "social_interaction_probability": self.social_interaction_probability
        }


class AgentRelationship(Base):
    """
    Tracks relationships between agents.
    Affection, trust determine relationship type.
    """
    __tablename__ = "agent_relationships"

    id = Column(Integer, primary_key=True, index=True)
    agent_from_id = Column(Integer, ForeignKey("agents.id"), nullable=False)
    agent_to_id = Column(Integer, ForeignKey("agents.id"), nullable=False)

    # Relationship metrics (-100 to 100)
    affection = Column(Integer, default=0)  # Liking
    trust = Column(Integer, default=0)  # Trust level

    # Derived from metrics
    relationship_type = Column(String(30), default="stranger")
    # Types: stranger, acquaintance, friend, close_friend, rival

    # Interaction tracking
    interaction_count = Column(Integer, default=0)
    last_interaction_tick = Column(Integer, default=0)

    created_at = Column(DateTime, default=func.now())
    updated_at = Column(DateTime, default=func.now(), onupdate=func.now())

    __table_args__ = (
        UniqueConstraint('agent_from_id', 'agent_to_id', name='unique_relationship'),
    )

    def __repr__(self):
        return f"<Relationship {self.agent_from_id}->{self.agent_to_id} {self.relationship_type}>"

    def to_dict(self):
        """Convert to dictionary for JSON serialization."""
        return {
            "agent_from_id": self.agent_from_id,
            "agent_to_id": self.agent_to_id,
            "affection": self.affection,
            "trust": self.trust,
            "relationship_type": self.relationship_type,
            "interaction_count": self.interaction_count
        }

    def update_relationship_type(self):
        """Calculate and update relationship type based on metrics."""
        total = self.affection + self.trust

        if total <= -50:
            self.relationship_type = "rival"
        elif total <= 20:
            self.relationship_type = "stranger"
        elif total <= 50:
            self.relationship_type = "acquaintance"
        elif total <= 100:
            self.relationship_type = "friend"
        else:
            self.relationship_type = "close_friend"
