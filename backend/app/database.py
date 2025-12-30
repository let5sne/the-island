"""
Database configuration for The Island.
SQLite + SQLAlchemy ORM setup.
"""

from pathlib import Path
from contextlib import contextmanager

from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker, declarative_base

# Database file path (in project root)
DB_PATH = Path(__file__).parent.parent.parent / "island.db"
DATABASE_URL = f"sqlite:///{DB_PATH}"

# Create engine with SQLite optimizations
engine = create_engine(
    DATABASE_URL,
    connect_args={"check_same_thread": False},  # Required for SQLite with FastAPI
    echo=False  # Set True for SQL debugging
)

# Session factory
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

# Base class for ORM models
Base = declarative_base()


def init_db():
    """Initialize database tables."""
    Base.metadata.create_all(bind=engine)


def get_db():
    """
    Dependency for getting database session.
    Use with FastAPI's Depends() or as context manager.
    """
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()


@contextmanager
def get_db_session():
    """
    Context manager for database session.
    Usage: with get_db_session() as db: ...
    """
    db = SessionLocal()
    try:
        yield db
        db.commit()
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()
