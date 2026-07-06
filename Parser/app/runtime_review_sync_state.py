from __future__ import annotations

import os
from dataclasses import dataclass
from typing import Any
from urllib.parse import urlencode, urljoin

import requests


@dataclass(frozen=True)
class ReviewSyncState:
    wb_product_id: str
    marketplace_feedback_count: int | None
    fetched_reviews_count: int
    coverage_status: str
    latest_review_date_utc: str | None
    known_review_ids: frozenset[str]
    unanswered_review_ids: frozenset[str]

    @property
    def has_complete_review_history(self) -> bool:
        if self.coverage_status != "full":
            return False
        if self.marketplace_feedback_count is None:
            return False
        return self.fetched_reviews_count >= self.marketplace_feedback_count


def runtime_review_sync_state_url(
    wb_product_id: str,
    batch_queue_url: str | None = None,
) -> str | None:
    explicit = (os.environ.get("PARSER_RUNTIME_REVIEW_SYNC_STATE_URL") or "").strip()
    if explicit:
        separator = "&" if "?" in explicit else "?"
        return f"{explicit}{separator}{urlencode({'wbProductId': wb_product_id})}"

    base = (batch_queue_url or os.environ.get("PARSER_BATCH_QUEUE_URL") or "").strip()
    if not base:
        return None

    normalized = base.rstrip("/") + "/"
    if normalized.endswith("/api/v1/parser/"):
        base_url = urljoin(normalized, "runtime/review-sync-state")
    else:
        base_url = urljoin(normalized, "api/v1/parser/runtime/review-sync-state")

    return f"{base_url}?{urlencode({'wbProductId': wb_product_id})}"


def load_runtime_review_sync_state(
    url: str,
    *,
    api_key: str | None = None,
    timeout_seconds: float = 15,
) -> ReviewSyncState:
    headers = {}
    effective_api_key = (api_key or os.environ.get("PARSER_API_KEY") or "").strip()
    if effective_api_key:
        headers["X-Parser-Api-Key"] = effective_api_key

    response = requests.get(url, headers=headers, timeout=timeout_seconds)
    response.raise_for_status()
    return review_sync_state_from_dict(response.json())


def review_sync_state_from_dict(payload: dict[str, Any]) -> ReviewSyncState:
    return ReviewSyncState(
        wb_product_id=str(payload.get("wbProductId") or payload.get("wb_product_id") or ""),
        marketplace_feedback_count=_int_or_none(
            payload.get("marketplaceFeedbackCount") or payload.get("marketplace_feedback_count")
        ),
        fetched_reviews_count=_int_or_zero(
            payload.get("fetchedReviewsCount") or payload.get("fetched_reviews_count")
        ),
        coverage_status=str(payload.get("coverageStatus") or payload.get("coverage_status") or "unknown"),
        latest_review_date_utc=payload.get("latestReviewDateUtc") or payload.get("latest_review_date_utc"),
        known_review_ids=frozenset(_string_ids(payload.get("knownReviewIds") or payload.get("known_review_ids"))),
        unanswered_review_ids=frozenset(
            _string_ids(payload.get("unansweredReviewIds") or payload.get("unanswered_review_ids"))
        ),
    )


def _string_ids(value: Any) -> list[str]:
    if not isinstance(value, list):
        return []
    return [normalized for item in value if (normalized := str(item or "").strip())]


def _int_or_none(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _int_or_zero(value: Any) -> int:
    return _int_or_none(value) or 0
