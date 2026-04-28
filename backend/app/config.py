"""Game configuration constants for The Island survival simulation."""

import re

# =============================================================================
# Tick timing
# =============================================================================
TICK_INTERVAL: float = 5.0

# =============================================================================
# Survival (base values, modified by difficulty)
# =============================================================================
BASE_ENERGY_DECAY_PER_TICK: int = 2
BASE_HP_DECAY_WHEN_STARVING: int = 5

# =============================================================================
# Command costs and effects
# =============================================================================
FEED_COST: int = 10
FEED_ENERGY_RESTORE: int = 20
HEAL_COST: int = 15
HEAL_HP_RESTORE: int = 30
ENCOURAGE_COST: int = 5
ENCOURAGE_MOOD_BOOST: int = 15
LOVE_COST: int = 5
LOVE_MOOD_BOOST: int = 20
REVIVE_COST: int = 10

INITIAL_USER_GOLD: int = 100
IDLE_CHAT_PROBABILITY: float = 0.15

# =============================================================================
# AI Director & Narrative Voting
# =============================================================================
DIRECTOR_TRIGGER_INTERVAL: int = 60
DIRECTOR_MIN_ALIVE_AGENTS: int = 2
VOTING_DURATION_SECONDS: int = 60
VOTE_BROADCAST_INTERVAL: float = 1.0

# =============================================================================
# Day/Night cycle
# =============================================================================
TICKS_PER_DAY: int = 120

DAY_PHASES: dict[str, tuple[int, int]] = {
    "dawn": (0, 15),
    "day": (16, 75),
    "dusk": (76, 90),
    "night": (91, 119),
}

PHASE_MODIFIERS: dict[str, dict[str, float]] = {
    "dawn": {"energy_decay": 0.8, "hp_recovery": 1, "mood_change": 3},
    "day": {"energy_decay": 1.0, "hp_recovery": 2, "mood_change": 2},
    "dusk": {"energy_decay": 1.2, "hp_recovery": 0, "mood_change": -2},
    "night": {"energy_decay": 1.3, "hp_recovery": 0, "mood_change": -3},
}

# =============================================================================
# Weather system
# =============================================================================
WEATHER_TYPES: dict[str, dict[str, float]] = {
    "Sunny": {"energy_modifier": 1.0, "mood_change": 5},
    "Cloudy": {"energy_modifier": 1.0, "mood_change": 0},
    "Rainy": {"energy_modifier": 1.2, "mood_change": -8},
    "Stormy": {"energy_modifier": 1.4, "mood_change": -15},
    "Hot": {"energy_modifier": 1.3, "mood_change": -5},
    "Foggy": {"energy_modifier": 1.1, "mood_change": -3},
}

WEATHER_TRANSITIONS: dict[str, dict[str, float]] = {
    "Sunny": {"Sunny": 0.5, "Cloudy": 0.3, "Hot": 0.15, "Rainy": 0.05},
    "Cloudy": {"Cloudy": 0.3, "Sunny": 0.3, "Rainy": 0.25, "Foggy": 0.1, "Stormy": 0.05},
    "Rainy": {"Rainy": 0.3, "Cloudy": 0.35, "Stormy": 0.2, "Foggy": 0.15},
    "Stormy": {"Stormy": 0.2, "Rainy": 0.5, "Cloudy": 0.3},
    "Hot": {"Hot": 0.3, "Sunny": 0.5, "Cloudy": 0.15, "Stormy": 0.05},
    "Foggy": {"Foggy": 0.2, "Cloudy": 0.4, "Rainy": 0.25, "Sunny": 0.15},
}

WEATHER_MIN_DURATION: int = 15
WEATHER_MAX_DURATION: int = 35

# =============================================================================
# Social interactions
# =============================================================================
SOCIAL_INTERACTIONS: dict[str, dict] = {
    "chat": {"affection": (1, 4), "trust": (0, 2), "weight": 0.4},
    "share_food": {"affection": (3, 7), "trust": (2, 4), "weight": 0.15, "min_energy": 40},
    "help": {"affection": (4, 8), "trust": (3, 6), "weight": 0.15, "min_energy": 30},
    "comfort": {"affection": (3, 6), "trust": (2, 4), "weight": 0.2, "target_max_mood": 40},
    "argue": {"affection": (-8, -3), "trust": (-5, -2), "weight": 0.1, "max_mood": 35},
}

# =============================================================================
# Initial NPC data
# =============================================================================
INITIAL_AGENTS: list[dict[str, str]] = [
    {"name": "Jack",  "personality": "Honest",     "social_tendency": "extrovert"},
    {"name": "Luna",  "personality": "Manipulative", "social_tendency": "extrovert"},
    {"name": "Rex",   "personality": "Hot-headed",  "social_tendency": "introvert"},
    {"name": "Maya",  "personality": "Saintly",     "social_tendency": "extrovert"},
    {"name": "Shadow","personality": "Loner",       "social_tendency": "introvert"},
    {"name": "Fox",   "personality": "Deceptive",   "social_tendency": "extrovert"},
    {"name": "Alpha", "personality": "Alpha",       "social_tendency": "extrovert"},
    {"name": "Mouse", "personality": "Cowardly",    "social_tendency": "introvert"},
    {"name": "Dice",  "personality": "Risk-taker",  "social_tendency": "neutral"},
    {"name": "Sage",  "personality": "Wise",        "social_tendency": "neutral"},
]

# =============================================================================
# Command patterns
# =============================================================================
FEED_PATTERN = re.compile(r"feed\s+(\w+)", re.IGNORECASE)
CHECK_PATTERN = re.compile(r"(check|查询|状态)", re.IGNORECASE)
RESET_PATTERN = re.compile(r"(reset|重新开始|重置)", re.IGNORECASE)
HEAL_PATTERN = re.compile(r"heal\s+(\w+)", re.IGNORECASE)
TALK_PATTERN = re.compile(r"talk\s+(\w+)\s*(.*)?", re.IGNORECASE)
ENCOURAGE_PATTERN = re.compile(r"encourage\s+(\w+)", re.IGNORECASE)
LOVE_PATTERN = re.compile(r"love\s+(\w+)", re.IGNORECASE)
REVIVE_PATTERN = re.compile(r"revive\s+(\w+)", re.IGNORECASE)
BUILD_PATTERN = re.compile(r"build\s+(\w+)", re.IGNORECASE)
TRADE_PATTERN = re.compile(r"trade\s+(\w+)\s+(\w+)\s+(\d+)", re.IGNORECASE)
WHISPER_PATTERN = re.compile(r"whisper\s+(\w+)\s+(.+)", re.IGNORECASE)

# =============================================================================
# Rumors / Whispers
# =============================================================================
RUMOR_TRUST_SHIFT: int = 5  # How much trust shifts per rumor
RUMOR_MOOD_SHIFT: int = 3   # Mood impact of hearing rumors

# =============================================================================
# Building system
# =============================================================================
BUILD_COST: int = 20
BUILDING_TYPES: dict[str, dict] = {
    "shelter": {
        "name": "Shelter",
        "description": "A sturdy shelter that protects from weather and restores HP.",
        "cost": 20,
        "hp": 200,
        "effects": {"weather_protection": True, "hp_recovery_bonus": 2},
        "construction_ticks": 10,
    },
    "watchtower": {
        "name": "Watchtower",
        "description": "A tall tower to spot ships and threats.",
        "cost": 30,
        "hp": 150,
        "effects": {"vision_range": 2, "threat_warning": True},
        "construction_ticks": 15,
    },
    "farm": {
        "name": "Farm",
        "description": "A small farm that produces food daily.",
        "cost": 25,
        "hp": 100,
        "effects": {"food_per_day": 2, "mood_bonus": 5},
        "construction_ticks": 12,
    },
    "workshop": {
        "name": "Workshop",
        "description": "A crafting workshop for making tools.",
        "cost": 35,
        "hp": 120,
        "effects": {"crafting_speed": 2, "tool_quality_bonus": 1},
        "construction_ticks": 18,
    },
}
