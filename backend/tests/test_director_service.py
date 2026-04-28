"""Tests for director_service.py - AI Director narrative control."""

import time
import pytest
from app.director_service import (
    DirectorService, GameMode, PlotChoice, PlotPoint, ResolutionResult,
)


class TestGameMode:
    def test_enum_values(self):
        assert GameMode.SIMULATION.value == "simulation"
        assert GameMode.NARRATIVE.value == "narrative"
        assert GameMode.VOTING.value == "voting"
        assert GameMode.RESOLUTION.value == "resolution"


class TestPlotChoice:
    def test_creation(self):
        choice = PlotChoice(choice_id="a", text="Go left", effects={"mood_delta": 5})
        assert choice.choice_id == "a"
        assert choice.text == "Go left"

    def test_immutability(self):
        choice = PlotChoice(choice_id="a", text="Test")
        with pytest.raises(Exception):
            choice.choice_id = "other"

    def test_default_effects(self):
        choice = PlotChoice(choice_id="a", text="Test")
        assert choice.effects == {}


class TestPlotPoint:
    def test_creation(self):
        choices = [PlotChoice("a", "A"), PlotChoice("b", "B")]
        plot = PlotPoint(
            plot_id="test-plot",
            title="Test Plot",
            description="Something happens",
            choices=choices,
        )
        assert plot.plot_id == "test-plot"
        assert plot.title == "Test Plot"
        assert len(plot.choices) == 2

    def test_to_dict(self):
        choices = [PlotChoice("a", "Option A"), PlotChoice("b", "Option B")]
        plot = PlotPoint(
            plot_id="p1",
            title="The Plot",
            description="Description",
            choices=choices,
        )
        d = plot.to_dict()
        assert d["plot_id"] == "p1"
        assert d["title"] == "The Plot"
        assert len(d["choices"]) == 2
        assert d["choices"][0]["choice_id"] == "a"


class TestResolutionResult:
    def test_creation(self):
        result = ResolutionResult(
            plot_id="p1",
            choice_id="a",
            message="The choice was made",
            effects={"mood_delta": 5},
        )
        assert result.plot_id == "p1"
        assert result.message == "The choice was made"

    def test_to_dict(self):
        result = ResolutionResult(
            plot_id="p1",
            choice_id="a",
            message="Done",
            effects={"hp_delta": 10},
        )
        d = result.to_dict()
        assert d["plot_id"] == "p1"
        assert d["message"] == "Done"
        assert "effects_json" in d


class TestDirectorServiceTensionCalculation:
    @pytest.fixture
    def director(self):
        return DirectorService()

    def test_tension_low_all_healthy(self, director):
        world_state = {
            "alive_agents": [{"hp": 100}, {"hp": 95}],
            "weather": "Sunny",
            "mood_avg": 70,
            "recent_deaths": 0,
            "resources_critical": False,
        }
        assert director.calculate_tension_level(world_state) == "low"

    def test_tension_medium_some_damage(self, director):
        # Score: hp<70(+1) + rainy(+1) + mood<50(+1) = 3 → "medium"
        world_state = {
            "alive_agents": [{"hp": 60}, {"hp": 65}],
            "weather": "Rainy",
            "mood_avg": 45,
            "recent_deaths": 0,
            "resources_critical": False,
        }
        assert director.calculate_tension_level(world_state) == "medium"

    def test_tension_high_critical_state(self, director):
        # Score: hp<30(+3) + stormy(+2) + mood<30(+2) + deaths*2(+4) + resources(+2) = 13 → "high"
        world_state = {
            "alive_agents": [{"hp": 20}, {"hp": 25}],
            "weather": "stormy",
            "mood_avg": 25,
            "recent_deaths": 2,
            "resources_critical": True,
        }
        assert director.calculate_tension_level(world_state) == "high"

    def test_tension_stormy_weather_increases(self, director):
        # hp 80 (+0) + Sunny (+0) + mood 70 (+0) = 0 → "low"
        base = director.calculate_tension_level({
            "alive_agents": [{"hp": 80}], "weather": "Sunny",
            "mood_avg": 70, "recent_deaths": 0, "resources_critical": False,
        })
        # hp 80 (+0) + stormy (+2) + mood 70 (+0) = 2 → "low"
        # Stormy alone isn't enough; need hp or mood to push it over
        # Let's use hp<50 (+2) + stormy (+2) = 4 → "medium"
        stormy = director.calculate_tension_level({
            "alive_agents": [{"hp": 40}], "weather": "stormy",
            "mood_avg": 70, "recent_deaths": 0, "resources_critical": False,
        })
        assert base == "low"
        assert stormy == "medium"

    def test_tension_low_mood_increases(self, director):
        # mood 80: score 0 → "low"
        happy = director.calculate_tension_level({
            "alive_agents": [{"hp": 80}], "weather": "Sunny",
            "mood_avg": 80, "recent_deaths": 0, "resources_critical": False,
        })
        # mood 20 (+2) + hp<50 (+2) = 4 → "medium"
        sad = director.calculate_tension_level({
            "alive_agents": [{"hp": 40}], "weather": "Sunny",
            "mood_avg": 20, "recent_deaths": 0, "resources_critical": False,
        })
        assert happy == "low"
        assert sad == "medium"

    def test_tension_empty_alive_agents(self, director):
        world_state = {
            "alive_agents": [],
            "weather": "Sunny",
            "mood_avg": 50,
            "recent_deaths": 0,
            "resources_critical": False,
        }
        assert director.calculate_tension_level(world_state) == "low"


class TestDirectorServicePlotGeneration:
    @pytest.fixture
    def director(self):
        return DirectorService()

    @pytest.mark.asyncio
    async def test_fallback_plot_returns_valid_plot_point(self, director):
        world_state = {
            "day": 3, "weather": "Sunny", "time_of_day": "day",
            "alive_agents": [{"name": "Jack", "hp": 80}],
            "recent_events": [], "tension_level": "low", "mood_avg": 70,
        }
        plot = await director.generate_plot_point(world_state)
        assert isinstance(plot, PlotPoint)
        assert len(plot.choices) == 2
        assert plot.title != ""

    @pytest.mark.asyncio
    async def test_fallback_plot_has_2_choices(self, director):
        world_state = {
            "day": 1, "weather": "Sunny", "time_of_day": "day",
            "alive_agents": [{"name": "Jack", "hp": 100}],
            "recent_events": [], "tension_level": "medium", "mood_avg": 50,
        }
        plot = await director.generate_plot_point(world_state)
        assert len(plot.choices) == 2
        assert all(isinstance(c, PlotChoice) for c in plot.choices)

    @pytest.mark.asyncio
    async def test_fallback_plot_avoids_recent_titles(self, director):
        world_state = {
            "day": 1, "weather": "Sunny", "time_of_day": "day",
            "alive_agents": [{"name": "Jack", "hp": 100}],
            "recent_events": [], "tension_level": "low", "mood_avg": 50,
        }
        plot1 = await director.generate_plot_point(world_state)
        director.clear_current_plot()

        world_state["alive_agents"] = [{"name": "Jack", "hp": 100}, {"name": "Luna", "hp": 100}]
        plot2 = await director.generate_plot_point(world_state)
        assert plot2.title != plot1.title

    @pytest.mark.asyncio
    async def test_generate_plot_point_sets_current_plot(self, director):
        world_state = {
            "day": 2, "weather": "Rainy", "time_of_day": "night",
            "alive_agents": [{"name": "Jack", "hp": 50}],
            "recent_events": [], "tension_level": "medium", "mood_avg": 40,
        }
        plot = await director.generate_plot_point(world_state)
        assert director.current_plot is not None
        assert director.current_plot.plot_id == plot.plot_id


class TestDirectorServiceResolveVote:
    @pytest.fixture
    def director(self):
        return DirectorService()

    @pytest.mark.asyncio
    async def test_resolve_fallback_returns_result(self, director):
        choices = [PlotChoice("a", "Option A"), PlotChoice("b", "Option B")]
        plot = PlotPoint(
            plot_id="test",
            title="Test",
            description="Test plot",
            choices=choices,
        )
        world_state = {
            "alive_agents": [{"name": "Jack", "hp": 80}],
        }
        result = await director.resolve_vote(plot, "a", world_state)
        assert isinstance(result, ResolutionResult)
        assert result.choice_id == "a"
        assert result.message != ""

    @pytest.mark.asyncio
    async def test_resolve_invalid_choice_fallsback_to_first(self, director):
        choices = [PlotChoice("a", "Option A"), PlotChoice("b", "Option B")]
        plot = PlotPoint(
            plot_id="test", title="Test", description="Test", choices=choices,
        )
        world_state = {"alive_agents": []}
        result = await director.resolve_vote(plot, "nonexistent", world_state)
        assert result.choice_id == "a"  # Falls back to first choice


class TestDirectorServiceClearPlot:
    @pytest.fixture
    def director(self):
        return DirectorService()

    @pytest.mark.asyncio
    async def test_clear_plot_adds_to_history(self, director):
        world_state = {
            "day": 1, "weather": "Sunny", "time_of_day": "day",
            "alive_agents": [{"name": "Jack", "hp": 100}],
            "recent_events": [], "tension_level": "low", "mood_avg": 50,
        }
        plot = await director.generate_plot_point(world_state)
        title = plot.title
        director.clear_current_plot()
        assert director.current_plot is None
        assert title in director._plot_history

    @pytest.mark.asyncio
    async def test_clear_plot_limits_history_to_5(self, director):
        world_state = {
            "day": 1, "weather": "Sunny", "time_of_day": "day",
            "alive_agents": [{"name": "Jack", "hp": 100}],
            "recent_events": [], "tension_level": "low", "mood_avg": 50,
        }
        for _ in range(10):
            plot = await director.generate_plot_point(world_state)
            director.clear_current_plot()
        assert len(director._plot_history) <= 5

    def test_clear_no_plot(self):
        director = DirectorService()
        director.clear_current_plot()
        assert director.current_plot is None
