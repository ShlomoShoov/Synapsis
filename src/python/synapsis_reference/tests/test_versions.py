"""Version rules (synapsis_reference/versions.py); the C# VersionTests.cs covers the same cases."""
from datetime import datetime, timezone

from synapsis_reference.versions import current_at, plan


def t(y, m=1, d=1):
    return datetime(y, m, d, tzinfo=timezone.utc)


NOW = t(2026, 10, 8)


def head(*versions):
    """versions: (content, known_at) oldest first."""
    history = [{"version": i + 1, "content": c, "known_at": k} for i, (c, k) in enumerate(versions)]
    return dict(history[-1], history=history)


def test_first_statement_is_version_one_known_as_sent():
    d = plan(None, "A", t(2006, 5, 15), NOW)
    assert (d.outcome, d.version, d.known_at, d.ingested) == ("new", 1, t(2006, 5, 15), False)


def test_same_content_as_latest_is_unchanged_even_with_an_earlier_known_at():
    assert plan(head(("A", t(2025, 5, 16))), "A", t(2006, 5, 15), NOW).outcome == "unchanged"


def test_change_known_later_becomes_the_next_version_known_as_sent():
    d = plan(head(("A", t(2006, 5, 15))), "B", t(2012, 8, 14), NOW)
    assert (d.outcome, d.version, d.known_at, d.ingested) == ("versioned", 2, t(2012, 8, 14), False)


def test_rereading_the_past_repeats_what_was_known_then():
    # A bank named A from 2006, renamed B in 2026; a re-run of 2025 sends A again with its 2025 known_at
    h = head(("A", t(2006, 5, 15)), ("B", t(2026, 5, 15)))
    assert plan(h, "A", t(2025, 5, 16), NOW).outcome == "unchanged"


def test_a_past_that_differs_from_what_was_known_then_is_stored_as_learned_now():
    # FDIC restated a 2025 figure: different from the version current in 2025, so it is new knowledge, known now
    h = head(("A", t(2006, 5, 15)), ("B", t(2026, 5, 15)))
    d = plan(h, "C", t(2025, 5, 16), NOW)
    assert (d.outcome, d.version, d.known_at, d.ingested) == ("versioned", 3, NOW, True)


def test_older_information_loaded_after_newer_is_known_now():
    # 2025 loaded first, then the 2006 backfill: nothing was current in 2006, so the 2006 name is learned now
    d = plan(head(("B", t(2025, 5, 16))), "A", t(2006, 5, 15), NOW)
    assert (d.outcome, d.version, d.known_at, d.ingested) == ("versioned", 2, NOW, True)


def test_known_at_in_the_future_of_the_receipt_is_replaced_by_the_receive_time():
    d = plan(head(("A", t(2006, 5, 15))), "B", t(2027, 1, 1), NOW)
    assert (d.known_at, d.ingested) == (NOW, True)


def test_current_at_picks_the_latest_version_known_by_then():
    h = head(("A", t(2006)), ("B", t(2012)), ("C", t(2020)))
    assert current_at(h["history"], t(2011))["content"] == "A"
    assert current_at(h["history"], t(2012))["content"] == "B"
    assert current_at(h["history"], t(2005)) is None
