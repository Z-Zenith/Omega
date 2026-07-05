import logging
from typing import List, Optional
from fastapi import FastAPI, HTTPException, status
from pydantic import BaseModel, Field, field_validator

from src.similarity import compute_pairwise_similarity, SubmissionItem, SimilarityMatch

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

class SubmissionItemSchema(BaseModel):
    id: str = Field(..., description="Unique submission identifier")
    content: str = Field(..., description="Source code or text content of the submission")

    @field_validator("id")
    @classmethod
    def id_must_not_be_empty(cls, v: str) -> str:
        if not v.strip():
            raise ValueError("id must not be empty or whitespace")
        return v

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

    @field_validator("submissions")
    @classmethod
    def must_have_at_least_two_submissions(cls, v: List[SubmissionItemSchema]) -> List[SubmissionItemSchema]:
        # Allow sending fewer submissions, but note that similarity checks won't yield matches
        return v

class SimilarityMatchSchema(BaseModel):
    submission_a_id: str
    submission_b_id: str
    similarity_score: float

class SimilarityResponse(BaseModel):
    matches: List[SimilarityMatchSchema]

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
