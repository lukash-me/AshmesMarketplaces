from __future__ import annotations

import random
import json
import time
from dataclasses import dataclass
from typing import Any, Callable

import requests

from app.proxy_transport import requests_proxy_kwargs
from common_data import HEADERS
from product_details_contracts import build_card_info_url, request_fingerprint


@dataclass
class ProductDetailFetchResult:
    wb_product_id: str
    endpoint: str
    request_fingerprint: str
    status: str
    payload: dict[str, Any] | None
    attempts: int
    retries: int
    http_status: int | None = None
    message: str | None = None
    is_transient: bool = False


class WbProductDetailsClient:
    def __init__(
        self,
        *,
        timeout_sec: int = 10,
        retries: int = 2,
        delay_ms: int = 300,
        delay_provider: Callable[[], None] | None = None,
        proxy_url: str | None = None,
    ) -> None:
        self.timeout_sec = timeout_sec
        self.retries = retries
        self.delay_ms = delay_ms
        self.delay_provider = delay_provider
        self.proxy_url = proxy_url
        self.session = requests.Session()
        self.session.headers.update(HEADERS)

    def fetch_product(self, *, wb_product_id: str) -> ProductDetailFetchResult:
        endpoint = build_card_info_url(wb_product_id)
        fingerprint = request_fingerprint(endpoint=endpoint)
        retries_used = 0
        attempts_made = 0
        last_status: int | None = None
        last_message: str | None = None
        last_transient = False

        for attempt in range(1, self.retries + 2):
            attempts_made = attempt
            self._delay()
            try:
                response = self.session.get(endpoint, timeout=self.timeout_sec, **requests_proxy_kwargs(self.proxy_url))
                last_status = response.status_code
                last_transient = self._is_retryable_status(response.status_code)
                if response.status_code == 200:
                    try:
                        payload = json.loads(response.content.decode("utf-8-sig"))
                    except (UnicodeDecodeError, ValueError):
                        return self._failed(
                            wb_product_id,
                            endpoint,
                            fingerprint,
                            attempt,
                            retries_used,
                            response.status_code,
                            "WB product details payload is not JSON.",
                            is_transient=False,
                        )

                    if isinstance(payload, dict):
                        return ProductDetailFetchResult(
                            wb_product_id=wb_product_id,
                            endpoint=endpoint,
                            request_fingerprint=fingerprint,
                            status="succeeded",
                            payload=payload,
                            attempts=attempt,
                            retries=retries_used,
                            http_status=response.status_code,
                        )

                    return self._failed(
                        wb_product_id,
                        endpoint,
                        fingerprint,
                        attempt,
                        retries_used,
                        response.status_code,
                        "WB product details payload root is not an object.",
                        is_transient=False,
                    )

                if response.status_code == 404:
                    return ProductDetailFetchResult(
                        wb_product_id=wb_product_id,
                        endpoint=endpoint,
                        request_fingerprint=fingerprint,
                        status="empty",
                        payload=None,
                        attempts=attempt,
                        retries=retries_used,
                        http_status=response.status_code,
                        message="WB product details were not found.",
                        is_transient=False,
                    )

                last_message = f"WB product details HTTP status {response.status_code}."
                if not last_transient:
                    break
            except requests.RequestException as exception:
                last_message = str(exception) or repr(exception)
                last_transient = True

            if attempt <= self.retries:
                retries_used += 1
                time.sleep(min(30.0, attempt + random.uniform(0.2, 1.0)))

        return self._failed(
            wb_product_id,
            endpoint,
            fingerprint,
            attempts_made,
            retries_used,
            last_status,
            last_message or "WB product details request failed.",
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

    @staticmethod
    def _failed(
        wb_product_id: str,
        endpoint: str,
        fingerprint: str,
        attempts: int,
        retries: int,
        http_status: int | None,
        message: str,
        *,
        is_transient: bool,
    ) -> ProductDetailFetchResult:
        return ProductDetailFetchResult(
            wb_product_id=wb_product_id,
            endpoint=endpoint,
            request_fingerprint=fingerprint,
            status="failed",
            payload=None,
            attempts=attempts,
            retries=retries,
            http_status=http_status,
            message=message,
            is_transient=is_transient,
        )
