from datetime import datetime, timezone

import pytest
from fastapi.testclient import TestClient

from src.main import app
from src.browsing_summary import generate_browsing_summary

client = TestClient(app)

# --- Unit tests for the summarizer (AIS-01) ---

def test_ais_01_no_visits_returns_no_activity_message():
    summary = generate_browsing_summary([])
    assert summary == "No browsing activity has been recorded for this student."

def test_ais_01_summary_mentions_total_visits_and_domain_count():
    visits = [
        {"url": "https://docs.example.com/page1", "visited_at": datetime(2026, 7, 1, tzinfo=timezone.utc), "duration_seconds": 60},
        {"url": "https://docs.example.com/page2", "visited_at": datetime(2026, 7, 2, tzinfo=timezone.utc), "duration_seconds": 30},
        {"url": "https://wiki.example.org/home", "visited_at": datetime(2026, 7, 3, tzinfo=timezone.utc), "duration_seconds": 10},
    ]
    summary = generate_browsing_summary(visits)
    assert "Recorded 3 page visit(s)" in summary
    assert "2 site(s)" in summary

def test_ais_01_strips_www_prefix_when_grouping_by_domain():
    visits = [
        {"url": "https://www.example.com/a", "visited_at": datetime(2026, 7, 1, tzinfo=timezone.utc), "duration_seconds": None},
        {"url": "https://example.com/b", "visited_at": datetime(2026, 7, 2, tzinfo=timezone.utc), "duration_seconds": None},
    ]
    summary = generate_browsing_summary(visits)
    assert "1 site(s)" in summary
    assert "example.com (2 visits)" in summary

def test_ais_01_domain_grouping_is_case_insensitive():
    visits = [
        {"url": "https://DOCS.example.com/a", "visited_at": datetime(2026, 7, 1, tzinfo=timezone.utc), "duration_seconds": None},
        {"url": "https://docs.example.com/b", "visited_at": datetime(2026, 7, 2, tzinfo=timezone.utc), "duration_seconds": None},
    ]
    summary = generate_browsing_summary(visits)
    assert "1 site(s)" in summary
    assert "docs.example.com (2 visits)" in summary

def test_ais_01_groups_schemeless_urls_by_domain():
    visits = [
        {"url": "example.com/a", "visited_at": datetime(2026, 7, 1, tzinfo=timezone.utc), "duration_seconds": None},
        {"url": "example.com/b", "visited_at": datetime(2026, 7, 2, tzinfo=timezone.utc), "duration_seconds": None},
    ]
    summary = generate_browsing_summary(visits)
    assert "1 site(s)" in summary
    assert "example.com (2 visits)" in summary

def test_ais_01_most_visited_domain_ranks_first():
    visits = (
        [{"url": "https://frequent.example/x", "visited_at": datetime(2026, 7, 1, tzinfo=timezone.utc), "duration_seconds": None}] * 5
        + [{"url": "https://rare.example/y", "visited_at": datetime(2026, 7, 2, tzinfo=timezone.utc), "duration_seconds": None}]
    )
    summary = generate_browsing_summary(visits)
    most_visited_clause = summary.split("Most visited: ")[1]
    assert most_visited_clause.startswith("frequent.example")

def test_ais_01_handles_visits_with_no_duration_data_gracefully():
    visits = [
        {"url": "https://a.example/1", "visited_at": datetime(2026, 7, 1, tzinfo=timezone.utc), "duration_seconds": None},
    ]
    summary = generate_browsing_summary(visits)
    assert "% of tracked time" not in summary
    assert "a.example (1 visit)" in summary

def test_ais_01_reports_duration_share_when_data_present():
    visits = [
        {"url": "https://heavy.example/1", "visited_at": datetime(2026, 7, 1, tzinfo=timezone.utc), "duration_seconds": 90},
        {"url": "https://light.example/1", "visited_at": datetime(2026, 7, 1, tzinfo=timezone.utc), "duration_seconds": 10},
    ]
    summary = generate_browsing_summary(visits)
    assert "heavy.example (1 visit, 90% of tracked time)" in summary

# --- Integration tests for the /api/v1/browsing-summary endpoint ---

def test_ais_01_api_browsing_summary_success():
    payload = {
        "visits": [
            {"url": "https://docs.example.com/page1", "visited_at": "2026-07-01T10:00:00Z", "duration_seconds": 120},
            {"url": "https://docs.example.com/page2", "visited_at": "2026-07-02T10:00:00Z"},
        ]
    }
    response = client.post("/api/v1/browsing-summary", json=payload)
    assert response.status_code == 200
    assert "docs.example.com" in response.json()["summary"]

def test_ais_01_api_browsing_summary_empty_visits_returns_no_activity_message():
    response = client.post("/api/v1/browsing-summary", json={"visits": []})
    assert response.status_code == 200
    assert response.json()["summary"] == "No browsing activity has been recorded for this student."

def test_ais_01_api_accepts_mixed_naive_and_aware_timestamps():
    payload = {
        "visits": [
            {"url": "https://a.example/1", "visited_at": "2026-07-01T10:00:00"},
            {"url": "https://b.example/1", "visited_at": "2026-07-02T10:00:00+05:30"},
        ]
    }
    response = client.post("/api/v1/browsing-summary", json=payload)
    assert response.status_code == 200

def test_ais_01_api_rejects_empty_url():
    payload = {"visits": [{"url": "", "visited_at": "2026-07-01T10:00:00Z"}]}
    response = client.post("/api/v1/browsing-summary", json=payload)
    assert response.status_code == 422

def test_ais_01_api_rejects_negative_duration():
    payload = {"visits": [{"url": "https://a.example/1", "visited_at": "2026-07-01T10:00:00Z", "duration_seconds": -5}]}
    response = client.post("/api/v1/browsing-summary", json=payload)
    assert response.status_code == 422
