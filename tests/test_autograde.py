import pytest
from fastapi.testclient import TestClient

from src.main import app
from src.autograde import generate_autograde_suggestion

client = TestClient(app)

_RUBRIC = [
    {"name": "Defines a function", "keywords": ["def", "function"], "weight": 0.5},
    {"name": "Handles the base case", "keywords": ["base case", "return"], "weight": 0.5},
]

# --- Unit tests for the autograde engine (AIS-04) ---

def test_ais_04_empty_submission_scores_zero():
    suggestion = generate_autograde_suggestion("", _RUBRIC, max_score=100.0)
    assert suggestion["suggested_grade"] == 0.0
    assert suggestion["matched_criteria"] == []
    assert suggestion["confidence"] == 1.0

def test_ais_04_full_rubric_match_scores_max():
    content = "def factorial(n):\n    # base case\n    return 1 if n == 0 else n * factorial(n - 1)"
    suggestion = generate_autograde_suggestion(content, _RUBRIC, max_score=100.0)
    assert suggestion["suggested_grade"] == 100.0
    assert set(suggestion["matched_criteria"]) == {"Defines a function", "Handles the base case"}

def test_ais_04_partial_match_scores_partial_weight():
    content = "def factorial(n):\n    pass"
    suggestion = generate_autograde_suggestion(content, _RUBRIC, max_score=100.0)
    assert suggestion["suggested_grade"] == 50.0
    assert suggestion["matched_criteria"] == ["Defines a function"]

def test_ais_04_no_match_scores_zero_but_nonempty_feedback():
    content = "this submission is entirely unrelated prose"
    suggestion = generate_autograde_suggestion(content, _RUBRIC, max_score=100.0)
    assert suggestion["suggested_grade"] == 0.0
    assert suggestion["matched_criteria"] == []
    assert len(suggestion["feedback"]) == len(_RUBRIC)

def test_ais_04_keyword_matching_is_whole_word_not_substring():
    # "definer" should not satisfy a "def" keyword match
    content = "the definer of this term is unclear"
    rubric = [{"name": "Has def keyword", "keywords": ["def"], "weight": 1.0}]
    suggestion = generate_autograde_suggestion(content, rubric, max_score=100.0)
    assert suggestion["suggested_grade"] == 0.0

def test_ais_04_respects_custom_max_score():
    content = "def factorial(n):\n    # base case\n    return 1"
    suggestion = generate_autograde_suggestion(content, _RUBRIC, max_score=10.0)
    assert suggestion["suggested_grade"] == 10.0
    assert suggestion["max_score"] == 10.0

# --- Integration tests for the /api/v1/autograde endpoint ---

def test_ais_04_api_autograde_success():
    payload = {
        "content": "def factorial(n):\n    # base case\n    return 1 if n == 0 else n * factorial(n - 1)",
        "rubric": _RUBRIC,
        "max_score": 100.0,
    }
    response = client.post("/api/v1/autograde", json=payload)
    assert response.status_code == 200

    data = response.json()
    assert data["suggested_grade"] == 100.0
    assert data["max_score"] == 100.0
    assert "Defines a function" in data["matched_criteria"]

def test_ais_04_api_rejects_rubric_weights_not_summing_to_one():
    payload = {
        "content": "def factorial(n): return 1",
        "rubric": [
            {"name": "A", "keywords": ["def"], "weight": 0.5},
            {"name": "B", "keywords": ["return"], "weight": 0.7},
        ],
        "max_score": 100.0,
    }
    response = client.post("/api/v1/autograde", json=payload)
    assert response.status_code == 422

def test_ais_04_api_rejects_empty_rubric():
    payload = {"content": "def factorial(n): return 1", "rubric": [], "max_score": 100.0}
    response = client.post("/api/v1/autograde", json=payload)
    assert response.status_code == 422

def test_ais_04_api_rejects_criterion_with_no_keywords():
    payload = {
        "content": "def factorial(n): return 1",
        "rubric": [{"name": "A", "keywords": [], "weight": 1.0}],
        "max_score": 100.0,
    }
    response = client.post("/api/v1/autograde", json=payload)
    assert response.status_code == 422
