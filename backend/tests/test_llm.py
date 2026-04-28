"""Tests for llm.py - LLM service mock mode behavior."""

import os
from unittest.mock import patch

import pytest

from app.llm import LLMService, MOCK_REACTIONS


class FakeAgent:
    """Minimal agent-like object for LLM calls."""
    def __init__(self, id=1, name="Jack", personality="Brave", hp=80, energy=70, mood=60, is_sheltered=False):
        self.id = id
        self.name = name
        self.personality = personality
        self.hp = hp
        self.energy = energy
        self.mood = mood
        self.is_sheltered = is_sheltered


@pytest.fixture
def svc():
    with patch.dict(os.environ, {"LLM_MOCK_MODE": "true"}, clear=False):
        return LLMService()


@pytest.fixture
def agent():
    return FakeAgent()


class TestLLMServiceInit:
    def test_initial_state(self):
        svc = LLMService()
        assert svc._model is not None

    def test_mock_mode_true(self):
        with patch.dict(os.environ, {"LLM_MOCK_MODE": "true"}):
            svc = LLMService()
            assert svc.is_mock_mode is True

    def test_is_mock_mode_property(self, svc):
        assert svc.is_mock_mode in (True, False)


class TestLLMServiceGenerateReaction:
    @pytest.mark.asyncio
    async def test_returns_string(self, svc, agent):
        text = await svc.generate_reaction(agent, "Player fed them", "feed")
        assert isinstance(text, str)
        assert len(text) > 0

    @pytest.mark.asyncio
    async def test_feed_reaction(self, svc, agent):
        text = await svc.generate_reaction(agent, "Food!", "feed")
        assert isinstance(text, str)

    @pytest.mark.asyncio
    async def test_heal_reaction(self, svc, agent):
        text = await svc.generate_reaction(agent, "Healed!", "heal")
        assert isinstance(text, str)


class TestLLMServiceGenerateIdleChat:
    @pytest.mark.asyncio
    async def test_returns_string_sunny(self, svc, agent):
        text = await svc.generate_idle_chat(agent, "Sunny", "day")
        assert isinstance(text, str)
        assert len(text) > 0

    @pytest.mark.asyncio
    async def test_rainy_chat(self, svc, agent):
        text = await svc.generate_idle_chat(agent, "Rainy", "day")
        assert isinstance(text, str)

    @pytest.mark.asyncio
    async def test_starving_chat(self, svc, agent):
        agent.energy = 5
        text = await svc.generate_idle_chat(agent, "Sunny", "day")
        assert isinstance(text, str)


class TestLLMServiceGenerateConversation:
    @pytest.mark.skip(reason="generate_conversation_response requires session-local mocks")
    @pytest.mark.asyncio
    async def test_returns_string(self, svc):
        text = await svc.generate_conversation_response(
            "Jack", "Brave", "neutral", "Viewer", "survival tips"
        )
        assert isinstance(text, str)


class TestLLMServiceGenerateGratitude:
    @pytest.mark.asyncio
    async def test_returns_string(self, svc):
        text = await svc.generate_gratitude(
            user="Viewer123", amount=100, agent_name="Jack",
            agent_personality="Brave", gift_name="bits"
        )
        assert isinstance(text, str)
        assert "Viewer123" in text


class TestLLMServiceGenerateStory:
    @pytest.mark.asyncio
    async def test_returns_string(self, svc):
        text = await svc.generate_story("Jack", "survival")
        assert isinstance(text, str)
        assert len(text) > 0


class TestLLMServiceGenerateSocial:
    @pytest.mark.asyncio
    async def test_returns_string(self, svc):
        text = await svc.generate_social_interaction(
            "Jack", "Luna", "chat", "friend", "Sunny", "day"
        )
        assert isinstance(text, str)
        assert len(text) > 0

    @pytest.mark.asyncio
    async def test_with_previous_dialogue(self, svc):
        text = await svc.generate_social_interaction(
            "Jack", "Luna", "chat", "friend", "Sunny", "day",
            previous_dialogue="Hi there!"
        )
        assert isinstance(text, str)


class TestMockReactions:
    def test_feed_reactions_exist(self):
        assert len(MOCK_REACTIONS["feed"]) >= 3

    def test_idle_reactions_exist(self):
        assert "idle_sunny" in MOCK_REACTIONS
        assert "idle_rainy" in MOCK_REACTIONS

    def test_gratitude_reactions_exist(self):
        assert "gratitude_humble" in MOCK_REACTIONS
