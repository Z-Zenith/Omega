import logging
from collections import defaultdict
from datetime import datetime
from typing import List, Optional, TypedDict
from urllib.parse import urlparse

logger = logging.getLogger(__name__)

class BrowsingVisit(TypedDict):
    url: str
    visited_at: datetime
    duration_seconds: Optional[int]

_TOP_DOMAINS_IN_SUMMARY = 3

def _domain_of(url: str) -> str:
    netloc = urlparse(url).netloc or url
    return netloc[4:] if netloc.startswith("www.") else netloc

def generate_browsing_summary(visits: List[BrowsingVisit]) -> str:
    """
    Generates a natural-language summary of a student's in-app browsing history
    (AIS-01). Visibility of the result is enforced by the caller via the
    view_browsing_history permission — this function has no notion of who's asking.

    Args:
        visits: Recorded page visits, each with a url, timestamp, and optional
            time-on-page. Callers are expected to have already scoped this to
            whatever history window they want summarized.

    Returns:
        A short paragraph describing total activity and the most-visited sites.
    """
    if not visits:
        logger.info("Browsing summary: no visits recorded.")
        return "No browsing activity has been recorded for this student."

    domain_visit_counts: dict[str, int] = defaultdict(int)
    domain_durations: dict[str, int] = defaultdict(int)
    total_duration = 0
    has_duration_data = False

    for visit in visits:
        domain = _domain_of(visit["url"])
        domain_visit_counts[domain] += 1
        duration = visit.get("duration_seconds")
        if duration is not None:
            has_duration_data = True
            domain_durations[domain] += duration
            total_duration += duration

    timestamps = [v["visited_at"] for v in visits]
    earliest, latest = min(timestamps), max(timestamps)

    top_domains = sorted(
        domain_visit_counts.keys(),
        key=lambda d: (domain_visit_counts[d], domain_durations.get(d, 0)),
        reverse=True,
    )[:_TOP_DOMAINS_IN_SUMMARY]

    domain_phrases = []
    for domain in top_domains:
        visit_count = domain_visit_counts[domain]
        visit_word = "visit" if visit_count == 1 else "visits"
        if has_duration_data and total_duration > 0:
            share = domain_durations.get(domain, 0) / total_duration * 100
            domain_phrases.append(f"{domain} ({visit_count} {visit_word}, {share:.0f}% of tracked time)")
        else:
            domain_phrases.append(f"{domain} ({visit_count} {visit_word})")

    summary = (
        f"Recorded {len(visits)} page visit(s) across {len(domain_visit_counts)} site(s) "
        f"between {earliest.date()} and {latest.date()}. "
        f"Most visited: {', '.join(domain_phrases)}."
    )

    logger.info(f"Browsing summary generated: {len(visits)} visits, {len(domain_visit_counts)} domains.")
    return summary
