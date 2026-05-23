from __future__ import annotations

import random
import time
from dataclasses import dataclass
from typing import Any, Callable

import requests

from common_data import HEADERS
from rank_config import RankContextConfig
from rank_contracts import (
    WB_SEARCH_ENDPOINT,
    build_search_params,
    products_from_payload,
    request_fingerprint,
    response_total,
)


@dataclass
class RankPageFetchResult:
    status: str
    params: dict[str, str]
    request_fingerprint: str
    page: int
    products: list[dict[str, Any]]
    response_total: int | None = None
    retry_count: int = 0
    http_status: int | None = None
    wb_code: str | int | None = None
    message: str | None = None


class WbRankFetcher:
    def __init__(
        self,
        *,
        cookies: dict[str, str] | None,
        timeout: int = 10,
        max_retries: int = 2,
        request_delay_bounds: tuple[float, float] = (2.0, 5.0),
        attempt_recorder: Callable | None = None,
        retry_recorder: Callable | None = None,
        backoff_recorder: Callable | None = None,
    ) -> None:
        self.cookies = cookies or {}
        self.timeout = timeout
        self.max_retries = max_retries
        self.request_delay_bounds = request_delay_bounds
        self.attempt_recorder = attempt_recorder
        self.retry_recorder = retry_recorder
        self.backoff_recorder = backoff_recorder

    @staticmethod
    def _extract_wb_code(payload: dict[str, Any] | None) -> str | int | None:
        if not isinstance(payload, dict):
            return None
        return payload.get("code") or payload.get("error") or payload.get("errorCode")

    @staticmethod
    def _is_retryable_status(status_code: int) -> bool:
        return status_code in {429, 498} or status_code >= 500

    def _delay(self) -> None:
        delay_min, delay_max = self.request_delay_bounds
        if delay_max > 0:
            time.sleep(random.uniform(delay_min, delay_max))

    def _backoff(self, attempt: int) -> None:
        seconds = min(60.0, (2 ** attempt) + random.uniform(0.5, 2.0))
        if self.backoff_recorder:
            self.backoff_recorder(seconds)
        time.sleep(seconds)

    def fetch_page(
        self,
        *,
        context: RankContextConfig,
        page: int,
        dest: str,
    ) -> RankPageFetchResult:
        params = build_search_params(context=context, page=page, dest=dest)
        fingerprint = request_fingerprint(endpoint=WB_SEARCH_ENDPOINT, params=params)
        retry_count = 0

        for attempt in range(1, self.max_retries + 2):
            self._delay()
            if self.attempt_recorder:
                self.attempt_recorder()

            try:
                response = requests.get(
                    WB_SEARCH_ENDPOINT,
                    params=params,
                    cookies=self.cookies,
                    headers=HEADERS,
                    timeout=self.timeout,
                )
            except requests.RequestException as exception:
                message = str(exception)
                if attempt <= self.max_retries:
                    retry_count += 1
                    if self.retry_recorder:
                        self.retry_recorder()
                    self._backoff(attempt)
                    continue

                return RankPageFetchResult(
                    status="failed",
                    params=params,
                    request_fingerprint=fingerprint,
                    page=page,
                    products=[],
                    retry_count=retry_count,
                    message=message,
                )

            if response.status_code == 200:
                try:
                    payload = response.json()
                except ValueError:
                    return RankPageFetchResult(
                        status="failed",
                        params=params,
                        request_fingerprint=fingerprint,
                        page=page,
                        products=[],
                        retry_count=retry_count,
                        http_status=response.status_code,
                        message="WB rank response is not JSON",
                    )

                wb_code = self._extract_wb_code(payload)
                if wb_code:
                    if attempt <= self.max_retries:
                        retry_count += 1
                        if self.retry_recorder:
                            self.retry_recorder()
                        self._backoff(attempt)
                        continue

                    return RankPageFetchResult(
                        status="failed",
                        params=params,
                        request_fingerprint=fingerprint,
                        page=page,
                        products=[],
                        response_total=response_total(payload),
                        retry_count=retry_count,
                        http_status=response.status_code,
                        wb_code=wb_code,
                        message="WB rank response contains error code",
                    )

                products = products_from_payload(payload)
                return RankPageFetchResult(
                    status="succeeded" if products else "empty",
                    params=params,
                    request_fingerprint=fingerprint,
                    page=page,
                    products=products,
                    response_total=response_total(payload),
                    retry_count=retry_count,
                    http_status=response.status_code,
                )

            if self._is_retryable_status(response.status_code) and attempt <= self.max_retries:
                retry_count += 1
                if self.retry_recorder:
                    self.retry_recorder()
                self._backoff(attempt)
                continue

            return RankPageFetchResult(
                status="failed",
                params=params,
                request_fingerprint=fingerprint,
                page=page,
                products=[],
                retry_count=retry_count,
                http_status=response.status_code,
                message=f"WB rank HTTP status {response.status_code}",
            )

        return RankPageFetchResult(
            status="failed",
            params=params,
            request_fingerprint=fingerprint,
            page=page,
            products=[],
            retry_count=retry_count,
            message="WB rank request attempts exhausted",
        )
