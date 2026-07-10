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

def strip_comments(content: str) -> str:
    """
    Removes single-line comments (# and //), C-style block comments (/* */), and
    triple-quoted strings/docstrings ('''...''' and \"\"\"...\"\"\"), while leaving
    single/double-quoted string literals completely intact — even when they contain
    a '#' or '//' sequence (e.g. `x = "cache #1"`, `url = "http://example.com"`).

    Implemented as a character-by-character scanner rather than a regex: telling
    "a comment marker" apart from "a comment marker that happens to be inside a
    string literal" requires tracking scanner state (are we inside a string right
    now?), which a regex lookbehind can only approximate and gets wrong on cases
    like `x = "cache #1"` (see #90) — the character immediately before the space
    preceding '#' is 'e', not a quote, so the old lookbehind heuristic stripped
    into the middle of the string literal.

    Supported comment styles: Python (#), C/C++/Java/JS (//, /* */).
    """
    if not content:
        return ""

    result: List[str] = []
    i = 0
    n = len(content)

    while i < n:
        ch = content[i]
        two = content[i:i + 2]
        three = content[i:i + 3]

        # Triple-quoted strings / docstrings: dropped entirely, matching this
        # codebase's existing treatment of them as removable documentation.
        if three in ("'''", '"""'):
            end = content.find(three, i + 3)
            i = n if end == -1 else end + 3
            continue

        # Single/double-quoted string literals: copied through verbatim, with
        # escape sequences respected so an escaped quote doesn't end the string
        # early (which would otherwise let a later '#'/'//' be misread as code).
        if ch in ("'", '"'):
            quote = ch
            result.append(ch)
            i += 1
            while i < n:
                c = content[i]
                if c == "\\" and i + 1 < n:
                    result.append(c)
                    result.append(content[i + 1])
                    i += 2
                    continue
                result.append(c)
                i += 1
                if c == quote:
                    break
            continue

        # Line comments: '#' (Python/shell) or '//' (C-like). Only reached when
        # not inside a string literal, so these never fire on quoted content.
        if ch == "#" or two == "//":
            newline = content.find("\n", i)
            i = n if newline == -1 else newline
            continue

        # Block comments: /* ... */
        if two == "/*":
            end = content.find("*/", i + 2)
            i = n if end == -1 else end + 2
            continue

        result.append(ch)
        i += 1

    return "".join(result)

def clean_code_or_text(content: str) -> str:
    """
    Strips single-line and multi-line comments, docstrings, and normalizes spacing
    to ensure similarity checks focus purely on structural logic and content.
    Supported styles: Python (#), C/C++/Java/JS (//, /* */).
    """
    if not content:
        return ""

    content = strip_comments(content)

    # Normalize spacing (multiple spaces/tabs to single space)
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
