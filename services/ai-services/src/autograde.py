import logging
import re
from typing import List, TypedDict

logger = logging.getLogger(__name__)

class RubricCriterion(TypedDict):
    name: str
    keywords: List[str]
    weight: float

class AutogradeSuggestion(TypedDict):
    suggested_grade: float
    max_score: float
    confidence: float
    matched_criteria: List[str]
    feedback: List[str]

# A submission with no rubric coverage at all still gets a floor confidence — the
# suggestion is never withheld, but a low confidence signals the teacher to look
# closer rather than rubber-stamp it (AIS-04: "never auto-published without teacher
# confirmation").
_BASE_CONFIDENCE = 0.5
_MATCHED_RATIO_CONFIDENCE_WEIGHT = 0.5
_MIN_CONTENT_LENGTH_FOR_FULL_CONFIDENCE = 20

def _normalize(content: str) -> str:
    return re.sub(r"\s+", " ", content).strip().lower()

def _keyword_present(keyword: str, normalized_content: str) -> bool:
    pattern = r"\b" + re.escape(keyword.strip().lower()) + r"\b"
    return re.search(pattern, normalized_content) is not None

def generate_autograde_suggestion(
    content: str,
    rubric: List[RubricCriterion],
    max_score: float = 100.0,
) -> AutogradeSuggestion:
    """
    Suggests a grade for a submission by checking rubric-criterion keyword coverage.
    This is advisory only (AIS-04, "Should" priority) — the caller is responsible for
    surfacing it exclusively to the teacher and never auto-publishing it as-is.

    Args:
        content: The student's submission text/code.
        rubric: Criteria to check for, each contributing its weight (0-1, summing to
            ~1.0) of max_score when at least one of its keywords is found.
        max_score: The maximum score for this assignment (default 100.0).

    Returns:
        An AutogradeSuggestion with the suggested grade, a confidence estimate, which
        criteria matched, and per-criterion feedback for the teacher to review.
    """
    normalized_content = _normalize(content)

    if not normalized_content:
        logger.info("Autograde suggestion: empty submission, suggesting 0.")
        return {
            "suggested_grade": 0.0,
            "max_score": max_score,
            "confidence": 1.0,
            "matched_criteria": [],
            "feedback": ["Submission is empty — no content to grade."],
        }

    matched_criteria: List[str] = []
    feedback: List[str] = []
    matched_weight = 0.0
    total_weight = sum(c["weight"] for c in rubric)

    for criterion in rubric:
        found = any(_keyword_present(kw, normalized_content) for kw in criterion["keywords"])
        if found:
            matched_criteria.append(criterion["name"])
            matched_weight += criterion["weight"]
            feedback.append(f"Criterion '{criterion['name']}' satisfied.")
        else:
            feedback.append(f"Criterion '{criterion['name']}' not found in submission.")

    matched_ratio = (matched_weight / total_weight) if total_weight > 0 else 0.0
    suggested_grade = round(min(matched_ratio, 1.0) * max_score, 2)

    length_factor = min(len(normalized_content) / _MIN_CONTENT_LENGTH_FOR_FULL_CONFIDENCE, 1.0)
    confidence = round(
        (_BASE_CONFIDENCE + _MATCHED_RATIO_CONFIDENCE_WEIGHT * matched_ratio) * length_factor,
        2,
    )

    logger.info(
        f"Autograde suggestion: grade={suggested_grade}/{max_score} "
        f"confidence={confidence} matched={matched_criteria}"
    )

    return {
        "suggested_grade": suggested_grade,
        "max_score": max_score,
        "confidence": confidence,
        "matched_criteria": matched_criteria,
        "feedback": feedback,
    }
