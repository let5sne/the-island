"""
LLM Service - Agent Brain Module.
Provides AI-powered responses for agents using OpenAI's API.
"""

import logging
import os
import random
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .models import Agent

logger = logging.getLogger(__name__)

# Mock responses for development without API key
MOCK_REACTIONS = {
    "feed": [
        "Oh! Finally some food! Thank you stranger!",
        "Mmm, that's delicious! I was starving!",
        "You're too kind! My energy is back!",
        "Food! Glorious food! I love you!",
    ],
    "idle_sunny": [
        "What a beautiful day on this island...",
        "The sun feels nice, but I'm getting hungry.",
        "I wonder if rescue will ever come...",
        "At least the weather is good today.",
    ],
    "idle_rainy": [
        "This rain is so depressing...",
        "I hope the storm passes soon.",
        "Getting wet and cold out here...",
        "Rain again? Just my luck.",
    ],
    "idle_starving": [
        "I'm so hungry... I can barely stand...",
        "Someone please... I need food...",
        "My stomach is eating itself...",
        "Is this how it ends? Starving on a beach?",
    ],
}


class LLMService:
    """
    Service for generating AI-powered agent reactions.
    Falls back to mock responses if API key is not configured.
    """

    def __init__(self) -> None:
        """Initialize the LLM service with OpenAI client or mock mode."""
        self._api_key = os.environ.get("OPENAI_API_KEY")
        self._client = None
        self._mock_mode = False

        if not self._api_key:
            logger.warning(
                "OPENAI_API_KEY not found in environment. "
                "LLMService running in MOCK mode - using predefined responses."
            )
            self._mock_mode = True
        else:
            try:
                from openai import AsyncOpenAI
                self._client = AsyncOpenAI(api_key=self._api_key)
                logger.info("LLMService initialized with OpenAI API")
            except ImportError:
                logger.error("openai package not installed. Running in MOCK mode.")
                self._mock_mode = True
            except Exception as e:
                logger.error(f"Failed to initialize OpenAI client: {e}. Running in MOCK mode.")
                self._mock_mode = True

    @property
    def is_mock_mode(self) -> bool:
        """Check if service is running in mock mode."""
        return self._mock_mode

    def _get_mock_response(self, event_type: str = "feed") -> str:
        """Get a random mock response for testing without API."""
        responses = MOCK_REACTIONS.get(event_type, MOCK_REACTIONS["feed"])
        return random.choice(responses)

    async def generate_reaction(
        self,
        agent: "Agent",
        event_description: str,
        event_type: str = "feed"
    ) -> str:
        """
        Generate an AI reaction for an agent based on an event.

        Args:
            agent: The Agent model instance
            event_description: Description of what happened (e.g., "User X gave you food")
            event_type: Type of event for mock mode categorization

        Returns:
            A first-person verbal response from the agent
        """
        if self._mock_mode:
            return self._get_mock_response(event_type)

        try:
            system_prompt = (
                f"You are {agent.name}. "
                f"Personality: {agent.personality}. "
                f"Current Status: HP={agent.hp}, Energy={agent.energy}. "
                f"You live on a survival island. "
                f"React to the following event briefly (under 20 words). "
                f"Respond in first person, as if speaking out loud."
            )

            response = await self._client.chat.completions.create(
                model="gpt-3.5-turbo",
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": event_description}
                ],
                max_tokens=50,
                temperature=0.8,
            )

            return response.choices[0].message.content.strip()

        except Exception as e:
            logger.error(f"LLM API error: {e}")
            return self._get_mock_response(event_type)

    async def generate_idle_chat(
        self,
        agent: "Agent",
        weather: str = "Sunny"
    ) -> str:
        """
        Generate idle chatter for an agent based on current conditions.

        Args:
            agent: The Agent model instance
            weather: Current weather condition

        Returns:
            A spontaneous thought or comment from the agent
        """
        # Determine event type for mock responses
        if agent.energy <= 20:
            event_type = "idle_starving"
        elif weather.lower() in ["rainy", "stormy"]:
            event_type = "idle_rainy"
        else:
            event_type = "idle_sunny"

        if self._mock_mode:
            return self._get_mock_response(event_type)

        try:
            system_prompt = (
                f"You are {agent.name}. "
                f"Personality: {agent.personality}. "
                f"Current Status: HP={agent.hp}, Energy={agent.energy}. "
                f"You are stranded on a survival island. "
                f"The weather is {weather}. "
                f"Say something brief (under 15 words) about your situation or thoughts. "
                f"Speak naturally, as if talking to yourself or nearby survivors."
            )

            response = await self._client.chat.completions.create(
                model="gpt-3.5-turbo",
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": "What are you thinking right now?"}
                ],
                max_tokens=40,
                temperature=0.9,
            )

            return response.choices[0].message.content.strip()

        except Exception as e:
            logger.error(f"LLM API error for idle chat: {e}")
            return self._get_mock_response(event_type)


# Global instance for easy import
llm_service = LLMService()
