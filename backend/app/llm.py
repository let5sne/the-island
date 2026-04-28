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
    from .memory_service import MemoryService

from .memory_service import memory_service

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
    "gratitude_arrogant": [
        "Finally! A worthy tribute! {user}, you understand greatness!",
        "About time someone recognized my value! Thanks, {user}!",
        "Hmph, {user} knows quality when they see it. Much appreciated!",
        "A gift for ME? Well, obviously. Thank you, {user}!",
    ],
    "gratitude_humble": [
        "Oh my gosh, {user}! You're too kind! Thank you so much!",
        "Wow, {user}, I don't deserve this! You're amazing!",
        "*tears up* {user}... this means the world to me!",
        "Thank you, thank you {user}! You're the best!",
    ],
    "gratitude_neutral": [
        "Hey, thanks {user}! That's really generous of you!",
        "Wow, {user}! Thank you so much for the support!",
        "Appreciate it, {user}! You're awesome!",
        "{user}, you're a legend! Thank you!",
    ],
    # Personality-specific idle barks
    "hotheaded_idle": [
        "Someone better not cross me today...",
        "I'm not sharing ANYTHING. Got it?",
        "This island is MINE. Everyone else can leave.",
    ],
    "manipulative_idle": [
        "Let's see... who can I use today?",
        "A little flattery goes a long way...",
        "Keep your friends close, and your enemies closer.",
    ],
    "saintly_idle": [
        "I hope everyone is okay... maybe I can help.",
        "Does anyone need food? I can share mine.",
        "Kindness costs nothing. Let's work together!",
    ],
    "deceptive_idle": [
        "Trust me, I know what I'm doing... *wink*",
        "I didn't take your herbs. Why would you think that?",
        "A little lie never hurt anyone. Much.",
    ],
    # Rumor reactions - AI responds to "风声"
    "rumor_suspect": [
        "What?! Someone said THAT about me?",
        "That's a lie! Who's spreading rumors?",
        "I can't believe people would say that...",
    ],
    "rumor_trust_shift": [
        "Hmm, I should be more careful around {target}...",
        "I knew {target} couldn't be trusted!",
        "Maybe {target} isn't so bad after all.",
    ],
    # Pardon reactions
    "pardon_plea": [
        "Please! Don't send me away! I'll do anything!",
        "Wait! I don't deserve this! Someone, help!",
        "I'm begging you... isn't there anyone who cares?!",
        "*sobbing* Please... I have so much more to give...",
    ],
    "pardon_gratitude": [
        "Thank you, {user}! You saved my life! I'll NEVER forget this!",
        "{user}... you're my hero. I'm yours forever.",
        "I owe you everything, {user}. Command me as you wish!",
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
            # Retrieve relevant memories
            memories = await memory_service.get_relevant_memories(agent.id, event_description)
            memory_context = "\n".join(memories) if memories else "No relevant memories."

            system_prompt = (
                f"You are {agent.name}. "
                f"Personality: {agent.personality}. "
                f"Current Status: HP={agent.hp}, Energy={agent.energy}. "
                f"Shelter Status: {'Under shelter (safe from weather)' if agent.is_sheltered else 'Exposed (vulnerable to weather)'}. "
                f"You are a land creature on a survival island. You have a natural instinct to stay on the dry sand and avoid the deep ocean. "
                f"Relevant Memories:\n{memory_context}\n"
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
        weather: str = "Sunny",
        time_of_day: str = "day"
    ) -> str:
        """
        Generate idle chatter for an agent based on current conditions.

        Args:
            agent: The Agent model instance
            weather: Current weather condition
            time_of_day: Current time of day (dawn/day/dusk/night)

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
                f"Shelter Status: {'Under shelter (protected)' if agent.is_sheltered else 'Exposed to elements'}. "
                f"You are a land creature stranded on a survival island beach. You feel safer on dry land than near the waves. "
                f"It is currently {time_of_day} and the weather is {weather}. "
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

    async def generate_conversation_response(
        self,
        agent_name: str,
        agent_personality: str,
        agent_mood: int,
        username: str,
        topic: str = "just chatting"
    ) -> str:
        """
        Generate a conversation response when a user talks to an agent.

        Args:
            agent_name: Name of the agent
            agent_personality: Agent's personality trait
            agent_mood: Agent's current mood (0-100)
            username: Name of the user talking to the agent
            topic: Topic of conversation

        Returns:
            Agent's response to the user
        """
        if self._mock_mode:
            mood_state = "happy" if agent_mood >= 70 else "neutral" if agent_mood >= 40 else "sad"
            responses = {
                "happy": [
                    f"Hey {username}! Great to see a friendly face!",
                    f"Oh, you want to chat? I'm in a good mood today!",
                    f"Nice of you to talk to me, {username}!",
                ],
                "neutral": [
                    f"Oh, hi {username}. What's on your mind?",
                    f"Sure, I can chat for a bit.",
                    f"What do you want to talk about?",
                ],
                "sad": [
                    f"*sighs* Oh... hey {username}...",
                    f"I'm not really in the mood, but... okay.",
                    f"What is it, {username}?",
                ]
            }
            return random.choice(responses.get(mood_state, responses["neutral"]))

        try:
            mood_desc = "happy and energetic" if agent_mood >= 70 else \
                       "calm and neutral" if agent_mood >= 40 else \
                       "a bit down" if agent_mood >= 20 else "anxious and worried"

            # Retrieve relevant memories
            memories = await memory_service.get_relevant_memories(agent.id, topic)
            memory_context = "\n".join(memories) if memories else "No relevant memories."

            system_prompt = (
                f"You are {agent_name}, a survivor on a deserted island. "
                f"Personality: {agent_personality}. "
                f"Current mood: {mood_desc} (mood level: {agent_mood}/100). "
                f"Relevant Memories:\n{memory_context}\n"
                f"A viewer named {username} wants to chat with you. "
                f"Respond naturally in character (under 30 words). "
                f"Be conversational and show your personality."
            )

            user_msg = f"{username} says: {topic}" if topic != "just chatting" else \
                      f"{username} wants to chat with you."

            kwargs = {
                "model": self._model,
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": user_msg}
                ],
                "max_tokens": 80,
                "temperature": 0.85,
            }
            if self._api_base:
                kwargs["api_base"] = self._api_base
            if self._api_key and not self._api_key_header:
                kwargs["api_key"] = self._api_key
            if self._extra_headers:
                kwargs["extra_headers"] = self._extra_headers

            response = await self._acompletion(**kwargs)
            return response.choices[0].message.content.strip()

        except Exception as e:
            logger.error(f"LLM API error for conversation: {e}")
            return f"*nods at {username}* Hey there."

    async def generate_social_interaction(
        self,
        initiator_name: str,
        target_name: str,
        interaction_type: str,
        relationship_type: str,
        weather: str = "Sunny",
        time_of_day: str = "day",
        previous_dialogue: str = None
    ) -> str:
        """
        Generate dialogue for social interaction between two agents.

        Args:
            initiator_name: Name of the agent initiating interaction
            target_name: Name of the target agent
            interaction_type: Type of interaction (chat, share_food, help, argue, comfort)
            relationship_type: Current relationship (stranger, acquaintance, friend, etc.)
            weather: Current weather
            time_of_day: Current time of day

        Returns:
            A brief dialogue exchange between the two agents
        """
        if self._mock_mode:
            dialogues = {
                "chat": [
                    f"{initiator_name}: Hey {target_name}, how are you holding up?\n{target_name}: Could be better, but I'm managing.",
                    f"{initiator_name}: Nice weather today, huh?\n{target_name}: Yeah, at least something's going right.",
                ],
                "share_food": [
                    f"{initiator_name}: Here, take some of my food.\n{target_name}: Really? Thanks, I appreciate it!",
                    f"{initiator_name}: You look hungry. Have some of this.\n{target_name}: You're a lifesaver!",
                ],
                "help": [
                    f"{initiator_name}: Need a hand with that?\n{target_name}: Yes, thank you so much!",
                    f"{initiator_name}: Let me help you out.\n{target_name}: I owe you one!",
                ],
                "argue": [
                    f"{initiator_name}: This is all your fault!\n{target_name}: My fault? You're the one who-",
                    f"{initiator_name}: I can't believe you did that!\n{target_name}: Just leave me alone!",
                ],
                "comfort": [
                    f"{initiator_name}: Hey, are you okay?\n{target_name}: *sniff* I'll be fine... thanks for asking.",
                    f"{initiator_name}: Don't worry, we'll get through this.\n{target_name}: I hope you're right...",
                ]
            }
            return random.choice(dialogues.get(interaction_type, dialogues["chat"]))

        try:
            relationship_desc = {
                "stranger": "barely know each other",
                "acquaintance": "are getting to know each other",
                "friend": "are friends",
                "close_friend": "are close friends who trust each other",
                "rival": "have tensions between them"
            }.get(relationship_type, "are acquaintances")

            interaction_desc = {
                "chat": "having a casual conversation",
                "share_food": "sharing food with",
                "help": "helping with a task",
                "argue": "having a disagreement with",
                "comfort": "comforting"
            }.get(interaction_type, "talking to")

            system_prompt = (
                f"You are writing dialogue for two survivors on a deserted island. "
                f"{initiator_name} and {target_name} {relationship_desc}. "
                f"It is {time_of_day} and the weather is {weather}. "
                f"{initiator_name} is {interaction_desc} {target_name}. "
            )

            if previous_dialogue:
                 system_prompt += (
                    f"\nCONTEXT: {target_name} just said: '{previous_dialogue}'\n"
                    f"Write a response from {initiator_name} to {target_name}. "
                    f"Format: '{initiator_name}: [response]'"
                 )
            else:
                 system_prompt += (
                    f"\nWrite a brief opening dialogue exchange (2-3 lines total). "
                    f"Format: '{initiator_name}: [line]\\n{target_name}: [response]'"
                 )

            kwargs = {
                "model": self._model,
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": f"Write a {interaction_type} dialogue between {initiator_name} and {target_name}."}
                ],
                "max_tokens": 100,
                "temperature": 0.9,
            }
            if self._api_base:
                kwargs["api_base"] = self._api_base
            if self._api_key and not self._api_key_header:
                kwargs["api_key"] = self._api_key
            if self._extra_headers:
                kwargs["extra_headers"] = self._extra_headers

            response = await self._acompletion(**kwargs)
            return response.choices[0].message.content.strip()

        except Exception as e:
            logger.error(f"LLM API error for social interaction: {e}")
            return f"{initiator_name}: ...\n{target_name}: ..."

    async def generate_story(
        self,
        storyteller_name: str,
        topic: str = "ghost_story"
    ) -> str:
        """
        Generate a short story for the campfire.
        """
        if self._mock_mode:
            stories = [
                "Once upon a time, a ship crashed here...",
                "The elders say this island is haunted...",
                "I saw a strange light in the forest yesterday..."
            ]
            return random.choice(stories)
            
        try:
            system_prompt = (
                f"You are {storyteller_name}, a survivor telling a story at a campfire. "
                f"Topic: {topic}. "
                f"Keep it short (2-3 sentences), mysterious, and atmospheric."
            )
            
            kwargs = {
                "model": self._model,
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": "Tell us a story."}
                ],
                "max_tokens": 100,
                "temperature": 1.0, 
            }
            if self._api_base:
                kwargs["api_base"] = self._api_base
            if self._api_key and not self._api_key_header:
                kwargs["api_key"] = self._api_key
            if self._extra_headers:
                kwargs["extra_headers"] = self._extra_headers

            response = await self._acompletion(**kwargs)
            return response.choices[0].message.content.strip()

        except Exception as e:
            logger.error(f"LLM API error for story: {e}")
            return "It was a dark and stormy night..."

    async def generate_gratitude(
        self,
        user: str,
        amount: int,
        agent_name: str = "Survivor",
        agent_personality: str = "friendly",
        gift_name: str = "bits"
    ) -> str:
        """
        Generate a special gratitude response for donations/gifts.

        Args:
            user: Name of the user who gave the gift
            amount: Amount of the gift
            agent_name: Name of the agent (optional)
            agent_personality: Personality of the agent (optional)
            gift_name: Type of gift (bits, subscription, etc.)

        Returns:
            An excited, grateful response from the agent
        """
        personality = agent_personality.lower() if agent_personality else "friendly"


        if self._mock_mode:
            if "arrogant" in personality or "proud" in personality:
                responses = MOCK_REACTIONS.get("gratitude_arrogant", [])
            elif "humble" in personality or "shy" in personality or "kind" in personality:
                responses = MOCK_REACTIONS.get("gratitude_humble", [])
            else:
                responses = MOCK_REACTIONS.get("gratitude_neutral", [])
            
            if responses:
                return random.choice(responses).format(user=user, amount=amount)
            return f"Thank you so much, {user}! You're amazing!"

        try:
            # Customize tone based on personality
            if "arrogant" in personality or "proud" in personality:
                tone_instruction = (
                    "You are somewhat arrogant but still grateful. "
                    "React with confident excitement, like 'Finally, a worthy tribute!' "
                    "but still thank them."
                )
            elif "humble" in personality or "shy" in personality:
                tone_instruction = (
                    "You are humble and easily moved. "
                    "React with overwhelming gratitude, maybe even get teary-eyed."
                )
            else:
                tone_instruction = (
                    "React with genuine excitement and gratitude."
                )

            system_prompt = (
                f"You are {agent_name}, a survivor on a deserted island. "
                f"Personality: {personality if personality else 'friendly'}. "
                f"A wealthy patron named {user} just gave you {amount} {gift_name}! "
                f"{tone_instruction} "
                f"Respond with extreme excitement and gratitude (max 15 words). "
                f"Keep it fun and energetic!"
            )

            kwargs = {
                "model": self._model,
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": f"{user} just gave you {amount} {gift_name}! React!"}
                ],
                "max_tokens": 40,
                "temperature": 0.95,
            }
            if self._api_base:
                kwargs["api_base"] = self._api_base
            if self._api_key and not self._api_key_header:
                kwargs["api_key"] = self._api_key
            if self._extra_headers:
                kwargs["extra_headers"] = self._extra_headers

            response = await self._acompletion(**kwargs)
            return response.choices[0].message.content.strip()

        except Exception as e:
            logger.error(f"LLM API error for gratitude: {e}")
            return f"Wow, thank you so much {user}! You're amazing!"


# Global instance for easy import
llm_service = LLMService()
