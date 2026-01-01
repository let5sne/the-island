"""
Vote Manager - Audience Voting System (Phase 9).

Manages voting sessions for narrative decisions,
supporting both Twitch chat commands and Unity client votes.
"""

from __future__ import annotations

import asyncio
import logging
import re
import time
import uuid
from dataclasses import dataclass, field
from typing import Any, Callable, Awaitable

logger = logging.getLogger(__name__)


@dataclass(frozen=True)
class VoteOption:
    """A voting option."""
    choice_id: str
    text: str


@dataclass
class VoteSession:
    """An active voting session."""
    vote_id: str
    options: list[VoteOption]
    start_ts: float
    end_ts: float
    duration_seconds: int = 60  # Store actual duration for this session
    votes_by_user: dict[str, int] = field(default_factory=dict)  # user_id -> choice_index
    tallies: list[int] = field(default_factory=list)  # vote count per option


@dataclass(frozen=True)
class VoteSnapshot:
    """Real-time voting statistics snapshot."""
    vote_id: str
    tallies: list[int]
    percentages: list[float]
    total_votes: int
    remaining_seconds: float
    ends_at: float

    def to_dict(self) -> dict[str, Any]:
        """Convert to dictionary for broadcasting."""
        return {
            "vote_id": self.vote_id,
            "tallies": self.tallies,
            "percentages": self.percentages,
            "total_votes": self.total_votes,
            "remaining_seconds": max(0, self.remaining_seconds),
            "ends_at": self.ends_at,
        }


@dataclass(frozen=True)
class VoteResult:
    """Final voting result after session ends."""
    vote_id: str
    winning_choice_id: str
    winning_choice_text: str
    winning_index: int
    tallies: list[int]
    percentages: list[float]
    total_votes: int
    is_tie: bool = False

    def to_dict(self) -> dict[str, Any]:
        """Convert to dictionary for broadcasting."""
        return {
            "vote_id": self.vote_id,
            "winning_choice_id": self.winning_choice_id,
            "winning_choice_text": self.winning_choice_text,
            "winning_index": self.winning_index,
            "tallies": self.tallies,
            "percentages": self.percentages,
            "total_votes": self.total_votes,
            "is_tie": self.is_tie,
        }


# Twitch command patterns
VOTE_PATTERN_NUMERIC = re.compile(r"^!([1-9])$")  # !1, !2, etc.
VOTE_PATTERN_ALPHA = re.compile(r"^!([AaBb])$")   # !A, !B, etc.


class VoteManager:
    """
    Manages voting sessions with dual-channel support (Twitch + Unity).

    Features:
    - Real-time vote counting
    - Vote changing (users can change their vote)
    - Automatic session expiration
    - Periodic snapshot broadcasting
    """

    def __init__(
        self,
        duration_seconds: int = 60,
        broadcast_interval: float = 1.0,
    ) -> None:
        """
        Initialize the vote manager.

        Args:
            duration_seconds: Default voting window duration
            broadcast_interval: How often to broadcast vote updates (seconds)
        """
        self._duration_seconds = duration_seconds
        self._broadcast_interval = broadcast_interval
        self._current: VoteSession | None = None
        self._broadcast_callback: Callable[[VoteSnapshot], Awaitable[None]] | None = None
        self._broadcast_task: asyncio.Task | None = None

    @property
    def is_voting_active(self) -> bool:
        """Check if a voting session is currently active."""
        if not self._current:
            return False
        return time.time() < self._current.end_ts

    @property
    def current_session(self) -> VoteSession | None:
        """Get the current voting session."""
        return self._current

    def set_broadcast_callback(
        self,
        callback: Callable[[VoteSnapshot], Awaitable[None]],
    ) -> None:
        """
        Set the callback for broadcasting vote updates.

        Args:
            callback: Async function that receives VoteSnapshot and broadcasts it
        """
        self._broadcast_callback = callback

    def start_vote(
        self,
        options: list[VoteOption],
        duration_seconds: int | None = None,
        now: float | None = None,
    ) -> VoteSession:
        """
        Start a new voting session.

        Args:
            options: List of voting options (minimum 2)
            duration_seconds: Override default duration
            now: Override current timestamp (for testing)

        Returns:
            The created VoteSession
        """
        if len(options) < 2:
            raise ValueError("Voting requires at least 2 options")

        now = now or time.time()
        duration = duration_seconds or self._duration_seconds

        session = VoteSession(
            vote_id=uuid.uuid4().hex,
            options=options,
            start_ts=now,
            end_ts=now + duration,
            duration_seconds=duration,
            tallies=[0 for _ in options],
        )
        self._current = session

        # Start broadcast loop
        if self._broadcast_callback:
            self._start_broadcast_loop()

        logger.info(
            f"Vote started: {session.vote_id} with {len(options)} options, "
            f"duration={duration}s"
        )

        return session

    def _start_broadcast_loop(self) -> None:
        """Start the periodic broadcast task."""
        if self._broadcast_task and not self._broadcast_task.done():
            self._broadcast_task.cancel()

        async def broadcast_loop():
            try:
                while self.is_voting_active:
                    snapshot = self.snapshot()
                    if snapshot and self._broadcast_callback:
                        try:
                            await self._broadcast_callback(snapshot)
                        except Exception as e:
                            logger.error(f"Broadcast callback error: {e}")
                    await asyncio.sleep(self._broadcast_interval)
            except asyncio.CancelledError:
                pass

        self._broadcast_task = asyncio.create_task(broadcast_loop())

    def parse_twitch_message(self, content: str) -> int | None:
        """
        Parse a Twitch chat message for vote commands.

        Supported formats:
        - !1, !2, !3, etc. (1-indexed, converted to 0-indexed)
        - !A, !B (converted to 0, 1)

        Args:
            content: The chat message content

        Returns:
            Choice index (0-indexed) or None if not a vote command
        """
        text = content.strip()

        # Try numeric pattern first
        match = VOTE_PATTERN_NUMERIC.match(text)
        if match:
            return int(match.group(1)) - 1  # Convert to 0-indexed

        # Try alphabetic pattern
        match = VOTE_PATTERN_ALPHA.match(text)
        if match:
            letter = match.group(1).upper()
            return ord(letter) - ord('A')  # A=0, B=1

        return None

    def cast_vote(
        self,
        voter_id: str,
        choice_index: int,
        source: str = "twitch",
    ) -> bool:
        """
        Record a vote from a user.

        Users can change their vote - the previous vote is subtracted
        and the new vote is added.

        Args:
            voter_id: Unique identifier for the voter
            choice_index: 0-indexed choice number
            source: Vote source ("twitch" or "unity")

        Returns:
            True if vote was recorded, False if invalid or session ended
        """
        if not self._current:
            logger.debug(f"Vote rejected: no active session (voter={voter_id})")
            return False

        if time.time() > self._current.end_ts:
            logger.debug(f"Vote rejected: session ended (voter={voter_id})")
            return False

        if choice_index < 0 or choice_index >= len(self._current.options):
            logger.debug(
                f"Vote rejected: invalid choice {choice_index} "
                f"(voter={voter_id}, max={len(self._current.options)-1})"
            )
            return False

        # Normalize voter ID (Twitch usernames are case-insensitive)
        normalized_voter_id = voter_id.strip().lower()
        if not normalized_voter_id:
            logger.debug("Vote rejected: empty voter id")
            return False

        # Handle vote change - subtract previous vote
        previous = self._current.votes_by_user.get(normalized_voter_id)
        if previous is not None:
            if previous == choice_index:
                # Same vote, no change needed
                return True
            # Subtract old vote
            self._current.tallies[previous] = max(
                0, self._current.tallies[previous] - 1
            )
            logger.debug(f"Vote changed: {normalized_voter_id} from {previous} to {choice_index}")

        # Record new vote
        self._current.votes_by_user[normalized_voter_id] = choice_index
        self._current.tallies[choice_index] += 1

        logger.debug(
            f"Vote cast: {voter_id} -> {choice_index} "
            f"(source={source}, tallies={self._current.tallies})"
        )

        return True

    def snapshot(self, now: float | None = None) -> VoteSnapshot | None:
        """
        Generate a real-time snapshot of current voting status.

        Args:
            now: Override current timestamp (for testing)

        Returns:
            VoteSnapshot or None if no active session
        """
        if not self._current:
            return None

        now = now or time.time()
        tallies = list(self._current.tallies)
        total = sum(tallies)

        # Calculate percentages
        if total > 0:
            percentages = [round((t / total) * 100, 1) for t in tallies]
        else:
            percentages = [0.0 for _ in tallies]

        return VoteSnapshot(
            vote_id=self._current.vote_id,
            tallies=tallies,
            percentages=percentages,
            total_votes=total,
            remaining_seconds=self._current.end_ts - now,
            ends_at=self._current.end_ts,
        )

    def maybe_finalize(self, now: float | None = None) -> VoteResult | None:
        """
        Check if voting has ended and finalize results.

        Args:
            now: Override current timestamp (for testing)

        Returns:
            VoteResult if voting ended, None if still active
        """
        if not self._current:
            return None

        now = now or time.time()
        if now < self._current.end_ts:
            return None

        # Cancel broadcast task
        if self._broadcast_task and not self._broadcast_task.done():
            self._broadcast_task.cancel()

        # Calculate final results
        tallies = list(self._current.tallies)
        total = sum(tallies)

        # Calculate percentages
        if total > 0:
            percentages = [round((t / total) * 100, 1) for t in tallies]
        else:
            percentages = [0.0 for _ in tallies]

        # Find winner
        if tallies:
            max_votes = max(tallies)
            winners = [i for i, t in enumerate(tallies) if t == max_votes]
            is_tie = len(winners) > 1

            # In case of tie, choose randomly (or could defer to Director)
            import random
            winning_index = random.choice(winners) if is_tie else winners[0]
        else:
            winning_index = 0
            is_tie = False

        winning_option = self._current.options[winning_index]

        result = VoteResult(
            vote_id=self._current.vote_id,
            winning_choice_id=winning_option.choice_id,
            winning_choice_text=winning_option.text,
            winning_index=winning_index,
            tallies=tallies,
            percentages=percentages,
            total_votes=total,
            is_tie=is_tie,
        )

        logger.info(
            f"Vote finalized: {result.vote_id} "
            f"winner={result.winning_choice_id} ({result.winning_choice_text}) "
            f"votes={result.tallies} tie={result.is_tie}"
        )

        # Clear current session
        self._current = None

        return result

    def cancel_vote(self) -> bool:
        """
        Cancel the current voting session.

        Returns:
            True if a session was cancelled, False if no active session
        """
        if not self._current:
            return False

        if self._broadcast_task and not self._broadcast_task.done():
            self._broadcast_task.cancel()

        vote_id = self._current.vote_id
        self._current = None

        logger.info(f"Vote cancelled: {vote_id}")
        return True

    def get_vote_started_data(self) -> dict[str, Any] | None:
        """
        Get data for VOTE_STARTED event.

        Returns:
            Dictionary with vote session info, or None if no active session
        """
        if not self._current:
            return None

        return {
            "vote_id": self._current.vote_id,
            "choices": [
                {"choice_id": o.choice_id, "text": o.text}
                for o in self._current.options
            ],
            "duration_seconds": self._current.duration_seconds,
            "ends_at": self._current.end_ts,
            "source": "director",
        }


# Global instance
vote_manager = VoteManager()
