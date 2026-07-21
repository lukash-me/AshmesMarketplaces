from __future__ import annotations

import asyncio
import random
import time
from dataclasses import dataclass
from typing import Any

import httpx
from loguru import logger

from common_data import HEADERS
from config import ReviewsParserConfig
from app.runtime_review_sync_state import ReviewSyncState
from app.proxy_transport import httpx_proxy_kwargs


@dataclass
class ReviewFetchResult:
    source_wb_root_id: str
    source_wb_product_id: str | None
    endpoint: str
    status: str
    payload: dict[str, Any] | None
    attempts: int
    retries: int
    backoff_seconds_total: float
    http_status: int | None
    elapsed_ms: int
    coverage_source: str = "root_capped_fallback"
    pages_fetched: int = 1
    error_summary: str | None = None
    exception_type: str | None = None
    fetched_new_reviews: int = 0
    fetched_new_replies: int = 0
    known_reviews_seen: int = 0
    existing_fetched_reviews_count: int | None = None
    marketplace_feedback_count: int | None = None
    sync_coverage_status: str | None = None


class WbReviewsClient:
    def __init__(self, config: ReviewsParserConfig):
        self.config = config
        self._client = httpx.AsyncClient(
            headers=HEADERS,
            timeout=config.timeout_seconds,
            **httpx_proxy_kwargs(),
        )

    async def __aenter__(self) -> "WbReviewsClient":
        return self

    async def __aexit__(self, exc_type, exc, traceback) -> None:
        await self._client.aclose()

    def endpoint_for(self, wb_root_id: str) -> str:
        return f"{self.config.endpoint_base}/{wb_root_id}"

    def product_endpoint_for(self, *, wb_product_id: str, wb_root_id: str, page: int = 1) -> str:
        offset = (page - 1) * self.config.page_size
        return self.config.product_endpoint_template.format(
            wb_product_id=wb_product_id,
            wb_root_id=wb_root_id,
            page=page,
            offset=offset,
            limit=self.config.page_size,
        )

    async def fetch_root(self, wb_root_id: str) -> ReviewFetchResult:
        endpoint = self.endpoint_for(wb_root_id)
        return await self._fetch_single(
            endpoint=endpoint,
            source_wb_root_id=wb_root_id,
            source_wb_product_id=None,
            coverage_source="root_capped_fallback",
        )

    async def fetch_root_variant(
        self,
        *,
        wb_product_id: str,
        wb_root_id: str,
        marketplace_feedback_count: int | None = None,
    ) -> ReviewFetchResult:
        endpoint = self.endpoint_for(wb_root_id)
        result = await self._fetch_single(
            endpoint=endpoint,
            source_wb_root_id=wb_root_id,
            source_wb_product_id=wb_product_id,
            coverage_source="root_variant_filtered",
        )
        result.marketplace_feedback_count = marketplace_feedback_count
        return result

    async def fetch_product(
        self,
        *,
        wb_product_id: str,
        wb_root_id: str,
        sync_state: ReviewSyncState | None = None,
        marketplace_feedback_count: int | None = None,
    ) -> ReviewFetchResult:
        started_at = time.perf_counter()
        effective_marketplace_feedback_count = (
            marketplace_feedback_count
            if marketplace_feedback_count is not None
            else (sync_state.marketplace_feedback_count if sync_state else None)
        )
        unchanged_review_count = (
            sync_state is not None
            and sync_state.marketplace_feedback_count is not None
            and effective_marketplace_feedback_count == sync_state.marketplace_feedback_count
        )
        if (
            sync_state is not None
            and unchanged_review_count
            and sync_state.has_complete_review_history
            and not sync_state.unanswered_review_ids
        ):
            return ReviewFetchResult(
                source_wb_root_id=wb_root_id,
                source_wb_product_id=wb_product_id,
                endpoint=self.product_endpoint_for(wb_product_id=wb_product_id, wb_root_id=wb_root_id),
                status="skipped",
                payload={
                    "feedbackCount": effective_marketplace_feedback_count,
                    "feedbacks": [],
                },
                attempts=0,
                retries=0,
                backoff_seconds_total=0.0,
                http_status=None,
                elapsed_ms=_elapsed_ms(started_at),
                coverage_source="product_full",
                pages_fetched=0,
                existing_fetched_reviews_count=sync_state.fetched_reviews_count,
                marketplace_feedback_count=effective_marketplace_feedback_count,
                sync_coverage_status=sync_state.coverage_status,
            )

        combined_payload: dict[str, Any] | None = None
        seen_review_ids: set[str] = set()
        known_review_ids = sync_state.known_review_ids if sync_state else frozenset()
        remaining_unanswered_ids = set(sync_state.unanswered_review_ids) if sync_state else set()
        fetched_new_reviews = 0
        fetched_new_replies = 0
        known_reviews_seen = 0
        attempts = 0
        retries = 0
        backoff_seconds_total = 0.0
        last_status: int | None = None
        last_endpoint = self.product_endpoint_for(
            wb_product_id=wb_product_id,
            wb_root_id=wb_root_id,
        )
        last_error: str | None = None
        last_exception: str | None = None
        supports_pages = "{page}" in self.config.product_endpoint_template or "{offset}" in self.config.product_endpoint_template

        for page in range(1, self.config.max_pages_per_product + 1):
            endpoint = self.product_endpoint_for(
                wb_product_id=wb_product_id,
                wb_root_id=wb_root_id,
                page=page,
            )
            last_endpoint = endpoint
            page_result = await self._fetch_single(
                endpoint=endpoint,
                source_wb_root_id=wb_root_id,
                source_wb_product_id=wb_product_id,
                coverage_source="product_full",
            )
            attempts += page_result.attempts
            retries += page_result.retries
            backoff_seconds_total += page_result.backoff_seconds_total
            last_status = page_result.http_status
            last_error = page_result.error_summary
            last_exception = page_result.exception_type

            if page_result.status == "failed":
                return ReviewFetchResult(
                    source_wb_root_id=wb_root_id,
                    source_wb_product_id=wb_product_id,
                    endpoint=last_endpoint,
                    status="failed",
                    payload=combined_payload,
                    attempts=attempts,
                    retries=retries,
                    backoff_seconds_total=round(backoff_seconds_total, 3),
                    http_status=last_status,
                    elapsed_ms=_elapsed_ms(started_at),
                    coverage_source="product_full",
                    pages_fetched=max(page - 1, 0),
                    error_summary=last_error or "WB product review fetch failed.",
                    exception_type=last_exception,
                )

            payload = page_result.payload or {}
            feedbacks = payload.get("feedbacks") or []
            if not isinstance(feedbacks, list):
                feedbacks = []

            if combined_payload is None:
                combined_payload = {**payload, "feedbacks": []}

            added_rows = 0
            for feedback in feedbacks:
                if not isinstance(feedback, dict):
                    continue
                review_id = str(feedback.get("id") or feedback.get("feedbackId") or feedback.get("reviewId") or "")
                if not review_id or review_id in seen_review_ids:
                    continue
                seen_review_ids.add(review_id)
                is_known = review_id in known_review_ids
                if is_known:
                    known_reviews_seen += 1
                answer_is_present = isinstance(feedback.get("answer"), dict)
                newly_observed_reply = review_id in remaining_unanswered_ids and answer_is_present
                if newly_observed_reply:
                    remaining_unanswered_ids.discard(review_id)

                if sync_state is not None and is_known and not newly_observed_reply:
                    continue

                combined_payload["feedbacks"].append(feedback)
                added_rows += 1
                if is_known:
                    fetched_new_replies += 1
                else:
                    fetched_new_reviews += 1

            if not supports_pages:
                if sync_state is not None and _payload_feedback_count(payload) > len(feedbacks):
                    logger.warning(
                        "product reviews endpoint is not paginated product={} endpoint={} feedbacks={} expected={}",
                        wb_product_id,
                        endpoint,
                        len(feedbacks),
                        _payload_feedback_count(payload),
                    )
                status = "success" if combined_payload and combined_payload.get("feedbacks") else "empty"
                return ReviewFetchResult(
                    source_wb_root_id=wb_root_id,
                    source_wb_product_id=wb_product_id,
                    endpoint=last_endpoint,
                    status=status,
                    payload=combined_payload or payload,
                    attempts=attempts,
                    retries=retries,
                    backoff_seconds_total=round(backoff_seconds_total, 3),
                    http_status=last_status,
                    elapsed_ms=_elapsed_ms(started_at),
                    coverage_source="product_full",
                    pages_fetched=page,
                    fetched_new_reviews=fetched_new_reviews,
                    fetched_new_replies=fetched_new_replies,
                    known_reviews_seen=known_reviews_seen,
                    existing_fetched_reviews_count=(
                        sync_state.fetched_reviews_count if sync_state else None
                    ),
                    marketplace_feedback_count=(
                        effective_marketplace_feedback_count
                    ),
                    sync_coverage_status=sync_state.coverage_status if sync_state else None,
                )

            reached_known_boundary = sync_state is not None and added_rows == 0 and known_reviews_seen > 0
            if reached_known_boundary and not remaining_unanswered_ids:
                status = "success" if combined_payload and combined_payload.get("feedbacks") else "empty"
                return ReviewFetchResult(
                    source_wb_root_id=wb_root_id,
                    source_wb_product_id=wb_product_id,
                    endpoint=last_endpoint,
                    status=status,
                    payload=combined_payload or payload,
                    attempts=attempts,
                    retries=retries,
                    backoff_seconds_total=round(backoff_seconds_total, 3),
                    http_status=last_status,
                    elapsed_ms=_elapsed_ms(started_at),
                    coverage_source="product_full",
                    pages_fetched=page,
                    fetched_new_reviews=fetched_new_reviews,
                    fetched_new_replies=fetched_new_replies,
                    known_reviews_seen=known_reviews_seen,
                    existing_fetched_reviews_count=(
                        sync_state.fetched_reviews_count if sync_state else None
                    ),
                    marketplace_feedback_count=(
                        effective_marketplace_feedback_count
                    ),
                    sync_coverage_status=sync_state.coverage_status if sync_state else None,
                )

            if len(feedbacks) < self.config.page_size:
                status = "success" if combined_payload and combined_payload.get("feedbacks") else "empty"
                return ReviewFetchResult(
                    source_wb_root_id=wb_root_id,
                    source_wb_product_id=wb_product_id,
                    endpoint=last_endpoint,
                    status=status,
                    payload=combined_payload or payload,
                    attempts=attempts,
                    retries=retries,
                    backoff_seconds_total=round(backoff_seconds_total, 3),
                    http_status=last_status,
                    elapsed_ms=_elapsed_ms(started_at),
                    coverage_source="product_full",
                    pages_fetched=page,
                    fetched_new_reviews=fetched_new_reviews,
                    fetched_new_replies=fetched_new_replies,
                    known_reviews_seen=known_reviews_seen,
                    existing_fetched_reviews_count=(
                        sync_state.fetched_reviews_count if sync_state else None
                    ),
                    marketplace_feedback_count=(
                        effective_marketplace_feedback_count
                    ),
                    sync_coverage_status=sync_state.coverage_status if sync_state else None,
                )

        return ReviewFetchResult(
            source_wb_root_id=wb_root_id,
            source_wb_product_id=wb_product_id,
            endpoint=last_endpoint,
            status="success" if combined_payload and combined_payload.get("feedbacks") else "empty",
            payload=combined_payload,
            attempts=attempts,
            retries=retries,
            backoff_seconds_total=round(backoff_seconds_total, 3),
            http_status=last_status,
            elapsed_ms=_elapsed_ms(started_at),
            coverage_source="product_full",
            pages_fetched=self.config.max_pages_per_product,
            fetched_new_reviews=fetched_new_reviews,
            fetched_new_replies=fetched_new_replies,
            known_reviews_seen=known_reviews_seen,
            existing_fetched_reviews_count=(
                sync_state.fetched_reviews_count if sync_state else None
            ),
            marketplace_feedback_count=(
                effective_marketplace_feedback_count
            ),
            sync_coverage_status=sync_state.coverage_status if sync_state else None,
        )

    async def _fetch_single(
        self,
        *,
        endpoint: str,
        source_wb_root_id: str,
        source_wb_product_id: str | None,
        coverage_source: str,
    ) -> ReviewFetchResult:
        started_at = time.perf_counter()
        attempts = 0
        retries = 0
        backoff_seconds_total = 0.0
        last_status: int | None = None
        last_error: str | None = None
        last_exception: str | None = None

        for attempt in range(1, self.config.max_retries + 2):
            attempts = attempt
            await self._request_delay()
            try:
                response = await self._client.get(endpoint)
                last_status = response.status_code
                if response.status_code == 200:
                    try:
                        payload = response.json()
                    except ValueError as exception:
                        last_error = "WB reviews payload is not JSON."
                        last_exception = type(exception).__name__
                    else:
                        if not isinstance(payload, dict):
                            last_error = "WB reviews payload root is not an object."
                        else:
                            feedbacks = payload.get("feedbacks") or []
                            status = "success" if feedbacks else "empty"
                            return ReviewFetchResult(
                                source_wb_root_id=source_wb_root_id,
                                source_wb_product_id=source_wb_product_id,
                                endpoint=endpoint,
                                status=status,
                                payload=payload,
                                attempts=attempts,
                                retries=retries,
                                backoff_seconds_total=round(backoff_seconds_total, 3),
                                http_status=response.status_code,
                                elapsed_ms=_elapsed_ms(started_at),
                                coverage_source=coverage_source,
                            )
                else:
                    last_error = f"WB reviews HTTP status {response.status_code}."
                    if not self._is_retryable_status(response.status_code):
                        break
            except httpx.RequestError as exception:
                last_error = str(exception) or repr(exception)
                last_exception = type(exception).__name__

            if attempt <= self.config.max_retries:
                retries += 1
                backoff_seconds = self._backoff_seconds(attempt)
                backoff_seconds_total += backoff_seconds
                logger.warning(
                    "Review root retry root={} attempt={} backoff={:.3f}s error={}",
                    source_wb_root_id,
                    attempt,
                    backoff_seconds,
                    last_error,
                )
                await asyncio.sleep(backoff_seconds)

        return ReviewFetchResult(
            source_wb_root_id=source_wb_root_id,
            source_wb_product_id=source_wb_product_id,
            endpoint=endpoint,
            status="failed",
            payload=None,
            attempts=attempts,
            retries=retries,
            backoff_seconds_total=round(backoff_seconds_total, 3),
            http_status=last_status,
            elapsed_ms=_elapsed_ms(started_at),
            coverage_source=coverage_source,
            error_summary=last_error or "WB review root fetch failed.",
            exception_type=last_exception,
        )

    async def smoke_probe(self, wb_root_id: str) -> ReviewFetchResult:
        return await self.fetch_root(wb_root_id)

    async def _request_delay(self) -> None:
        if self.config.request_delay_max_seconds <= 0:
            return

        await asyncio.sleep(
            random.uniform(
                self.config.request_delay_min_seconds,
                self.config.request_delay_max_seconds,
            )
        )

    def _backoff_seconds(self, attempt: int) -> float:
        exponential = self.config.backoff_base_seconds * (2 ** max(attempt - 1, 0))
        bounded = min(self.config.backoff_max_seconds, exponential)
        return bounded + random.uniform(0.0, min(1.0, max(bounded, 0.1)))

    @staticmethod
    def _is_retryable_status(status_code: int) -> bool:
        return status_code in {408, 425, 429, 498} or status_code >= 500


def _elapsed_ms(started_at: float) -> int:
    return int((time.perf_counter() - started_at) * 1000)


def _payload_feedback_count(payload: dict[str, Any]) -> int:
    value = payload.get("feedbackCount")
    try:
        return int(value)
    except (TypeError, ValueError):
        return 0
