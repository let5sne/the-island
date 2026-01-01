
import logging
import random
from typing import List, Optional
from datetime import datetime

from .database import get_db_session
from .models import Agent, AgentMemory

logger = logging.getLogger(__name__)

class MemoryService:
    """
    Manages long-term memories for agents.
    Responsible for:
    1. Storing new memories.
    2. Retrieving relevant memories for context.
    3. Pruning/Summarizing old memories (future).
    """

    def __init__(self):
        pass

    async def add_memory(self, agent_id: int, description: str, importance: int = 1, 
                         related_entity_id: int = None, related_entity_name: str = None, 
                         memory_type: str = "general") -> AgentMemory:
        """
        Record a new memory for an agent.
        """
        with get_db_session() as db:
            memory = AgentMemory(
                agent_id=agent_id,
                description=description,
                importance=importance,
                related_entity_id=related_entity_id,
                related_entity_name=related_entity_name,
                memory_type=memory_type
            )
            db.add(memory)
            db.commit() # Ensure ID is generated
            db.refresh(memory)
            logger.info(f"Agent {agent_id} remembered: {description} (Imp: {importance})")
            return memory

    async def get_relevant_memories(self, agent_id: int, context: str, limit: int = 3) -> List[str]:
        """
        Retrieve memories relevant to the current context.
        For MVP, we just return the most recent high-importance memories 
        and any memories related to the entities in context.
        """
        memories = []
        with get_db_session() as db:
            # 1. Get recent important memories (Short-term / working memory)
            recent_memories = db.query(AgentMemory).filter(
                AgentMemory.agent_id == agent_id,
                AgentMemory.importance >= 5
            ).order_by(AgentMemory.created_at.desc()).limit(limit).all()
            
            # 2. Get entity-specific memories (e.g. if talking to "User1")
            # Simple keyword matching for now (Vector DB is Phase 14+)
            entity_memories = []
            if context:
                # Naive search for names in context
                # In real prod, use embeddings.
                search_term = f"%{context}%" # Very naive
                # Let's just fallback to recent for MVP to ensure stability
            
            for mem in recent_memories:
                memories.append(f"- {mem.description}")
        
        return memories

# Global instance
memory_service = MemoryService()
