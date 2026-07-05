import logging
import re
from typing import List, TypedDict
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity

logger = logging.getLogger(__name__)

class SubmissionItem(TypedDict):
    id: str
    content: str

class SimilarityMatch(TypedDict):
    submission_a_id: str
    submission_b_id: str
    similarity_score: float

def clean_code_or_text(content: str) -> str:
    """
    Strips single-line and multi-line comments, docstrings, and normalizes spacing
    to ensure similarity checks focus purely on structural logic and content.
    Supported styles: Python (#), C/C++/Java/JS (//, /* */).
    """
    if not content:
        return ""
        
    # 1. Strip Python/Shell single-line comments (#)
    # Be careful not to strip # inside quotes (approximate rule for speed and safety)
    content = re.sub(r'(?m)^[ \t]*#.*$', '', content)  # Full-line comments
    content = re.sub(r'(?m)(?<=[^\'"] )#.*$', '', content)  # Inline comments with leading space

    # 2. Strip C-style single line comments (//)
    content = re.sub(r'(?m)^[ \t]*//.*$', '', content)
    content = re.sub(r'(?m)(?<=[^\'"] )//.*$', '', content)

    # 3. Strip C-style multi-line comments (/* */)
    content = re.sub(r'/\*.*?\*/', '', content, flags=re.DOTALL)

    # 4. Strip Python docstrings or multi-line quotes (''' ''' or """ """)
    content = re.sub(r"'''[\s\S]*?'''", '', content)
    content = re.sub(r'"""[\s\S]*?"""', '', content)

    # 5. Normalize spacing (multiple spaces/tabs to single space)
    content = re.sub(r'[ \t]+', ' ', content)
    
    return content.strip()

def compute_pairwise_similarity(
    submissions: List[SubmissionItem], 
    threshold: float = 0.90
) -> List[SimilarityMatch]:
    """
    Computes pairwise cosine similarity between all submissions using TF-IDF vectorization after cleaning.
    Returns pairs of submissions that meet or exceed the similarity threshold.
    
    Args:
        submissions: A list of dicts with keys 'id' and 'content'.
        threshold: Floating point score between 0.0 and 1.0 (default 0.90).
        
    Returns:
        A list of dicts containing matching pairs and their similarity scores.
    """
    n = len(submissions)
    if n < 2:
        logger.info(f"Skipping similarity check: only {n} submission(s) provided.")
        return []

    logger.info(f"Computing pairwise similarity for {n} submissions (threshold={threshold}).")
    
    # Preprocess/clean all submission contents
    cleaned_contents = [clean_code_or_text(sub["content"]) for sub in submissions]
    submission_ids = [sub["id"] for sub in submissions]

    if not any(cleaned_contents):
        logger.info("Skipping similarity check: all submissions are empty after cleaning.")
        return []

    try:
        # TF-IDF Vectorization
        # Use a token pattern that preserves alphanumeric tokens.
        vectorizer = TfidfVectorizer(
            token_pattern=r"(?u)\b\w+\b",
            lowercase=True,
            min_df=1
        )
        tfidf_matrix = vectorizer.fit_transform(cleaned_contents)
        
        # Compute Cosine Similarity Matrix
        similarity_matrix = cosine_similarity(tfidf_matrix, tfidf_matrix)
        
        flagged_matches: List[SimilarityMatch] = []
        
        # Iterate over the upper triangle of the matrix to avoid self-comparison and duplicate pairs
        for i in range(n):
            for j in range(i + 1, n):
                score = float(similarity_matrix[i][j])
                if score >= threshold:
                    logger.warning(
                        f"Copy-check flag raised: {submission_ids[i]} and {submission_ids[j]} "
                        f"have similarity {score:.4f}"
                    )
                    flagged_matches.append({
                        "submission_a_id": submission_ids[i],
                        "submission_b_id": submission_ids[j],
                        "similarity_score": round(score, 4)
                    })
                    
        return flagged_matches
        
    except Exception as e:
        logger.error(f"Error during similarity computation: {str(e)}", exc_info=True)
        raise RuntimeError("Failed to compute similarity matrix") from e
