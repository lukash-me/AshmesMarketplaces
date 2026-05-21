import requests
from loguru import logger
import json
from dto import DataPage
from common_data import HEADERS
from get_token import get_token
import time
import random
from typing import Callable


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
            source_subcategory: str | None = None):
        self.search_phrase = search_phrase
        self.cookies = cookies
        self.dest = dest
        self.timeout = timeout
        self.max_retries = max_retries
        self.request_delay_bounds = request_delay_bounds
        self.event_recorder = event_recorder
        self.source_category = source_category
        self.source_subcategory = source_subcategory

        self.default_step = 500 * 100
        self.max_count_of_good = 5000

        self.min_step = 10 * 100
        self.max_step = 5000 * 100

        self.max_split_depth = 10
        self.low_goods_threshold = 500

    def _record_error(
            self,
            *,
            message: str,
            http_status: int | None = None,
            wb_code: str | int | None = None,
            attempt: int | None = None,
            action: str | None = None):
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
        )

    @staticmethod
    def _extract_wb_code(payload: dict | None):
        if not isinstance(payload, dict):
            return None
        return payload.get("code") or payload.get("error") or payload.get("errorCode")

    @staticmethod
    def _is_retryable_status(status_code: int) -> bool:
        return status_code in {429, 498} or status_code >= 500

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

        for attempt in range(1, self.max_retries + 2):
            delay_min, delay_max = self.request_delay_bounds
            if delay_max > 0:
                time.sleep(random.uniform(delay_min, delay_max))

            try:
                response = requests.get(
                    "https://www.wildberries.ru/__internal/u-search/exactmatch/ru/common/v18/search",
                    params=params,
                    cookies=self.cookies,
                    headers=HEADERS,
                    timeout=self.timeout)
            except requests.RequestException as err:
                logger.warning(f"WB filters request failed: {err}")
                self._record_error(message=str(err), attempt=attempt, action="retry")
                if attempt <= self.max_retries:
                    time.sleep(0.75 * attempt)
                    continue
                return None

            if response.status_code == 200:
                try:
                    payload = response.json()
                except ValueError:
                    self._record_error(message="WB filters response is not JSON", attempt=attempt, action="stopped")
                    return None

                wb_code = self._extract_wb_code(payload)
                if wb_code:
                    self._record_error(
                        message="WB filters response contains error code",
                        wb_code=wb_code,
                        attempt=attempt,
                        action="retry" if attempt <= self.max_retries else "stopped")
                    if attempt <= self.max_retries:
                        time.sleep(0.75 * attempt)
                        continue

                return payload

            logger.error(f"WB status: {response.status_code}")
            action = "retry" if self._is_retryable_status(response.status_code) and attempt <= self.max_retries else "stopped"
            self._record_error(
                message=f"WB filters HTTP status {response.status_code}",
                http_status=response.status_code,
                attempt=attempt,
                action=action)

            if action == "retry":
                time.sleep(0.75 * attempt)
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

    def get_price_range(self, data: json) -> DataPage | None:
        if not data:
            logger.error("No data")
            return None

        total = self._get_total(data=data)
        min_price, max_price = self._get_min_max_price(data=data)

        if total is None or min_price is None or max_price is None:
            logger.error("No enough data")
            logger.debug(f"{min_price}, {max_price}, {total}")
            return None

        return DataPage(min_price=min_price, max_price=max_price, total=total)

    def split_price_range(self, min_price, max_price, depth=0) -> list[DataPage]:
        if depth > self.max_split_depth:
            logger.warning("Превышена глубина дробления")
            return []

        if max_price - min_price <= self.min_step:
            logger.warning("Минимальный шаг достигнут")
            res = self.fetch_data(add_params={"priceU": f'{min_price};{max_price}'})
            data = self.get_price_range(res)

            return [DataPage(min_price, max_price, data.total) if data else 0]

        res = self.fetch_data(add_params={"priceU": f'{min_price};{max_price}'})
        data = self.get_price_range(res)

        if not data:
            logger.error("Нет данных")
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
        if not base_data:
            logger.error("Не удалось получить данные")
            return None

        result: list[DataPage] = []
        step = self.default_step
        start_price = base_data.min_price

        while start_price < base_data.max_price:
            finish_price = min(start_price + step, base_data.max_price)

            logger.info(f"Диапазон: {start_price / 100} - {finish_price / 100}. Шаг {step / 100}")

            res = self.fetch_data(
                add_params={"priceU": f'{start_price};{finish_price}'}
            )
            data = self.get_price_range(data=res)

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
        return result

if __name__ == "__main__":
    wb_token = get_token()
    cookies = {
        'x_wbaas_token': wb_token,
    }

    res = SearchPhraseParser(search_phrase="menu_redirect_subject_v2_631 обувь для девочек", cookies=cookies).parse()



    result = SearchPhraseParser(search_phrase="menu_redirect_subject_v2_645 обувь для мальчиков", cookies=cookies).parse()
