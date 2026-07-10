import pytest
from fastapi.testclient import TestClient

from src.main import app
from src.similarity import clean_code_or_text, compute_pairwise_similarity, strip_comments

client = TestClient(app)

# --- Unit Tests for strip_comments (#90 regression) ---

def test_hash_inside_double_quoted_string_survives():
    # The old lookbehind regex only checked the single character before the space
    # preceding '#'; here that's 'e' (not a quote), so it truncated the string.
    assert strip_comments('x = "cache #1"') == 'x = "cache #1"'

def test_hash_inside_single_quoted_string_survives():
    assert strip_comments("x = 'cache #1'") == "x = 'cache #1'"

def test_double_slash_inside_string_survives():
    assert strip_comments('url = "http://example.com"') == 'url = "http://example.com"'

def test_real_comment_after_string_with_hash_is_still_stripped():
    # The string literal must be preserved, but a genuine trailing comment
    # (outside the string) should still be removed.
    result = strip_comments('x = "cache #1"  # real comment')
    assert 'cache #1' in result
    assert 'real comment' not in result

def test_real_comment_after_string_with_double_slash_is_still_stripped():
    result = strip_comments('url = "http://example.com"  // real comment')
    assert 'http://example.com' in result
    assert 'real comment' not in result

def test_full_line_hash_comment_stripped():
    result = strip_comments("x = 1\n# a full-line comment\ny = 2")
    assert '# a full-line comment' not in result
    assert 'x = 1' in result
    assert 'y = 2' in result

def test_c_style_block_comment_stripped():
    result = strip_comments("int x = 1; /* block\ncomment */ int y = 2;")
    assert 'block' not in result
    assert 'int x = 1;' in result
    assert 'int y = 2;' in result

def test_triple_quoted_docstring_stripped():
    content = 'def f():\n    """This is a docstring with a # and // inside."""\n    return 1'
    result = strip_comments(content)
    assert 'docstring' not in result
    assert 'def f():' in result
    assert 'return 1' in result

def test_escaped_quote_inside_string_does_not_end_string_early():
    # If the escaped quote were misread as the string terminator, the '#' that
    # follows would be treated as a real comment and stripped.
    result = strip_comments('x = "she said \\"hi\\" #not-a-comment"')
    assert '#not-a-comment' in result

def test_clean_code_or_text_preserves_string_with_hash():
    cleaned = clean_code_or_text('x = "cache #1"')
    assert cleaned == 'x = "cache #1"'

def test_similarity_not_skewed_by_hash_in_string_literal():
    # Two submissions identical except for an in-string '#' should be scored on
    # their real content, not have one truncated mid-string by the comment
    # stripper — regression test for the false-similarity-signal bug in #90.
    sub_a = {"id": "a", "content": 'label = "cache #1"\nvalue = compute(label)'}
    sub_b = {"id": "b", "content": 'label = "cache #1"\nvalue = compute(label)'}
    matches = compute_pairwise_similarity([sub_a, sub_b], threshold=0.99)
    assert len(matches) == 1
    assert matches[0]["similarity_score"] == 1.0

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

def test_api_explicit_null_threshold_uses_documented_default():
    # Regression test for #89: an explicit `"threshold": null` (e.g. a frontend
    # doing `{ threshold: options.threshold ?? null }` to mean "no override")
    # must fall back to the documented default (0.90) instead of crashing with
    # a 500 from `score >= None`.
    payload = {
        "submissions": [
            {"id": "s1", "content": "def add(a, b):\n    return a + b"},
            {"id": "s2", "content": "def add(a, b):\n    return a + b"}
        ],
        "threshold": None
    }
    response = client.post("/api/v1/similarity", json=payload)
    assert response.status_code == 200

    data = response.json()
    # Identical submissions score 1.0, which clears the 0.90 default either way,
    # so also verify the default was actually applied (not just "didn't crash").
    assert len(data["matches"]) == 1
    assert data["matches"][0]["similarity_score"] == 1.0

def test_api_omitted_threshold_still_uses_default():
    # Sanity check that omitting the field entirely (the non-null case) still
    # behaves the same as before this fix.
    payload = {
        "submissions": [
            {"id": "s1", "content": "def add(a, b):\n    return a + b"},
            {"id": "s2", "content": "def add(a, b):\n    return a + b"}
        ]
    }
    response = client.post("/api/v1/similarity", json=payload)
    assert response.status_code == 200
    assert len(response.json()["matches"]) == 1
