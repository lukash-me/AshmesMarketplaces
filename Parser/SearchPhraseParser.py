import requests
from loguru import logger
import json
from dto import DataPage
from common_data import headers_with_wbaas_token
import time
import os
from datetime import datetime, timezone
from typing import Callable
from app.proxy_transport import requests_proxy_kwargs
from app.proxy_rate_limiter import SyncProxyRateLimiter, global_filter_sync_proxy_rate_limiter
from app.browser_sessions import invalidate_proxy_session


# Парсинг на основе поисковых запросов
class SearchPhraseParser:
    def __init__(
            self,
            search_phrase: str,
            cookies: dict = None,
            dest: str = "12354108",
            timeout: int = 10,
            max_retries: int = 2,
            request_delay_bounds: tuple[float, float] = (0.4, 1.2),
            event_recorder: Callable | None = None,
            source_category: str | None = None,
            source_subcategory: str | None = None,
            proxy_url: str | None = None,
            proxy_key: str | None = None,
            rate_limiter: SyncProxyRateLimiter | None = None,
            split_progress_recorder: Callable | None = None,
            max_retryable_statuses: int | None = None,
            max_total_retryable_attempts: int | None = None):
        self.search_phrase = search_phrase
        self.cookies = cookies
        self.dest = dest
        self.timeout = timeout
        self.max_retries = max_retries
        self.request_delay_bounds = request_delay_bounds
        self.event_recorder = event_recorder
        self.source_category = source_category
        self.source_subcategory = source_subcategory
        self.proxy_url = proxy_url
        self.proxy_key = proxy_key or "direct"
        self.rate_limiter = rate_limiter or global_filter_sync_proxy_rate_limiter()
        self.split_progress_recorder = split_progress_recorder
        self.max_retryable_statuses = max_retryable_statuses or _env_int("PARSER_FILTERS_MAX_RETRYABLE_STATUSES", 8)
        self.max_total_retryable_attempts = max_total_retryable_attempts or _env_int(
            "PARSER_FILTERS_MAX_TOTAL_RETRYABLE_ATTEMPTS",
            40,
        )
        self.retryable_statuses_count = 0
        self.consecutive_retryable_statuses = 0
        self.aborted_by_rate_limit = False
        self.final_error: str | None = None
        self.max_filter_backoff_seconds = _env_float("PARSER_FILTER_MAX_BACKOFF_SECONDS", 180.0)
        self.filter_backoff_base_seconds = _env_float("PARSER_FILTER_BACKOFF_BASE_SECONDS", 3.0)
        self.filter_rate_limit_cooldown_seconds = _env_float("PARSER_FILTER_RATE_LIMIT_COOLDOWN_SECONDS", 120.0)
        self._split_processed_ranges = 0
        self._split_pending_ranges = 0
        self.range_checks_count = 0
        self.final_ranges_count = 0
        self.empty_ranges_count = 0
        self.split_ranges_count = 0

        self.default_step = 500 * 100
        self.max_count_of_good = 5000

        self.min_step = 10 * 100
        self.max_step = 5000 * 100

        self.max_split_depth = 10
        self.low_goods_threshold = 500

    def _emit_parser_event(self, event_name: str, *, phase: str = "ranges", extra: dict | None = None) -> None:
        payload = {
            "event": event_name,
            "timestampUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
            "proxyKey": self.proxy_key,
            "phase": phase,
            "sourceCategory": self.source_category,
            "sourceSubcategory": self.source_subcategory,
            "sourceQuery": self.search_phrase,
            "rangeChecksCount": max(0, int(self.range_checks_count)),
            "finalRangesCount": max(0, int(self.final_ranges_count)),
            "emptyRangesCount": max(0, int(self.empty_ranges_count)),
            "splitRangesCount": max(0, int(self.split_ranges_count)),
        }
        if extra:
            payload.update(extra)
        logger.info("PARSER_EVENT {}", json.dumps(payload, ensure_ascii=False, separators=(",", ":")))

    def _emit_split_progress(self) -> None:
        if not self.split_progress_recorder:
            return

        processed = max(0, int(self.range_checks_count))
        pending = max(0, int(self._split_pending_ranges))
        denominator = max(1, processed + pending)
        progress = min(99.0, (processed / denominator) * 100.0)
        self.split_progress_recorder(
            processed_ranges=processed,
            pending_ranges=pending,
            progress_percent=progress,
            range_checks_count=processed,
            final_ranges_count=max(0, int(self.final_ranges_count)),
            empty_ranges_count=max(0, int(self.empty_ranges_count)),
            split_ranges_count=max(0, int(self.split_ranges_count)),
        )

    def _begin_split_unit(self) -> None:
        self._split_pending_ranges += 1
        self._emit_split_progress()

    def _finish_split_unit(self) -> None:
        self._split_pending_ranges = max(0, self._split_pending_ranges - 1)
        self._emit_split_progress()

    def _record_analyzed_range(self, data: DataPage | None, *, force_final: bool = False) -> None:
        if data is None:
            self._emit_split_progress()
            return

        self.range_checks_count += 1
        self._split_processed_ranges = self.range_checks_count

        total = max(0, int(data.total or 0))
        if total == 0:
            self.empty_ranges_count += 1
            event_name = "range_empty"
        elif force_final or total <= self.max_count_of_good:
            self.final_ranges_count += 1
            event_name = "range_final"
        else:
            self.split_ranges_count += 1
            event_name = "range_split"

        self._emit_parser_event(
            event_name,
            extra={
                "minPriceU": data.min_price,
                "maxPriceU": data.max_price,
                "rangeTotal": total,
            },
        )
        self._emit_split_progress()

    def _record_error(
            self,
            *,
            message: str,
            http_status: int | None = None,
            wb_code: str | int | None = None,
            attempt: int | None = None,
            action: str | None = None,
            details: dict | None = None):
        if not self.event_recorder:
            return

        self.event_recorder(
            phase="filters",
            source_category=self.source_category,
            source_subcategory=self.source_subcategory,
            source_query=self.search_phrase,
            message=message,
            http_status=http_status,
            wb_code=wb_code,
            attempt=attempt,
            action=action,
            details=details,
        )

    @staticmethod
    def _extract_wb_code(payload: dict | None):
        if not isinstance(payload, dict):
            return None
        return payload.get("code") or payload.get("error") or payload.get("errorCode")

    @staticmethod
    def _is_retryable_status(status_code: int) -> bool:
        return status_code in {429, 498} or status_code >= 500

    @staticmethod
    def _is_auth_or_antibot_status(status_code: int) -> bool:
        return status_code in {401, 403, 498}

    def _backoff_seconds(self, *, status_code: int, attempt: int) -> float:
        multiplier = max(1, self.consecutive_retryable_statuses)
        if status_code in {401, 403}:
            multiplier = max(multiplier, 2)
        if status_code == 498:
            multiplier = max(multiplier, 2)
        seconds = self.filter_backoff_base_seconds * multiplier
        seconds = max(seconds, 0.75 * max(1, attempt))
        if status_code == 429 and self.consecutive_retryable_statuses >= self.max_retryable_statuses:
            seconds = max(seconds, self.filter_rate_limit_cooldown_seconds)
        return min(self.max_filter_backoff_seconds, seconds)

    def _defer_proxy(self, seconds: float) -> None:
        defer = getattr(self.rate_limiter, "defer", None)
        if callable(defer):
            defer(self.proxy_key, seconds)
            return
        time.sleep(seconds)

    def _mark_retry_budget_exhausted(
            self,
            *,
            status_code: int,
            attempt: int,
            price_range: str | None = None) -> None:
        self.aborted_by_rate_limit = True
        self.final_error = (
            "WB filters retry budget exhausted: "
            f"last_status={status_code}, "
            f"consecutive_retryable={self.consecutive_retryable_statuses}, "
            f"total_retryable={self.retryable_statuses_count}, "
            f"attempt={attempt}"
            + (f", range={price_range}" if price_range else "")
        )

    def fetch_data(self, add_params: dict = None):
        params = {
            'ab_testing': 'false',
            'appType': '1',
            'autoselectFilters': 'false',
            'curr': 'rub',
            'dest': self.dest,
            'lang': 'ru',
            'locale': 'ru',
            'query': self.search_phrase,
            'resultset': 'filters',
            'spp': '30',
            'suppressSpellcheck': 'false',
        }
        if add_params:
            params.update(add_params)
            logger.debug(add_params)

        price_range = str(params.get("priceU") or "") or None
        attempt = 0
        request_exception_count = 0
        auth_retry_used = False

        while True:
            attempt += 1
            self.rate_limiter.wait(self.proxy_key)

            try:
                response = requests.get(
                    "https://search.wb.ru/exactmatch/ru/common/v18/search",
                    params=params,
                    cookies=self.cookies,
                    headers=headers_with_wbaas_token(self.cookies),
                    timeout=self.timeout,
                    **requests_proxy_kwargs(self.proxy_url))
            except requests.RequestException as err:
                logger.warning(f"WB filters request failed: {err}")
                request_exception_count += 1
                self._record_error(
                    message=str(err),
                    attempt=attempt,
                    action="retry" if request_exception_count <= self.max_retries else "stopped",
                    details={"proxyKey": self.proxy_key, "priceRange": price_range},
                )
                if request_exception_count <= self.max_retries:
                    time.sleep(0.75 * attempt)
                    continue
                return None

            if response.status_code == 200:
                try:
                    payload = response.json()
                except ValueError:
                    self.final_error = "WB filters response is not JSON"
                    self._record_error(
                        message=self.final_error,
                        attempt=attempt,
                        action="stopped",
                        details={"proxyKey": self.proxy_key, "priceRange": price_range},
                    )
                    return None

                wb_code = self._extract_wb_code(payload)
                if wb_code:
                    self._record_error(
                        message="WB filters response contains error code",
                        wb_code=wb_code,
                        attempt=attempt,
                        action="retry" if attempt <= self.max_retries else "stopped",
                        details={"proxyKey": self.proxy_key, "priceRange": price_range},
                    )
                    if attempt <= self.max_retries:
                        time.sleep(0.75 * attempt)
                        continue

                self.consecutive_retryable_statuses = 0
                return payload

            logger.error(f"WB status: {response.status_code}")
            status_code = int(response.status_code)
            if self._is_auth_or_antibot_status(status_code):
                invalidate_proxy_session(self.proxy_key, reason=f"WB filters HTTP status {response.status_code}")

            retry_allowed = False
            backoff_seconds = 0.0

            if status_code in {401, 403} and not auth_retry_used:
                auth_retry_used = True
                retry_allowed = True
                backoff_seconds = self._backoff_seconds(status_code=status_code, attempt=attempt)
            elif self._is_retryable_status(status_code):
                self.retryable_statuses_count += 1
                self.consecutive_retryable_statuses += 1
                retry_limit = (
                    self.max_total_retryable_attempts
                    if status_code == 429
                    else self.max_retryable_statuses
                )
                if self.consecutive_retryable_statuses < retry_limit:
                    retry_allowed = True
                    backoff_seconds = self._backoff_seconds(status_code=status_code, attempt=attempt)
                else:
                    self._mark_retry_budget_exhausted(
                        status_code=status_code,
                        attempt=attempt,
                        price_range=price_range,
                    )
            else:
                self.final_error = f"WB filters HTTP status {status_code}"

            action = "retry" if retry_allowed else "stopped"
            self._record_error(
                message=f"WB filters HTTP status {status_code}",
                http_status=status_code,
                attempt=attempt,
                action=action,
                details={
                    "proxyKey": self.proxy_key,
                    "priceRange": price_range,
                    "consecutiveRetryable": self.consecutive_retryable_statuses,
                    "totalRetryable": self.retryable_statuses_count,
                    "backoffSeconds": round(backoff_seconds, 3),
                    "authRetryUsed": auth_retry_used,
                },
            )

            if action == "retry":
                self._defer_proxy(backoff_seconds)
                continue

            return None

        return None

    @staticmethod
    def _get_total(data: json):
        return data.get("data", {}).get("total")

    @staticmethod
    def _get_min_max_price(data: json) -> tuple:
        filters = data.get("data", {}).get("filters", [])

        for _filter in filters:
            if _filter.get("name") == "Цена":
                return _filter.get("minPriceU"), _filter.get("maxPriceU")
        return None, None

    def get_price_range(
            self,
            data: json,
            fallback_min_price: int | None = None,
            fallback_max_price: int | None = None) -> DataPage | None:
        if not data:
            logger.error("No data")
            return None

        total = self._get_total(data=data)
        min_price, max_price = self._get_min_max_price(data=data)

        if total == 0 and fallback_min_price is not None and fallback_max_price is not None:
            return DataPage(min_price=fallback_min_price, max_price=fallback_max_price, total=0)

        if total is None or min_price is None or max_price is None:
            logger.error("No enough data")
            logger.debug(f"{min_price}, {max_price}, {total}")
            return None

        return DataPage(min_price=min_price, max_price=max_price, total=total)

    def split_price_range(self, min_price, max_price, depth=0) -> list[DataPage]:
        if self.aborted_by_rate_limit:
            logger.warning("WB filters split stopped because proxy is rate limited")
            return []

        if depth > self.max_split_depth:
            logger.warning("Превышена глубина дробления")
            return []

        if max_price - min_price <= self.min_step:
            logger.warning("Минимальный шаг достигнут")
            self._begin_split_unit()
            res = self.fetch_data(add_params={"priceU": f'{min_price};{max_price}'})
            data = self.get_price_range(res, min_price, max_price)
            self._record_analyzed_range(data, force_final=True)
            self._finish_split_unit()

            if self.aborted_by_rate_limit:
                return []

            if not data or int(data.total or 0) == 0:
                return []
            return [DataPage(min_price, max_price, data.total)]

        self._begin_split_unit()
        res = self.fetch_data(add_params={"priceU": f'{min_price};{max_price}'})
        data = self.get_price_range(res, min_price, max_price)
        self._record_analyzed_range(data)
        self._finish_split_unit()

        if self.aborted_by_rate_limit:
            return []

        if not data:
            logger.error("Нет данных")
            return []

        if int(data.total or 0) == 0:
            logger.info("Ok - empty range")
            return []

        if data.total <= self.max_count_of_good:
            logger.info(f"Ok - {data.total}")
            return [DataPage(min_price, max_price, data.total)]

        logger.warning(f"Дробим - {data.total}")

        mid = (min_price + max_price) // 2

        left = self.split_price_range(min_price=min_price, max_price=mid, depth=depth + 1)
        right = self.split_price_range(min_price=mid + 1, max_price=max_price, depth=depth + 1)

        return left + right

    def parse(self):
        logger.info(f"Начало парсинга <{self.search_phrase}>")

        base_data = self.get_price_range(data=self.fetch_data())
        if self.aborted_by_rate_limit:
            logger.warning("WB filters parsing stopped because proxy is rate limited")
            return []

        if not base_data:
            logger.error("Не удалось получить данные")
            return None

        result: list[DataPage] = []
        step = self.default_step
        start_price = base_data.min_price

        while start_price < base_data.max_price:
            finish_price = min(start_price + step, base_data.max_price)

            logger.info(f"Диапазон: {start_price / 100} - {finish_price / 100}. Шаг {step / 100}")

            self._begin_split_unit()
            res = self.fetch_data(
                add_params={"priceU": f'{start_price};{finish_price}'}
            )
            data = self.get_price_range(data=res, fallback_min_price=start_price, fallback_max_price=finish_price)
            self._record_analyzed_range(data)
            self._finish_split_unit()

            if self.aborted_by_rate_limit:
                logger.warning("WB filters parsing stopped because proxy is rate limited")
                return []

            if not data:
                logger.warning("Нет данных")
                start_price = finish_price

                step = self.max_step
                continue

            if data.total > self.max_count_of_good:
                logger.warning(f"{data.total} - дробим диапазон")

                sub_ranges = self.split_price_range(start_price, finish_price)
                result.extend(sub_ranges)

                start_price = finish_price
                step = self.default_step
                continue

            if int(data.total or 0) == 0:
                logger.info("Empty price range")
                start_price = finish_price
                step = self.max_step
                continue

            if data.total < self.low_goods_threshold:
                logger.info("Мало товаров")
                step = self.max_step

            else:
                step = self.default_step

            logger.info(f"Принят диапазон: {data.total}")

            result.append((
                DataPage(start_price, finish_price, data.total)
            ))

            start_price = finish_price

        logger.info(f"Всего {len(result)} диапазонов")
        logger.info(result[1:5])
        self._emit_parser_event(
            "split_completed",
            phase="download",
            extra={
                "finalRangesCount": len(result),
                "plannedProductsCount": sum(max(0, int(item.total or 0)) for item in result),
            },
        )
        return result


def _env_int(name: str, default: int) -> int:
    raw_value = os.getenv(name)
    if raw_value is None:
        return default
    try:
        value = int(raw_value)
    except ValueError:
        logger.warning(f"Invalid integer value for {name}: {raw_value!r}; using {default}")
        return default
    return value if value > 0 else default


def _env_float(name: str, default: float) -> float:
    raw_value = os.getenv(name)
    if raw_value is None:
        return default
    try:
        value = float(raw_value)
    except ValueError:
        logger.warning(f"Invalid float value for {name}: {raw_value!r}; using {default}")
        return default
    return value if value > 0 else default


if __name__ == "__main__":
    from app.browser_sessions import get_token_for_proxy

    wb_token = get_token_for_proxy("direct")
    cookies = {
        'x_wbaas_token': wb_token,
    }

    res = SearchPhraseParser(search_phrase="menu_redirect_subject_v2_631 обувь для девочек", cookies=cookies).parse()



    result = SearchPhraseParser(search_phrase="menu_redirect_subject_v2_645 обувь для мальчиков", cookies=cookies).parse()
