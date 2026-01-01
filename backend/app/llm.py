"""
LLM Service - Agent Brain Module.
Provides AI-powered responses for agents using LiteLLM (supports multiple providers).

Supported providers (via environment variables):
- OpenAI: OPENAI_API_KEY → model="gpt-3.5-turbo" or "gpt-4"
- Anthropic: ANTHROPIC_API_KEY → model="claude-3-haiku-20240307" or "claude-3-sonnet-20240229"
- Google: GEMINI_API_KEY → model="gemini/gemini-pro"
- Azure OpenAI: AZURE_API_KEY + AZURE_API_BASE → model="azure/<deployment-name>"
- OpenRouter: OPENROUTER_API_KEY → model="openrouter/<model>"
- Ollama (local): OLLAMA_API_BASE → model="ollama/llama2"
- Custom/Self-hosted: LLM_API_KEY + LLM_API_BASE → any OpenAI-compatible endpoint
- And 100+ more providers...

Configuration:
- LLM_MODEL: Model to use (default: gpt-3.5-turbo)
- LLM_API_BASE: Custom API base URL (for self-hosted or proxy services)
- LLM_API_KEY: Generic API key (used with LLM_API_BASE)
- LLM_API_KEY_HEADER: Custom header name for API key (default: none, uses provider default)
- LLM_MOCK_MODE: Set to "true" to force mock mode
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

# Default model configuration
DEFAULT_MODEL = "gpt-3.5-turbo"


class LLMService:
    """
    Service for generating AI-powered agent reactions using LiteLLM.
    Supports multiple LLM providers through a unified interface.
    Falls back to mock responses if no API key is configured.
    """

    def __init__(self) -> None:
        """Initialize the LLM service with LiteLLM or mock mode."""
        self._model = os.environ.get("LLM_MODEL", DEFAULT_MODEL)
        self._api_base = os.environ.get("LLM_API_BASE")  # Custom base URL
        self._api_key = os.environ.get("LLM_API_KEY")    # Generic API key
        self._api_key_header = os.environ.get("LLM_API_KEY_HEADER")  # Custom header name
        self._mock_mode = os.environ.get("LLM_MOCK_MODE", "").lower() == "true"
        self._acompletion = None
        self._extra_headers = {}

        # Build extra headers if custom API key header is specified
        if self._api_key_header and self._api_key:
            self._extra_headers[self._api_key_header] = self._api_key
            logger.info(f"Using custom API key header: {self._api_key_header}")

            # LiteLLM requires provider-specific API key env var to pass validation
            # Set it to satisfy the check (actual auth uses extra_headers)
            if self._model.startswith("anthropic/"):
                os.environ.setdefault("ANTHROPIC_API_KEY", self._api_key)
            elif self._model.startswith("openai/"):
                os.environ.setdefault("OPENAI_API_KEY", self._api_key)

        if self._mock_mode:
            logger.info("LLMService running in MOCK mode (forced by LLM_MOCK_MODE)")
            return

        # Check for any supported API key (order matters for provider detection)
        api_keys = {
            "OPENAI_API_KEY": "OpenAI",
            "ANTHROPIC_API_KEY": "Anthropic",
            "GEMINI_API_KEY": "Google Gemini",
            "AZURE_API_KEY": "Azure OpenAI",
            "AZURE_API_BASE": "Azure OpenAI",
            "OPENROUTER_API_KEY": "OpenRouter",
            "COHERE_API_KEY": "Cohere",
            "HUGGINGFACE_API_KEY": "HuggingFace",
            "OLLAMA_API_BASE": "Ollama (local)",
            "LLM_API_KEY": "Custom (with LLM_API_BASE)",
            "LLM_API_BASE": "Custom endpoint",
        }

        found_provider = None
        for key, provider in api_keys.items():
            if os.environ.get(key):
                found_provider = provider
                break

        if not found_provider:
            logger.warning(
                "No LLM API key found in environment. "
                "LLMService running in MOCK mode - using predefined responses. "
                f"Supported keys: {', '.join(api_keys.keys())}"
            )
            self._mock_mode = True
            return

        try:
            from litellm import acompletion
            self._acompletion = acompletion

            # Log configuration details
            config_info = f"provider: {found_provider}, model: {self._model}"
            if self._api_base:
                config_info += f", api_base: {self._api_base}"
            logger.info(f"LLMService initialized with LiteLLM ({config_info})")
        except ImportError:
            logger.error("litellm package not installed. Running in MOCK mode.")
            self._mock_mode = True
        except Exception as e:
            logger.error(f"Failed to initialize LiteLLM: {e}. Running in MOCK mode.")
            self._mock_mode = True

    @property
    def is_mock_mode(self) -> bool:
        """Check if service is running in mock mode."""
        return self._mock_mode

    @property
    def model(self) -> str:
        """Get the current model name."""
        return self._model

    @property
    def api_base(self) -> str | None:
        """Get the custom API base URL if configured."""
        return self._api_base

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

            # Build kwargs for acompletion
            kwargs = {
                "model": self._model,
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": event_description}
                ],
                "max_tokens": 50,
                "temperature": 0.8,
            }
            if self._api_base:
                kwargs["api_base"] = self._api_base
            if self._api_key and not self._api_key_header:
                # Only pass api_key if not using custom header
                kwargs["api_key"] = self._api_key
            if self._extra_headers:
                kwargs["extra_headers"] = self._extra_headers

            response = await self._acompletion(**kwargs)

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

            # Build kwargs for acompletion
            kwargs = {
                "model": self._model,
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": "What are you thinking right now?"}
                ],
                "max_tokens": 40,
                "temperature": 0.9,
            }
            if self._api_base:
                kwargs["api_base"] = self._api_base
            if self._api_key and not self._api_key_header:
                # Only pass api_key if not using custom header
                kwargs["api_key"] = self._api_key
            if self._extra_headers:
                kwargs["extra_headers"] = self._extra_headers

            response = await self._acompletion(**kwargs)

            return response.choices[0].message.content.strip()

        except Exception as e:
            logger.error(f"LLM API error for idle chat: {e}")
            return self._get_mock_response(event_type)


# Global instance for easy import
llm_service = LLMService()
