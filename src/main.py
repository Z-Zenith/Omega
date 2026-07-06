import logging
from datetime import datetime, timezone
from typing import List, Optional
from fastapi import FastAPI, HTTPException, status
from pydantic import BaseModel, Field, field_validator, model_validator

from src.similarity import compute_pairwise_similarity, SubmissionItem, SimilarityMatch
from src.anomaly import detect_suspicious_behaviour, TelemetryEvent, SuspiciousFlag
from src.autograde import generate_autograde_suggestion, RubricCriterion
from src.browsing_summary import generate_browsing_summary, BrowsingVisit

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    handlers=[logging.StreamHandler()]
)
logger = logging.getLogger("ai-services")

app = FastAPI(
    title="Campus Platform — AI Services",
    description="Microservice exposing copy-checking, plagiarism, and telemetry analysis APIs.",
    version="0.1.0"
)

# --- Pydantic Schemas ---

def _require_non_empty(v: str) -> str:
    if not v.strip():
        raise ValueError("must not be empty or whitespace")
    return v

class SubmissionItemSchema(BaseModel):
    id: str = Field(..., description="Unique submission identifier")
    content: str = Field(..., description="Source code or text content of the submission")

    @field_validator("id")
    @classmethod
    def id_must_not_be_empty(cls, v: str) -> str:
        return _require_non_empty(v)

class SimilarityRequest(BaseModel):
    submissions: List[SubmissionItemSchema] = Field(
        ..., 
        description="List of student submissions to check against each other"
    )
    threshold: Optional[float] = Field(
        0.90,
        ge=0.0,
        le=1.0,
        description="Cosine similarity threshold (default 0.90 / 90%)"
    )

class SimilarityMatchSchema(BaseModel):
    submission_a_id: str
    submission_b_id: str
    similarity_score: float

class SimilarityResponse(BaseModel):
    matches: List[SimilarityMatchSchema]

class TelemetryEventSchema(BaseModel):
    student_id: str = Field(..., description="Student the event belongs to")
    class_session_id: Optional[str] = Field(
        None, description="Active class session the event occurred during, if any"
    )
    assignment_id: Optional[str] = Field(
        None, description="Active assignment window the event occurred during, if any"
    )
    event_type: str = Field(
        ...,
        description="Telemetry event type, e.g. keystroke, paste, window_blur, window_focus, mouse_move"
    )
    metadata: dict = Field(
        default_factory=dict,
        description="Event-specific detail, e.g. char_count for paste events"
    )
    recorded_at: datetime = Field(..., description="Client-reported event timestamp")

    @field_validator("student_id", "event_type")
    @classmethod
    def field_must_not_be_empty(cls, v: str) -> str:
        return _require_non_empty(v)

    @field_validator("recorded_at")
    @classmethod
    def normalize_recorded_at(cls, v: datetime) -> datetime:
        # Client timestamps may or may not carry a timezone offset. Normalizing to
        # UTC here guarantees every event in a batch is mutually comparable — mixing
        # naive and aware datetimes crashes the sort in the anomaly engine otherwise.
        if v.tzinfo is None:
            return v.replace(tzinfo=timezone.utc)
        return v.astimezone(timezone.utc)

    @model_validator(mode="after")
    def must_belong_to_a_window(self):
        # AIS-07's acceptance criterion: "No analysis runs outside a class session or
        # assignment window." An event tied to neither isn't valid telemetry for this
        # endpoint — reject it here rather than silently scoring it.
        if self.class_session_id is None and self.assignment_id is None:
            raise ValueError("event must have a class_session_id or assignment_id")
        return self

class SuspiciousBehaviourRequest(BaseModel):
    events: List[TelemetryEventSchema] = Field(
        ...,
        description="Usage telemetry recorded during an active class session or assignment window (SDA-25)"
    )
    min_confidence: Optional[float] = Field(
        0.70,
        ge=0.0,
        le=1.0,
        description="Minimum combined confidence score required to surface a flag (default 0.70)"
    )

class SuspiciousFlagSchema(BaseModel):
    student_id: str
    class_session_id: Optional[str] = None
    assignment_id: Optional[str] = None
    confidence_score: float
    reasons: List[str]

class SuspiciousBehaviourResponse(BaseModel):
    flags: List[SuspiciousFlagSchema]

class RubricCriterionSchema(BaseModel):
    name: str = Field(..., description="Rubric criterion label shown to the teacher")
    keywords: List[str] = Field(
        ..., min_length=1, description="Any one of these (case-insensitive, whole-word) satisfies the criterion"
    )
    weight: float = Field(..., gt=0.0, le=1.0, description="Fraction of max_score this criterion is worth")

    @field_validator("name")
    @classmethod
    def name_must_not_be_empty(cls, v: str) -> str:
        return _require_non_empty(v)

    @field_validator("keywords")
    @classmethod
    def keywords_must_not_be_empty(cls, v: List[str]) -> List[str]:
        if not all(kw.strip() for kw in v):
            raise ValueError("keywords must not contain empty or whitespace-only entries")
        return v

class AutogradeRequest(BaseModel):
    content: str = Field(..., description="The student's submission text or code")
    rubric: List[RubricCriterionSchema] = Field(
        ..., min_length=1, description="Criteria to check the submission against"
    )
    max_score: float = Field(100.0, gt=0.0, description="Maximum score for this assignment")

    @model_validator(mode="after")
    def rubric_weights_must_sum_to_one(self):
        # A rubric whose weights don't sum to ~1.0 either under- or over-awards
        # max_score — catch the authoring mistake here rather than silently
        # producing a suggested grade that doesn't match the stated max_score.
        total_weight = sum(c.weight for c in self.rubric)
        if abs(total_weight - 1.0) > 0.01:
            raise ValueError(f"rubric weights must sum to 1.0 (got {total_weight:.4f})")
        return self

class AutogradeResponse(BaseModel):
    suggested_grade: float
    max_score: float
    confidence: float
    matched_criteria: List[str]
    feedback: List[str]

class BrowsingVisitSchema(BaseModel):
    url: str = Field(..., description="Visited page URL")
    visited_at: datetime = Field(..., description="Client-reported visit timestamp")
    duration_seconds: Optional[int] = Field(None, ge=0, description="Time spent on the page, if tracked")

    @field_validator("url")
    @classmethod
    def url_must_not_be_empty(cls, v: str) -> str:
        return _require_non_empty(v)

    @field_validator("visited_at")
    @classmethod
    def normalize_visited_at(cls, v: datetime) -> datetime:
        # Same rationale as TelemetryEventSchema.normalize_recorded_at: normalize to UTC
        # so min()/max() over a mixed naive/aware batch doesn't crash the summary.
        if v.tzinfo is None:
            return v.replace(tzinfo=timezone.utc)
        return v.astimezone(timezone.utc)

class BrowsingSummaryRequest(BaseModel):
    visits: List[BrowsingVisitSchema] = Field(
        ...,
        description="The student's recorded page visits to summarize (caller scopes the history window)"
    )

class BrowsingSummaryResponse(BaseModel):
    summary: str

# --- Routes ---

@app.get("/", status_code=status.HTTP_200_OK)
def read_root():
    """Health check endpoint."""
    return {
        "status": "healthy",
        "service": "ai-services",
        "version": "0.1.0"
    }

@app.post(
    "/api/v1/similarity", 
    response_model=SimilarityResponse, 
    status_code=status.HTTP_200_OK
)
async def check_similarity(payload: SimilarityRequest):
    """
    Computes pairwise similarity scores between all submitted documents.
    Returns pairs of submissions that exceed the specified threshold (default 0.90 / 90%).
    """
    try:
        # Map Pydantic models to typed dicts for similarity engine
        submission_data: List[SubmissionItem] = [
            {"id": item.id, "content": item.content} for item in payload.submissions
        ]
        
        # Calculate matches
        matches: List[SimilarityMatch] = compute_pairwise_similarity(
            submission_data, 
            threshold=payload.threshold
        )
        
        return {"matches": matches}
        
    except Exception as e:
        logger.error(f"Internal error processing similarity request: {str(e)}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to complete similarity comparison"
        )

@app.post(
    "/api/v1/suspicious-behaviour",
    response_model=SuspiciousBehaviourResponse,
    status_code=status.HTTP_200_OK
)
async def check_suspicious_behaviour(payload: SuspiciousBehaviourRequest):
    """
    Analyzes usage-pattern telemetry (SDA-25) recorded during an active class session
    or assignment window for signs of suspicious behaviour or automation. Callers are
    responsible for only submitting telemetry gathered within an active window — this
    endpoint scores whatever it is given and does not itself track window state.
    """
    try:
        telemetry_data: List[TelemetryEvent] = [
            {
                "student_id": e.student_id,
                "class_session_id": e.class_session_id,
                "assignment_id": e.assignment_id,
                "event_type": e.event_type,
                "metadata": e.metadata,
                "recorded_at": e.recorded_at,
            }
            for e in payload.events
        ]

        flags: List[SuspiciousFlag] = detect_suspicious_behaviour(
            telemetry_data,
            min_confidence=payload.min_confidence
        )

        return {"flags": flags}

    except Exception as e:
        logger.error(f"Internal error processing suspicious-behaviour request: {str(e)}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to complete suspicious-behaviour analysis"
        )

@app.post(
    "/api/v1/autograde",
    response_model=AutogradeResponse,
    status_code=status.HTTP_200_OK
)
async def suggest_autograde(payload: AutogradeRequest):
    """
    Suggests a grade for a submission against a weighted rubric (AIS-04). This is
    advisory only — callers must surface it exclusively in the Teacher Web App for
    review and must never auto-publish it or show it to the submitting student as-is.
    """
    try:
        rubric_data: List[RubricCriterion] = [
            {"name": c.name, "keywords": c.keywords, "weight": c.weight}
            for c in payload.rubric
        ]

        suggestion = generate_autograde_suggestion(
            payload.content,
            rubric_data,
            max_score=payload.max_score
        )

        return suggestion

    except Exception as e:
        logger.error(f"Internal error processing autograde request: {str(e)}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to complete autograde suggestion"
        )

@app.post(
    "/api/v1/browsing-summary",
    response_model=BrowsingSummaryResponse,
    status_code=status.HTTP_200_OK
)
async def browsing_summary(payload: BrowsingSummaryRequest):
    """
    Generates a natural-language summary of a student's in-app browsing history
    (AIS-01). Visibility is enforced by the caller via the view_browsing_history
    permission — this endpoint has no notion of who's asking and summarizes
    whatever visit history it's handed.
    """
    try:
        visit_data: List[BrowsingVisit] = [
            {"url": v.url, "visited_at": v.visited_at, "duration_seconds": v.duration_seconds}
            for v in payload.visits
        ]

        summary = generate_browsing_summary(visit_data)

        return {"summary": summary}

    except Exception as e:
        logger.error(f"Internal error processing browsing-summary request: {str(e)}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to complete browsing-summary generation"
        )
