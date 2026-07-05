from datetime import datetime, timedelta, timezone

import pytest
from fastapi.testclient import TestClient

from src.main import app
from src.anomaly import detect_suspicious_behaviour

client = TestClient(app)

BASE_TIME = datetime(2026, 7, 5, 9, 0, 0)

def _event(student_id="s1", event_type="keystroke", offset_seconds=0, session_id="sess-1",
           assignment_id=None, metadata=None):
    return {
        "student_id": student_id,
        "class_session_id": session_id,
        "assignment_id": assignment_id,
        "event_type": event_type,
        "metadata": metadata or {},
        "recorded_at": BASE_TIME + timedelta(seconds=offset_seconds),
    }

# --- Unit Tests for AIS-07 anomaly engine ---

def test_ais_07_no_events_returns_no_flags():
    assert detect_suspicious_behaviour([]) == []

def test_ais_07_too_few_events_not_flagged():
    events = [_event(offset_seconds=i) for i in range(3)]
    assert detect_suspicious_behaviour(events) == []

def test_ais_07_jittered_human_typing_not_flagged():
    # Irregular, human-like intervals with a normal keystroke/paste mix and no tab-switching.
    offsets = [0, 0.4, 1.1, 1.3, 2.6, 3.9, 4.1, 5.7, 7.0, 7.2]
    events = [_event(offset_seconds=o) for o in offsets]
    assert detect_suspicious_behaviour(events) == []

def test_ais_07_uniform_timing_alone_does_not_cross_default_threshold():
    # Perfectly even spacing is scripted-looking, but a single heuristic must not
    # be enough on its own to cross the default confidence threshold.
    events = [_event(event_type="mouse_move", offset_seconds=i * 0.5) for i in range(10)]
    assert detect_suspicious_behaviour(events, min_confidence=0.70) == []
    # Lowering the bar reveals the underlying signal was still detected.
    flags = detect_suspicious_behaviour(events, min_confidence=0.40)
    assert len(flags) == 1
    assert "uniform_event_timing" in flags[0]["reasons"]

def test_ais_07_zero_min_confidence_does_not_flag_windows_with_no_evidence():
    # A min_confidence of 0.0 must not turn "no signals triggered" into a flag —
    # a flag with an empty reasons list is meaningless output.
    events = [_event(offset_seconds=0)]
    assert detect_suspicious_behaviour(events, min_confidence=0.0) == []

def test_ais_07_uniform_timing_plus_paste_ratio_flags():
    # All 10 events stay on the same 0.5s cadence so overall timing is still uniform;
    # the last two happen to be pastes, pushing the paste ratio over threshold too.
    events = [_event(event_type="mouse_move", offset_seconds=i * 0.5) for i in range(8)]
    events += [
        _event(event_type="paste", offset_seconds=4.0, metadata={"char_count": 10}),
        _event(event_type="paste", offset_seconds=4.5, metadata={"char_count": 10}),
    ]
    flags = detect_suspicious_behaviour(events)
    assert len(flags) == 1
    assert flags[0]["student_id"] == "s1"
    assert set(flags[0]["reasons"]) >= {"uniform_event_timing", "excessive_paste_ratio"}
    assert 0.0 <= flags[0]["confidence_score"] <= 1.0

def test_ais_07_large_paste_burst_combined_with_context_switching_flags():
    events = [_event(event_type="window_blur", offset_seconds=i) for i in range(4)]
    events.append(_event(event_type="window_focus", offset_seconds=4))
    events.append(_event(event_type="paste", offset_seconds=5, metadata={"char_count": 500}))
    flags = detect_suspicious_behaviour(events)
    assert len(flags) == 1
    assert set(flags[0]["reasons"]) >= {"large_paste_burst", "frequent_context_switching"}

def test_ais_07_confidence_score_never_exceeds_one():
    # All 12 events stay on the same 0.5s cadence so every heuristic fires at once:
    # uniform timing, a high blur ratio, a high paste ratio, and a large paste burst.
    events = [_event(event_type="mouse_move", offset_seconds=i * 0.5) for i in range(5)]
    events += [_event(event_type="window_blur", offset_seconds=2.5 + i * 0.5) for i in range(5)]
    events += [
        _event(event_type="paste", offset_seconds=5.0, metadata={"char_count": 1000}),
        _event(event_type="paste", offset_seconds=5.5, metadata={"char_count": 1000}),
    ]
    flags = detect_suspicious_behaviour(events)
    assert len(flags) == 1
    assert flags[0]["confidence_score"] <= 1.0
    assert set(flags[0]["reasons"]) == {
        "uniform_event_timing", "excessive_paste_ratio",
        "large_paste_burst", "frequent_context_switching",
    }

def test_ais_07_windows_are_scored_independently_per_student_session_assignment():
    suspicious = [_event(student_id="s1", session_id="sess-1", event_type="window_blur", offset_seconds=i)
                  for i in range(4)]
    suspicious.append(_event(student_id="s1", session_id="sess-1", event_type="paste",
                              offset_seconds=5, metadata={"char_count": 500}))
    clean = [_event(student_id="s2", session_id="sess-1", offset_seconds=o)
             for o in [0, 0.4, 1.1, 1.3, 2.6]]

    flags = detect_suspicious_behaviour(suspicious + clean)
    assert len(flags) == 1
    assert flags[0]["student_id"] == "s1"

def test_ais_07_different_assignment_windows_for_same_student_scored_separately():
    window_a = [_event(student_id="s1", session_id=None, assignment_id="a1", event_type="window_blur", offset_seconds=i)
                for i in range(4)]
    window_a.append(_event(student_id="s1", session_id=None, assignment_id="a1", event_type="paste",
                            offset_seconds=5, metadata={"char_count": 500}))
    window_b = [_event(student_id="s1", session_id=None, assignment_id="a2", offset_seconds=o)
                for o in [0, 0.4, 1.1, 1.3, 2.6]]

    flags = detect_suspicious_behaviour(window_a + window_b)
    assert len(flags) == 1
    assert flags[0]["assignment_id"] == "a1"

def test_ais_07_mixed_naive_and_aware_timestamps_still_score_correctly():
    # The engine itself assumes comparable datetimes (normalization happens at the API
    # boundary in main.py) — this exercises that a uniform, all-aware-UTC window still
    # scores the same as an all-naive one, guarding against regressions in that assumption.
    naive_events = [_event(event_type="mouse_move", offset_seconds=i * 0.5) for i in range(10)]
    aware_events = [
        {**e, "recorded_at": e["recorded_at"].replace(tzinfo=timezone.utc)}
        for e in naive_events
    ]
    naive_flags = detect_suspicious_behaviour(naive_events, min_confidence=0.40)
    aware_flags = detect_suspicious_behaviour(aware_events, min_confidence=0.40)
    assert len(naive_flags) == len(aware_flags) == 1
    assert naive_flags[0]["reasons"] == aware_flags[0]["reasons"]

# --- Integration Tests for AIS-07 API endpoint ---

def test_ais_07_api_suspicious_behaviour_success():
    events = [
        {
            "student_id": "s1",
            "class_session_id": "sess-1",
            "assignment_id": None,
            "event_type": "window_blur",
            "metadata": {},
            "recorded_at": (BASE_TIME + timedelta(seconds=i)).isoformat(),
        }
        for i in range(4)
    ]
    events.append({
        "student_id": "s1",
        "class_session_id": "sess-1",
        "assignment_id": None,
        "event_type": "paste",
        "metadata": {"char_count": 500},
        "recorded_at": (BASE_TIME + timedelta(seconds=5)).isoformat(),
    })

    response = client.post("/api/v1/suspicious-behaviour", json={"events": events})
    assert response.status_code == 200

    data = response.json()
    assert len(data["flags"]) == 1
    assert data["flags"][0]["student_id"] == "s1"
    assert "large_paste_burst" in data["flags"][0]["reasons"]

def test_ais_07_api_suspicious_behaviour_no_flags_for_normal_usage():
    offsets = [0, 0.4, 1.1, 1.3, 2.6]
    events = [
        {
            "student_id": "s1",
            "class_session_id": "sess-1",
            "assignment_id": None,
            "event_type": "keystroke",
            "metadata": {},
            "recorded_at": (BASE_TIME + timedelta(seconds=o)).isoformat(),
        }
        for o in offsets
    ]

    response = client.post("/api/v1/suspicious-behaviour", json={"events": events})
    assert response.status_code == 200
    assert response.json()["flags"] == []

def test_ais_07_api_accepts_mixed_naive_and_aware_timestamps():
    # Regression test: recorded_at values with and without a timezone offset in the
    # same request used to crash the whole batch with a 500 (naive vs. aware compare).
    events = [
        {
            "student_id": "s1",
            "class_session_id": "sess-1",
            "assignment_id": None,
            "event_type": "keystroke",
            "metadata": {},
            "recorded_at": (BASE_TIME + timedelta(seconds=i)).isoformat() + ("Z" if i % 2 == 0 else ""),
        }
        for i in range(5)
    ]

    response = client.post("/api/v1/suspicious-behaviour", json={"events": events})
    assert response.status_code == 200

def test_ais_07_api_rejects_event_with_no_session_or_assignment():
    # AIS-07's acceptance criterion: "No analysis runs outside a class session or
    # assignment window." An event tied to neither must be rejected, not scored.
    payload = {
        "events": [
            {
                "student_id": "s1",
                "class_session_id": None,
                "assignment_id": None,
                "event_type": "keystroke",
                "recorded_at": BASE_TIME.isoformat(),
            }
        ]
    }
    response = client.post("/api/v1/suspicious-behaviour", json=payload)
    assert response.status_code == 422

def test_ais_07_api_validation_error_empty_student_id():
    payload = {
        "events": [
            {
                "student_id": "",
                "class_session_id": "sess-1",
                "event_type": "keystroke",
                "recorded_at": BASE_TIME.isoformat(),
            }
        ]
    }
    response = client.post("/api/v1/suspicious-behaviour", json=payload)
    assert response.status_code == 422

def test_ais_07_api_validation_error_missing_recorded_at():
    payload = {
        "events": [
            {"student_id": "s1", "class_session_id": "sess-1", "event_type": "keystroke"}
        ]
    }
    response = client.post("/api/v1/suspicious-behaviour", json=payload)
    assert response.status_code == 422
