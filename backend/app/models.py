"""
Game entity models for The Island.
Defines Player and Boss data structures.
"""

from pydantic import BaseModel, Field


class Player(BaseModel):
    """
    Represents a player in the game world.
    """
    name: str
    hp: int = Field(default=100, ge=0)
    max_hp: int = Field(default=100, gt=0)
    gold: int = Field(default=0, ge=0)

    def take_damage(self, amount: int) -> int:
        """Apply damage to player, returns actual damage dealt."""
        actual = min(amount, self.hp)
        self.hp -= actual
        return actual

    def heal(self, amount: int) -> int:
        """Heal player, returns actual HP restored."""
        actual = min(amount, self.max_hp - self.hp)
        self.hp += actual
        return actual

    def add_gold(self, amount: int) -> None:
        """Add gold to player."""
        self.gold += amount

    @property
    def is_alive(self) -> bool:
        """Check if player is alive."""
        return self.hp > 0


class Boss(BaseModel):
    """
    Represents a boss enemy in the game.
    """
    name: str
    hp: int = Field(ge=0)
    max_hp: int = Field(gt=0)

    def take_damage(self, amount: int) -> int:
        """Apply damage to boss, returns actual damage dealt."""
        actual = min(amount, self.hp)
        self.hp -= actual
        return actual

    def reset(self) -> None:
        """Reset boss to full HP."""
        self.hp = self.max_hp

    @property
    def is_alive(self) -> bool:
        """Check if boss is alive."""
        return self.hp > 0

    @property
    def hp_percentage(self) -> float:
        """Get HP as percentage."""
        return (self.hp / self.max_hp) * 100 if self.max_hp > 0 else 0
