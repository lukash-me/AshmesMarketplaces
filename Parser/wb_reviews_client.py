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
from app.proxy_transport import httpx_proxy_kwargs


@dataclass
class ReviewFetchResult:
    source_wb_root_id: str
    endpoint: str
    status: str
    payload: dict[str, Any] | None
    attempts: int
    retries: int
    backoff_seconds_total: float
    http_status: int | None
    elapsed_ms: int
    error_summary: str | None = None
    exception_type: str | None = None


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

    async def fetch_root(self, wb_root_id: str) -> ReviewFetchResult:
        endpoint = self.endpoint_for(wb_root_id)
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
                                source_wb_root_id=wb_root_id,
                                endpoint=endpoint,
                                status=status,
                                payload=payload,
                                attempts=attempts,
                                retries=retries,
                                backoff_seconds_total=round(backoff_seconds_total, 3),
                                http_status=response.status_code,
                                elapsed_ms=_elapsed_ms(started_at),
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
                    wb_root_id,
                    attempt,
                    backoff_seconds,
                    last_error,
                )
                await asyncio.sleep(backoff_seconds)

        return ReviewFetchResult(
            source_wb_root_id=wb_root_id,
            endpoint=endpoint,
            status="failed",
            payload=None,
            attempts=attempts,
            retries=retries,
            backoff_seconds_total=round(backoff_seconds_total, 3),
            http_status=last_status,
            elapsed_ms=_elapsed_ms(started_at),
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
