"""
Agent classes for processing game inputs and generating responses.
Provides base abstraction and rule-based implementation.
"""

from abc import ABC, abstractmethod
import logging

logger = logging.getLogger(__name__)


class BaseAgent(ABC):
    """
    Abstract base class for all game agents.

    Agents process input text and generate appropriate responses.
    Subclasses must implement the process_input method.
    """

    def __init__(self, name: str = "BaseAgent") -> None:
        """
        Initialize the agent.

        Args:
            name: Display name for the agent
        """
        self.name = name

    @abstractmethod
    def process_input(self, text: str) -> str:
        """
        Process input text and return a response.

        Args:
            text: The input text to process

        Returns:
            The agent's response string
        """
        pass


class RuleBasedAgent(BaseAgent):
    """
    A simple rule-based agent that responds based on keyword matching.

    This agent does not use LLMs - it returns static strings based on
    detected keywords in the input text.
    """

    def __init__(self, name: str = "RuleBot") -> None:
        """Initialize the rule-based agent."""
        super().__init__(name)
        self._rules: dict[str, str] = {
            "attack": "Defending! Shield activated!",
            "heal": "Healing spell cast! +50 HP",
            "run": "Too slow! You can't escape!",
            "help": "Allies are on the way!",
            "fire": "Water shield deployed!",
            "magic": "Counter-spell activated!",
        }
        self._default_response = "I heard you! Processing command..."

    def process_input(self, text: str) -> str:
        """
        Process input and return response based on keyword rules.

        Args:
            text: The input text to analyze

        Returns:
            Response string based on matched keywords
        """
        text_lower = text.lower()

        for keyword, response in self._rules.items():
            if keyword in text_lower:
                logger.info(f"Agent matched keyword: {keyword}")
                return f"{self.name}: {response}"

        logger.info("Agent using default response")
        return f"{self.name}: {self._default_response}"
