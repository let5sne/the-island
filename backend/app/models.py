"""
SQLAlchemy ORM models for The Island.
Defines User (viewers), Agent (NPCs), and WorldState entities.
"""

from datetime import datetime

from sqlalchemy import Column, Integer, String, DateTime, func

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
    Has personality, health, energy, and inventory.
    """
    __tablename__ = "agents"

    id = Column(Integer, primary_key=True, index=True)
    name = Column(String(50), unique=True, nullable=False)
    personality = Column(String(50), nullable=False)
    status = Column(String(20), default="Alive")  # Alive, Exiled, Dead
    hp = Column(Integer, default=100)
    energy = Column(Integer, default=100)
    inventory = Column(String(500), default="{}")  # JSON string

    def __repr__(self):
        return f"<Agent {self.name} ({self.personality}) HP={self.hp} Energy={self.energy} Status={self.status}>"

    def to_dict(self):
        """Convert to dictionary for JSON serialization."""
        return {
            "id": self.id,
            "name": self.name,
            "personality": self.personality,
            "status": self.status,
            "hp": self.hp,
            "energy": self.energy,
            "inventory": self.inventory
        }


class WorldState(Base):
    """
    Global state of the island environment.
    Tracks day count, weather, and shared resources.
    """
    __tablename__ = "world_state"

    id = Column(Integer, primary_key=True, index=True)
    day_count = Column(Integer, default=1)
    weather = Column(String(20), default="Sunny")
    resource_level = Column(Integer, default=100)

    def __repr__(self):
        return f"<WorldState Day={self.day_count} Weather={self.weather} Resources={self.resource_level}>"

    def to_dict(self):
        """Convert to dictionary for JSON serialization."""
        return {
            "day_count": self.day_count,
            "weather": self.weather,
            "resource_level": self.resource_level
        }
