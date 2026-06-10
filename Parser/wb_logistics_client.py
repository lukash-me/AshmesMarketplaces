from __future__ import annotations

import random
import time
from dataclasses import dataclass
from typing import Any, Callable

import requests

from common_data import HEADERS
from logistics_contracts import WB_CARD_DETAIL_ENDPOINT, build_card_detail_params, request_fingerprint


@dataclass
class LogisticsFetchResult:
    wb_product_id: str
    endpoint: str
    params: dict[str, str]
    request_fingerprint: str
    status: str
    payload: dict[str, Any] | None
    attempts: int
    retries: int
    http_status: int | None = None
    message: str | None = None
    is_transient: bool = False


class WbLogisticsClient:
    def __init__(
        self,
        *,
        endpoint: str = WB_CARD_DETAIL_ENDPOINT,
        timeout_sec: int = 10,
        retries: int = 2,
        delay_ms: int = 500,
        delay_provider: Callable[[], None] | None = None,
    ) -> None:
        self.endpoint = endpoint
        self.timeout_sec = timeout_sec
        self.retries = retries
        self.delay_ms = delay_ms
        self.delay_provider = delay_provider
        self.session = requests.Session()
        self.session.headers.update(HEADERS)

    def fetch_product(self, *, wb_product_id: str, dest: str) -> LogisticsFetchResult:
        params = build_card_detail_params(wb_product_id=wb_product_id, dest=dest)
        fingerprint = request_fingerprint(endpoint=self.endpoint, params=params)
        retries_used = 0
        attempts_made = 0
        last_status: int | None = None
        last_message: str | None = None
        last_transient = False

        for attempt in range(1, self.retries + 2):
            attempts_made = attempt
            self._delay()
            try:
                response = self.session.get(self.endpoint, params=params, timeout=self.timeout_sec)
                last_status = response.status_code
                last_transient = self._is_retryable_status(response.status_code)
                if response.status_code == 200:
                    try:
                        payload = response.json()
                    except ValueError:
                        return LogisticsFetchResult(
                            wb_product_id=wb_product_id,
                            endpoint=self.endpoint,
                            params=params,
                            request_fingerprint=fingerprint,
                            status="failed",
                            payload=None,
                            attempts=attempt,
                            retries=retries_used,
                            http_status=response.status_code,
                            message="WB logistics payload is not JSON.",
                            is_transient=False,
                        )

                    if isinstance(payload, dict):
                        return LogisticsFetchResult(
                            wb_product_id=wb_product_id,
                            endpoint=self.endpoint,
                            params=params,
                            request_fingerprint=fingerprint,
                            status="succeeded",
                            payload=payload,
                            attempts=attempt,
                            retries=retries_used,
                            http_status=response.status_code,
                        )

                    return LogisticsFetchResult(
                        wb_product_id=wb_product_id,
                        endpoint=self.endpoint,
                        params=params,
                        request_fingerprint=fingerprint,
                        status="failed",
                        payload=None,
                        attempts=attempt,
                        retries=retries_used,
                        http_status=response.status_code,
                        message="WB logistics payload root is not an object.",
                        is_transient=False,
                    )

                last_message = f"WB logistics HTTP status {response.status_code}."
                if not last_transient:
                    break
            except requests.RequestException as exception:
                last_message = str(exception) or repr(exception)
                last_transient = True

            if attempt <= self.retries:
                retries_used += 1
                time.sleep(min(30.0, attempt + random.uniform(0.2, 1.0)))

        return LogisticsFetchResult(
            wb_product_id=wb_product_id,
            endpoint=self.endpoint,
            params=params,
            request_fingerprint=fingerprint,
            status="failed",
            payload=None,
            attempts=attempts_made,
            retries=retries_used,
            http_status=last_status,
            message=last_message or "WB logistics request failed.",
            is_transient=last_transient,
        )

    def _delay(self) -> None:
        if self.delay_provider is not None:
            self.delay_provider()
            return
        if self.delay_ms <= 0:
            return
        jitter = random.uniform(0.75, 1.25)
        time.sleep((self.delay_ms / 1000.0) * jitter)

    @staticmethod
    def _is_retryable_status(status_code: int) -> bool:
        return status_code in {408, 425, 429, 498} or status_code >= 500
