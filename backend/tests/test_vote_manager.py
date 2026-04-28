"""Tests for vote_manager.py - Audience Voting System."""

import time
from unittest.mock import patch

import pytest

from app.vote_manager import (
    VoteOption, VoteSession, VoteSnapshot, VoteResult, VoteManager,
)


# =============================================================================
# Test VoteOption (frozen dataclass)
# =============================================================================

class TestVoteOption:
    def test_creation(self):
        opt = VoteOption(choice_id="opt_a", text="Go left")
        assert opt.choice_id == "opt_a"
        assert opt.text == "Go left"

    def test_immutability(self):
        opt = VoteOption(choice_id="opt_a", text="Go left")
        with pytest.raises(Exception):
            opt.choice_id = "other"


# =============================================================================
# Test VoteSession (mutable dataclass)
# =============================================================================

class TestVoteSession:
    def test_initial_state(self):
        options = [VoteOption("a", "Option A"), VoteOption("b", "Option B")]
        now = time.time()
        session = VoteSession(
            vote_id="test-id",
            options=options,
            start_ts=now,
            end_ts=now + 60,
            duration_seconds=60,
            tallies=[0, 0],
        )
        assert session.vote_id == "test-id"
        assert len(session.options) == 2
        assert session.votes_by_user == {}
        assert session.tallies == [0, 0]

    def test_tallies_match_options_count(self):
        options = [VoteOption("a", "A"), VoteOption("b", "B"), VoteOption("c", "C")]
        session = VoteSession(
            vote_id="test-id",
            options=options,
            start_ts=0.0,
            end_ts=60.0,
            tallies=[0, 0, 0],
        )
        assert len(session.tallies) == len(session.options)


# =============================================================================
# Test VoteSnapshot (frozen dataclass)
# =============================================================================

class TestVoteSnapshot:
    def test_to_dict_keys(self):
        snap = VoteSnapshot(
            vote_id="v1",
            tallies=[3, 2],
            percentages=[60.0, 40.0],
            total_votes=5,
            remaining_seconds=30.0,
            ends_at=9999.0,
        )
        d = snap.to_dict()
        assert d["vote_id"] == "v1"
        assert d["tallies"] == [3, 2]
        assert d["percentages"] == [60.0, 40.0]
        assert d["total_votes"] == 5

    def test_remaining_seconds_clamped_to_zero(self):
        snap = VoteSnapshot(
            vote_id="v1",
            tallies=[0],
            percentages=[0.0],
            total_votes=0,
            remaining_seconds=-5.0,
            ends_at=0.0,
        )
        assert snap.to_dict()["remaining_seconds"] == 0


# =============================================================================
# Test VoteResult (frozen dataclass)
# =============================================================================

class TestVoteResult:
    def test_to_dict_keys(self):
        result = VoteResult(
            vote_id="v1",
            winning_choice_id="b",
            winning_choice_text="Option B",
            winning_index=1,
            tallies=[1, 3],
            percentages=[25.0, 75.0],
            total_votes=4,
            is_tie=False,
        )
        d = result.to_dict()
        assert d["winning_choice_id"] == "b"
        assert d["winning_index"] == 1
        assert d["total_votes"] == 4

    def test_tie_flag_set_correctly(self):
        result = VoteResult(
            vote_id="v1",
            winning_choice_id="a",
            winning_choice_text="A",
            winning_index=0,
            tallies=[2, 2],
            percentages=[50.0, 50.0],
            total_votes=4,
            is_tie=True,
        )
        assert result.is_tie is True


# =============================================================================
# Test VoteManager.cast_vote
# =============================================================================

class TestVoteManagerCastVote:
    def test_cast_vote_no_active_session(self):
        vm = VoteManager()
        assert vm.cast_vote("user1", 0) is False

    def test_cast_vote_after_expiry(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, duration_seconds=60, now=fake_time)
        # Advance time past expiry
        fake_time += 61.0
        with patch("time.time", return_value=fake_time):
            assert vm.cast_vote("user1", 0) is False

    def test_cast_vote_invalid_choice_index(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, now=fake_time)
        with patch("time.time", return_value=fake_time + 1):
            assert vm.cast_vote("user1", -1) is False
            assert vm.cast_vote("user1", 2) is False
            assert vm.cast_vote("user1", 99) is False

    def test_cast_vote_empty_voter_id(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, now=fake_time)
        with patch("time.time", return_value=fake_time + 1):
            assert vm.cast_vote("", 0) is False
            assert vm.cast_vote("   ", 0) is False

    def test_cast_vote_basic(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, now=fake_time)
        fake_time += 1.0
        with patch("time.time", return_value=fake_time):
            assert vm.cast_vote("user1", 0) is True
        snap = vm.snapshot(now=fake_time)
        assert snap.tallies[0] == 1
        assert snap.tallies[1] == 0
        assert snap.total_votes == 1

    def test_cast_vote_change(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, now=fake_time)
        fake_time += 1.0
        with patch("time.time", return_value=fake_time):
            vm.cast_vote("user1", 0)
        fake_time += 1.0
        with patch("time.time", return_value=fake_time):
            vm.cast_vote("user1", 1)
        snap = vm.snapshot(now=fake_time)
        assert snap.tallies[0] == 0
        assert snap.tallies[1] == 1

    def test_cast_vote_same_vote_noop(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, now=fake_time)
        fake_time += 1.0
        with patch("time.time", return_value=fake_time):
            vm.cast_vote("user1", 0)
            assert vm.cast_vote("user1", 0) is True
        snap = vm.snapshot(now=fake_time)
        assert snap.tallies[0] == 1  # No double-count

    def test_cast_vote_case_insensitive_twitch(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, now=fake_time)
        fake_time += 1.0
        with patch("time.time", return_value=fake_time):
            vm.cast_vote("UserA", 0)
        fake_time += 1.0
        with patch("time.time", return_value=fake_time):
            vm.cast_vote("usera", 1)
        snap = vm.snapshot(now=fake_time)
        assert snap.tallies[0] == 0
        assert snap.tallies[1] == 1

    def test_cast_vote_multiple_voters(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B"), VoteOption("c", "C")]
        vm.start_vote(options, now=fake_time)
        fake_time += 1.0
        with patch("time.time", return_value=fake_time):
            vm.cast_vote("user1", 0)
            vm.cast_vote("user2", 1)
            vm.cast_vote("user3", 2)
            vm.cast_vote("user4", 1)
        snap = vm.snapshot(now=fake_time)
        assert snap.tallies == [1, 2, 1]
        assert snap.total_votes == 4


# =============================================================================
# Test VoteManager.parse_twitch_message
# =============================================================================

class TestVoteManagerParseTwitchMessage:
    def test_parse_numeric_valid(self):
        vm = VoteManager()
        assert vm.parse_twitch_message("!1") == 0
        assert vm.parse_twitch_message("!2") == 1
        assert vm.parse_twitch_message("!9") == 8

    def test_parse_numeric_one_indexed(self):
        vm = VoteManager()
        assert vm.parse_twitch_message("!1") == 0

    def test_parse_alpha_valid(self):
        vm = VoteManager()
        assert vm.parse_twitch_message("!A") == 0
        assert vm.parse_twitch_message("!B") == 1

    def test_parse_alpha_case_insensitive(self):
        vm = VoteManager()
        assert vm.parse_twitch_message("!a") == 0
        assert vm.parse_twitch_message("!b") == 1

    def test_parse_invalid_formats(self):
        vm = VoteManager()
        assert vm.parse_twitch_message("!0") is None
        assert vm.parse_twitch_message("!C") is None
        assert vm.parse_twitch_message("hello") is None
        assert vm.parse_twitch_message("!") is None
        assert vm.parse_twitch_message("") is None
        assert vm.parse_twitch_message("!12") is None

    def test_parse_with_whitespace(self):
        vm = VoteManager()
        assert vm.parse_twitch_message(" !1 ") == 0
        assert vm.parse_twitch_message("  !b  ") == 1


# =============================================================================
# Test VoteManager.snapshot
# =============================================================================

class TestVoteManagerSnapshot:
    def test_snapshot_no_session(self):
        vm = VoteManager()
        assert vm.snapshot() is None

    def test_snapshot_zero_votes(self):
        vm = VoteManager()
        now = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, now=now)
        snap = vm.snapshot(now=now + 1)
        assert snap.total_votes == 0
        assert snap.percentages == [0.0, 0.0]

    def test_snapshot_with_votes(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, now=fake_time)
        fake_time += 1.0
        with patch("time.time", return_value=fake_time):
            vm.cast_vote("user1", 0)
            vm.cast_vote("user2", 0)
            vm.cast_vote("user3", 1)
        snap = vm.snapshot(now=fake_time)
        assert snap.tallies == [2, 1]
        assert snap.percentages == [66.7, 33.3]
        assert snap.total_votes == 3

    def test_snapshot_remaining_seconds(self):
        vm = VoteManager()
        now = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, duration_seconds=30, now=now)
        snap = vm.snapshot(now=now + 10)
        assert snap.remaining_seconds == pytest.approx(20.0, rel=0.1)


# =============================================================================
# Test VoteManager.maybe_finalize
# =============================================================================

class TestVoteManagerMaybeFinalize:
    def test_finalize_before_end_returns_none(self):
        vm = VoteManager()
        now = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, duration_seconds=60, now=now)
        assert vm.maybe_finalize(now=now + 30) is None

    def test_finalize_after_expiry(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, duration_seconds=60, now=fake_time)
        fake_time += 1.0
        with patch("time.time", return_value=fake_time):
            vm.cast_vote("user1", 1)
        result = vm.maybe_finalize(now=fake_time + 60)
        assert result is not None
        assert result.winning_index == 1
        assert result.total_votes == 1
        assert vm.is_voting_active is False

    def test_finalize_clears_session(self):
        vm = VoteManager()
        now = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, duration_seconds=60, now=now)
        vm.maybe_finalize(now=now + 61)
        assert vm.current_session is None

    def test_finalize_no_votes_cast(self):
        vm = VoteManager()
        now = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, duration_seconds=60, now=now)
        result = vm.maybe_finalize(now=now + 61)
        assert result is not None
        assert result.total_votes == 0
        assert result.percentages == [0.0, 0.0]

    def test_finalize_no_active_session(self):
        vm = VoteManager()
        assert vm.maybe_finalize() is None


# =============================================================================
# Test VoteManager.cancel_vote
# =============================================================================

class TestVoteManagerCancelVote:
    def test_cancel_active_session(self):
        vm = VoteManager()
        now = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, now=now)
        assert vm.cancel_vote() is True
        assert vm.is_voting_active is False
        assert vm.current_session is None

    def test_cancel_no_session(self):
        vm = VoteManager()
        assert vm.cancel_vote() is False


# =============================================================================
# Test VoteManager.start_vote
# =============================================================================

class TestVoteManagerStartVote:
    def test_start_vote_creates_session(self):
        vm = VoteManager()
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        session = vm.start_vote(options, now=fake_time)
        assert session is not None
        assert len(session.options) == 2
        with patch("time.time", return_value=fake_time + 1):
            assert vm.is_voting_active is True
        assert vm.current_session is session

    def test_start_vote_less_than_2_options_raises(self):
        vm = VoteManager()
        with pytest.raises(ValueError, match="at least 2 options"):
            vm.start_vote([VoteOption("a", "A")])
        with pytest.raises(ValueError):
            vm.start_vote([])

    def test_start_vote_custom_duration(self):
        vm = VoteManager()
        now = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        session = vm.start_vote(options, duration_seconds=30, now=now)
        assert session.duration_seconds == 30

    def test_start_vote_with_now_override(self):
        vm = VoteManager()
        now = 500.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        session = vm.start_vote(options, now=now)
        assert session.start_ts == 500.0
        assert session.end_ts == 560.0

    def test_start_vote_tallies_initialized(self):
        vm = VoteManager()
        options = [VoteOption("a", "A"), VoteOption("b", "B"), VoteOption("c", "C")]
        session = vm.start_vote(options)
        assert session.tallies == [0, 0, 0]


# =============================================================================
# Test VoteManager.get_vote_started_data
# =============================================================================

class TestVoteManagerGetVoteStartedData:
    def test_get_data_with_active_session(self):
        vm = VoteManager()
        now = 1000.0
        options = [VoteOption("a", "Go left"), VoteOption("b", "Go right")]
        vm.start_vote(options, now=now)
        data = vm.get_vote_started_data()
        assert data is not None
        assert data["vote_id"] is not None
        assert len(data["choices"]) == 2
        assert data["choices"][0] == {"choice_id": "a", "text": "Go left"}
        assert data["duration_seconds"] == 60
        assert data["source"] == "director"

    def test_get_data_no_session(self):
        vm = VoteManager()
        assert vm.get_vote_started_data() is None


# =============================================================================
# Test VoteManager integration (full lifecycle)
# =============================================================================

@pytest.mark.asyncio
class TestVoteManagerIntegration:
    async def test_full_vote_lifecycle(self):
        vm = VoteManager(duration_seconds=1)
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]

        vm.start_vote(options, duration_seconds=1, now=fake_time)
        with patch("time.time", return_value=fake_time + 0.1):
            assert vm.is_voting_active is True

        fake_time += 0.2
        with patch("time.time", return_value=fake_time):
            vm.cast_vote("viewer1", 0)
            vm.cast_vote("viewer2", 0)
            vm.cast_vote("viewer3", 1)

        snap = vm.snapshot(now=fake_time)
        assert snap.total_votes == 3
        assert snap.tallies == [2, 1]

        result = vm.maybe_finalize(now=fake_time + 0.9)
        assert result is not None
        assert result.winning_index == 0

        with patch("time.time", return_value=fake_time + 1.0):
            assert vm.is_voting_active is False

    async def test_vote_change_during_session(self):
        vm = VoteManager(duration_seconds=1)
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, duration_seconds=1, now=fake_time)

        fake_time += 0.1
        with patch("time.time", return_value=fake_time):
            vm.cast_vote("user1", 0)
            vm.cast_vote("user2", 0)
            vm.cast_vote("user3", 0)
            vm.cast_vote("user2", 1)  # Change
            vm.cast_vote("user3", 1)  # Change

        snap = vm.snapshot(now=fake_time)
        assert snap.tallies == [1, 2]

    async def test_concurrent_voters(self):
        vm = VoteManager(duration_seconds=1)
        fake_time = 1000.0
        options = [VoteOption("a", "A"), VoteOption("b", "B")]
        vm.start_vote(options, duration_seconds=1, now=fake_time)

        fake_time += 0.1
        with patch("time.time", return_value=fake_time):
            for i in range(100):
                vm.cast_vote(f"user{i}", i % 2)

        snap = vm.snapshot(now=fake_time)
        assert snap.total_votes == 100
