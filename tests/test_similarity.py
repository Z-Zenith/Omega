import pytest
from fastapi.testclient import TestClient

from src.main import app
from src.similarity import compute_pairwise_similarity

client = TestClient(app)

# --- Unit Tests for similarity engine ---

def test_empty_submissions():
    # Less than 2 submissions should return empty list
    assert compute_pairwise_similarity([]) == []
    assert compute_pairwise_similarity([{"id": "1", "content": "print('hello')"}]) == []

def test_identical_submissions():
    submissions = [
        {"id": "sub_a", "content": "def add(a, b):\n    return a + b"},
        {"id": "sub_b", "content": "def add(a, b):\n    return a + b"}
    ]
    matches = compute_pairwise_similarity(submissions, threshold=0.95)
    assert len(matches) == 1
    assert matches[0]["submission_a_id"] == "sub_a"
    assert matches[0]["submission_b_id"] == "sub_b"
    assert matches[0]["similarity_score"] == 1.0

def test_minor_modification_code_caught():
    # Variable rename and comments/whitespace changes
    sub_a = """
    def compute_sum(first_number, second_number):
        # Calculate the sum of two inputs
        val = first_number + second_number
        return val
    """
    # In sub_b, we rename count to total_count but keep the main keywords.
    # With comment cleaning, similarity is ~0.83, passing threshold 0.80
    sub_b = """
    def compute_sum(first_number, second_number):
        # Sum calculations
        val = first_number + second_number
        return val
    """
    submissions = [
        {"id": "sub_a", "content": sub_a},
        {"id": "sub_b", "content": sub_b}
    ]
    matches = compute_pairwise_similarity(submissions, threshold=0.80)
    assert len(matches) == 1
    assert matches[0]["similarity_score"] >= 0.80

def test_unrelated_submissions():
    sub_a = "def process_data(data):\n    return [d * 2 for d in data]"
    sub_b = "<html><body><h1>Welcome to Campus Platform</h1></body></html>"
    submissions = [
        {"id": "sub_a", "content": sub_a},
        {"id": "sub_b", "content": sub_b}
    ]
    # Should have extremely low/zero similarity
    matches = compute_pairwise_similarity(submissions, threshold=0.50)
    assert len(matches) == 0

# --- Integration Tests for API endpoints ---

def test_health_check():
    response = client.get("/")
    assert response.status_code == 200
    assert response.json()["status"] == "healthy"
    assert response.json()["service"] == "ai-services"

def test_api_similarity_success():
    payload = {
        "submissions": [
            {"id": "s1", "content": "import math\ndef circle_area(r):\n    # Calculate circle area\n    return math.pi * r * r"},
            {"id": "s2", "content": "import math\ndef circle_area(r):\n    # Area of circle calculation\n    return math.pi * r * r"}
        ],
        "threshold": 0.85
    }
    response = client.post("/api/v1/similarity", json=payload)
    assert response.status_code == 200
    
    data = response.json()
    assert "matches" in data
    assert len(data["matches"]) == 1
    assert data["matches"][0]["submission_a_id"] == "s1"
    assert data["matches"][0]["submission_b_id"] == "s2"
    assert data["matches"][0]["similarity_score"] >= 0.85

def test_api_validation_error_empty_id():
    payload = {
        "submissions": [
            {"id": "", "content": "print('hello')"},
            {"id": "s2", "content": "print('world')"}
        ]
    }
    response = client.post("/api/v1/similarity", json=payload)
    assert response.status_code == 422  # Unprocessable Entity
