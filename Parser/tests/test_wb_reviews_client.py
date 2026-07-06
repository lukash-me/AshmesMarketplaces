from __future__ import annotations

import asyncio
import sys
import unittest
from pathlib import Path
from typing import Any


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from app.runtime_review_sync_state import ReviewSyncState  # noqa: E402
from config import ReviewsParserConfig  # noqa: E402
from wb_reviews_client import ReviewFetchResult, WbReviewsClient  # noqa: E402


class WbReviewsClientIncrementalTests(unittest.TestCase):
    def test_complete_known_product_without_unanswered_reviews_is_skipped(self) -> None:
        client, calls = self._client_with_pages({})
        state = ReviewSyncState(
            wb_product_id="1001",
            marketplace_feedback_count=2,
            fetched_reviews_count=2,
            coverage_status="full",
            latest_review_date_utc=None,
            known_review_ids=frozenset({"known-1", "known-2"}),
            unanswered_review_ids=frozenset(),
        )

        result = asyncio.run(
            client.fetch_product(
                wb_product_id="1001",
                wb_root_id="5001",
                sync_state=state,
                marketplace_feedback_count=2,
            )
        )

        self.assertEqual(result.status, "skipped")
        self.assertEqual(result.pages_fetched, 0)
        self.assertEqual(result.existing_fetched_reviews_count, 2)
        self.assertEqual(calls, [])

    def test_fetches_only_new_reviews_until_known_boundary(self) -> None:
        client, calls = self._client_with_pages(
            {
                1: self._payload(
                    feedback_count=3,
                    reviews=[
                        self._review("new-2", "1001"),
                        self._review("new-1", "1001"),
                    ],
                ),
                2: self._payload(
                    feedback_count=3,
                    reviews=[self._review("known-1", "1001")],
                ),
            }
        )
        state = ReviewSyncState(
            wb_product_id="1001",
            marketplace_feedback_count=1,
            fetched_reviews_count=1,
            coverage_status="full",
            latest_review_date_utc=None,
            known_review_ids=frozenset({"known-1"}),
            unanswered_review_ids=frozenset(),
        )

        result = asyncio.run(
            client.fetch_product(
                wb_product_id="1001",
                wb_root_id="5001",
                sync_state=state,
                marketplace_feedback_count=3,
            )
        )

        self.assertEqual(calls, [1, 2])
        self.assertEqual(result.status, "success")
        self.assertEqual(result.fetched_new_reviews, 2)
        self.assertEqual(result.known_reviews_seen, 1)
        self.assertEqual(
            [row["id"] for row in result.payload["feedbacks"]],
            ["new-2", "new-1"],
        )

    def test_fetches_new_reply_for_known_unanswered_review(self) -> None:
        client, calls = self._client_with_pages(
            {
                1: self._payload(
                    feedback_count=1,
                    reviews=[
                        self._review(
                            "known-unanswered",
                            "1001",
                            answer={"text": "Спасибо за отзыв", "createDate": "2026-07-04T10:00:00Z"},
                        )
                    ],
                )
            }
        )
        state = ReviewSyncState(
            wb_product_id="1001",
            marketplace_feedback_count=1,
            fetched_reviews_count=1,
            coverage_status="full",
            latest_review_date_utc=None,
            known_review_ids=frozenset({"known-unanswered"}),
            unanswered_review_ids=frozenset({"known-unanswered"}),
        )

        result = asyncio.run(
            client.fetch_product(
                wb_product_id="1001",
                wb_root_id="5001",
                sync_state=state,
                marketplace_feedback_count=1,
            )
        )

        self.assertEqual(calls, [1])
        self.assertEqual(result.fetched_new_reviews, 0)
        self.assertEqual(result.fetched_new_replies, 1)
        self.assertEqual(result.payload["feedbacks"][0]["id"], "known-unanswered")
        self.assertIn("answer", result.payload["feedbacks"][0])

    def test_fetches_root_payload_for_variant_filtered_product_reviews(self) -> None:
        client, calls = self._client_with_root_payload(
            self._payload(
                feedback_count=85,
                reviews=[
                    self._review("foreign", "111927645"),
                    self._review("selected-1", "175139943"),
                    self._review("selected-2", "175139943"),
                ],
            )
        )

        result = asyncio.run(
            client.fetch_root_variant(
                wb_product_id="175139943",
                wb_root_id="174524123",
                marketplace_feedback_count=85,
            )
        )

        self.assertEqual(calls, ["https://example.test/reviews/174524123"])
        self.assertEqual(result.status, "success")
        self.assertEqual(result.coverage_source, "root_variant_filtered")
        self.assertEqual(result.source_wb_product_id, "175139943")
        self.assertEqual(result.marketplace_feedback_count, 85)
        self.assertEqual(
            [row["id"] for row in result.payload["feedbacks"]],
            ["foreign", "selected-1", "selected-2"],
        )

    def _client_with_pages(
        self,
        pages: dict[int, dict[str, Any]],
    ) -> tuple[WbReviewsClient, list[int]]:
        config = ReviewsParserConfig(
            product_endpoint_template="https://example.test/reviews/{wb_product_id}?page={page}",
            page_size=2,
            max_pages_per_product=5,
            request_delay_min_seconds=0,
            request_delay_max_seconds=0,
            max_retries=0,
        )
        client = object.__new__(WbReviewsClient)
        client.config = config
        calls: list[int] = []

        async def fake_fetch_single(
            *,
            endpoint: str,
            source_wb_root_id: str,
            source_wb_product_id: str | None,
            coverage_source: str,
        ) -> ReviewFetchResult:
            page = int(endpoint.rsplit("page=", 1)[1])
            calls.append(page)
            return ReviewFetchResult(
                source_wb_root_id=source_wb_root_id,
                source_wb_product_id=source_wb_product_id,
                endpoint=endpoint,
                status="success",
                payload=pages.get(page, self._payload(feedback_count=0, reviews=[])),
                attempts=1,
                retries=0,
                backoff_seconds_total=0.0,
                http_status=200,
                elapsed_ms=1,
                coverage_source=coverage_source,
            )

        client._fetch_single = fake_fetch_single  # type: ignore[method-assign]
        return client, calls

    def _client_with_root_payload(
        self,
        payload: dict[str, Any],
    ) -> tuple[WbReviewsClient, list[str]]:
        config = ReviewsParserConfig(
            endpoint_base="https://example.test/reviews",
            product_endpoint_template="https://example.test/reviews/{wb_product_id}?page={page}",
            request_delay_min_seconds=0,
            request_delay_max_seconds=0,
            max_retries=0,
        )
        client = object.__new__(WbReviewsClient)
        client.config = config
        calls: list[str] = []

        async def fake_fetch_single(
            *,
            endpoint: str,
            source_wb_root_id: str,
            source_wb_product_id: str | None,
            coverage_source: str,
        ) -> ReviewFetchResult:
            calls.append(endpoint)
            return ReviewFetchResult(
                source_wb_root_id=source_wb_root_id,
                source_wb_product_id=source_wb_product_id,
                endpoint=endpoint,
                status="success",
                payload=payload,
                attempts=1,
                retries=0,
                backoff_seconds_total=0.0,
                http_status=200,
                elapsed_ms=1,
                coverage_source=coverage_source,
                marketplace_feedback_count=85,
            )

        client._fetch_single = fake_fetch_single  # type: ignore[method-assign]
        return client, calls

    @staticmethod
    def _payload(*, feedback_count: int, reviews: list[dict[str, Any]]) -> dict[str, Any]:
        return {"feedbackCount": feedback_count, "feedbacks": reviews}

    @staticmethod
    def _review(
        review_id: str,
        wb_product_id: str,
        *,
        answer: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        row: dict[str, Any] = {
            "id": review_id,
            "nmId": int(wb_product_id),
            "productValuation": 5,
            "createdDate": "2026-07-04T09:00:00Z",
        }
        if answer is not None:
            row["answer"] = answer
        return row


if __name__ == "__main__":
    unittest.main()
