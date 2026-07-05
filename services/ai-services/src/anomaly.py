import logging
import statistics
from collections import defaultdict
from datetime import datetime
from typing import Dict, List, Optional, Tuple, TypedDict

logger = logging.getLogger(__name__)

class TelemetryEvent(TypedDict):
    student_id: str
    class_session_id: Optional[str]
    assignment_id: Optional[str]
    event_type: str
    metadata: dict
    recorded_at: datetime

class SuspiciousFlag(TypedDict):
    student_id: str
    class_session_id: Optional[str]
    assignment_id: Optional[str]
    confidence_score: float
    reasons: List[str]

# Each heuristic contributes a capped weight toward the combined confidence score.
# Weights are tuned so no single heuristic can cross the default threshold alone —
# a flag needs corroborating signals, not one noisy metric (same fairness principle
# the architecture doc applies to AIS-05: a score should never stand alone as evidence).
_UNIFORM_TIMING_WEIGHT = 0.45
_PASTE_RATIO_WEIGHT = 0.30
_LARGE_PASTE_BURST_WEIGHT = 0.25
_CONTEXT_SWITCH_WEIGHT = 0.25

_MIN_EVENTS_FOR_TIMING = 5
_UNIFORM_TIMING_CV_THRESHOLD = 0.15  # coefficient of variation below this looks scripted, not human
_PASTE_RATIO_THRESHOLD = 0.5
_LARGE_PASTE_CHAR_THRESHOLD = 150  # a single paste this large suggests content copied from elsewhere
_CONTEXT_SWITCH_RATE_THRESHOLD = 0.3

_INPUT_EVENT_TYPES = ("keystroke", "paste")

def _window_key(event: TelemetryEvent) -> Tuple[str, Optional[str], Optional[str]]:
    return (event["student_id"], event.get("class_session_id"), event.get("assignment_id"))

def detect_suspicious_behaviour(
    events: List[TelemetryEvent],
    min_confidence: float = 0.70
) -> List[SuspiciousFlag]:
    """
    Analyzes usage-pattern telemetry (SDA-25) for signs of suspicious behaviour or
    automation, grouped per student/class-session/assignment window. Returns only
    windows whose combined confidence score meets or exceeds min_confidence.

    Callers are responsible for only supplying telemetry recorded during an active
    class session or assignment window — this function scores whatever it's given.
    """
    if not events:
        logger.info("Skipping suspicious-behaviour check: no telemetry events provided.")
        return []

    windows: Dict[Tuple[str, Optional[str], Optional[str]], List[TelemetryEvent]] = defaultdict(list)
    for event in events:
        windows[_window_key(event)].append(event)

    logger.info(f"Scoring {len(windows)} student/session/assignment window(s) from {len(events)} event(s).")

    flags: List[SuspiciousFlag] = []
    for (student_id, class_session_id, assignment_id), window_events in windows.items():
        score, reasons = _score_window(window_events)
        if score >= min_confidence:
            logger.warning(
                f"Suspicious behaviour flagged: student={student_id} "
                f"session={class_session_id} assignment={assignment_id} "
                f"confidence={score:.4f} reasons={reasons}"
            )
            flags.append({
                "student_id": student_id,
                "class_session_id": class_session_id,
                "assignment_id": assignment_id,
                "confidence_score": round(min(score, 1.0), 4),
                "reasons": reasons,
            })

    return flags

def _score_window(events: List[TelemetryEvent]) -> Tuple[float, List[str]]:
    ordered = sorted(events, key=lambda e: e["recorded_at"])
    score = 0.0
    reasons: List[str] = []

    if _has_uniform_timing(ordered):
        score += _UNIFORM_TIMING_WEIGHT
        reasons.append("uniform_event_timing")

    if _paste_ratio(ordered) >= _PASTE_RATIO_THRESHOLD:
        score += _PASTE_RATIO_WEIGHT
        reasons.append("excessive_paste_ratio")

    if _has_large_paste_burst(ordered):
        score += _LARGE_PASTE_BURST_WEIGHT
        reasons.append("large_paste_burst")

    if _context_switch_rate(ordered) >= _CONTEXT_SWITCH_RATE_THRESHOLD:
        score += _CONTEXT_SWITCH_WEIGHT
        reasons.append("frequent_context_switching")

    return score, reasons

def _has_uniform_timing(events: List[TelemetryEvent]) -> bool:
    """Near-zero variance between consecutive event timestamps looks scripted, not human."""
    if len(events) < _MIN_EVENTS_FOR_TIMING:
        return False

    timestamps = [e["recorded_at"] for e in events]
    intervals = [
        (timestamps[i + 1] - timestamps[i]).total_seconds()
        for i in range(len(timestamps) - 1)
    ]
    intervals = [i for i in intervals if i > 0]
    if len(intervals) < _MIN_EVENTS_FOR_TIMING - 1:
        return False

    mean_interval = statistics.mean(intervals)
    if mean_interval == 0:
        return False

    coefficient_of_variation = statistics.pstdev(intervals) / mean_interval
    return coefficient_of_variation < _UNIFORM_TIMING_CV_THRESHOLD

def _paste_ratio(events: List[TelemetryEvent]) -> float:
    input_events = [e for e in events if e["event_type"] in _INPUT_EVENT_TYPES]
    if not input_events:
        return 0.0
    paste_events = [e for e in input_events if e["event_type"] == "paste"]
    return len(paste_events) / len(input_events)

def _has_large_paste_burst(events: List[TelemetryEvent]) -> bool:
    for event in events:
        if event["event_type"] != "paste":
            continue
        char_count = (event.get("metadata") or {}).get("char_count", 0)
        if isinstance(char_count, (int, float)) and char_count >= _LARGE_PASTE_CHAR_THRESHOLD:
            return True
    return False

def _context_switch_rate(events: List[TelemetryEvent]) -> float:
    if not events:
        return 0.0
    blur_events = [e for e in events if e["event_type"] == "window_blur"]
    return len(blur_events) / len(events)
