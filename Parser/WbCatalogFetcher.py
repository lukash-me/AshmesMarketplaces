import asyncio
import math

import httpx

from loguru import logger
from common_data import HEADERS
from dto import DataPage
from typing import List
from get_token import get_token
from SearchPhraseParser import SearchPhraseParser
import random
from typing import Callable

class WbCatalogFetcher:
    def __init__(self,
                 pages: List[DataPage],
                 search_phrase: str,
                 cookies: dict,
                 dest: str='12354108',
                 batch_size: int=5,
                 max_concurrent: int=1,
                 timeout: int=10,
                 max_retries: int=2,
                 request_delay_bounds: tuple[float, float]=(2.0, 5.0),
                 batch_delay_bounds: tuple[float, float]=(15.0, 30.0),
                 max_catalog_pages: int | None=None,
                 max_limit_signals: int=2,
                 price_split_enabled: bool=True,
                 event_recorder: Callable | None=None,
                 attempt_recorder: Callable | None=None,
                 retry_recorder: Callable | None=None,
                 backoff_recorder: Callable | None=None,
                 source_category: str | None=None,
                 source_subcategory: str | None=None):

        self.pages = pages
        self.search_phrase = search_phrase
        self.cookies = cookies
        self.dest = dest
        self.headers = HEADERS

        self.batch_size = batch_size
        self.semaphore = asyncio.Semaphore(max_concurrent)
        self.timeout = timeout
        self.max_retries = max_retries
        self.request_delay_bounds = request_delay_bounds
        self.batch_delay_bounds = batch_delay_bounds
        self.max_catalog_pages = max_catalog_pages
        self.max_limit_signals = max_limit_signals
        self.price_split_enabled = price_split_enabled
        self.event_recorder = event_recorder
        self.attempt_recorder = attempt_recorder
        self.retry_recorder = retry_recorder
        self.backoff_recorder = backoff_recorder
        self.source_category = source_category
        self.source_subcategory = source_subcategory
        self.limit_signals = 0
        self.stop_requested = False

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
            phase="catalog",
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

    async def _backoff(self, attempt: int):
        seconds = min(60.0, (2 ** attempt) + random.uniform(0.5, 2.0))
        if self.backoff_recorder:
            self.backoff_recorder(seconds)
        await asyncio.sleep(seconds)

    def _mark_limit_signal(self):
        self.limit_signals += 1
        if self.limit_signals >= self.max_limit_signals:
            self.stop_requested = True


    def _build_tasks(self) -> list[dict]:
        tasks = []

        if not self.price_split_enabled:
            page_count = self.max_catalog_pages or 1
            for page_num in range(1, page_count + 1):
                tasks.append({"page": page_num})

            logger.info(f"Direct catalog mode tasks: {len(tasks)}")
            return tasks

        for page in self.pages:
            page_count = math.ceil(page.total / 100)

            for page_num in range(1, page_count+1):
                tasks.append({
                    "min_price": page.min_price,
                    "max_price": page.max_price,
                    "page": page_num,
                })

                if self.max_catalog_pages and len(tasks) >= self.max_catalog_pages:
                    logger.warning(f"Catalog page cap reached: {self.max_catalog_pages}")
                    logger.info(f"Сформировано задач: {len(tasks)}")
                    return tasks

        logger.info(f"Сформировано задач: {len(tasks)}")
        return tasks

    def _build_params(self, task: dict) -> dict:
        params = {
            'ab_testing': 'false',
            'appType': '1',
            'autoselectFilters': 'false',
            'curr': 'rub',
            'dest': self.dest,
            'inheritFilters': 'false',
            'lang': 'ru',
            'page': str(task["page"]),
            'locale': 'ru',
            'query': self.search_phrase,
            'resultset': 'catalog',
            'spp': '30',
            'suppressSpellcheck': 'false',
        }
        if self.price_split_enabled:
            params['priceU'] = f'{task["min_price"]};{task["max_price"]}'
        return params

    def _task_label(self, task: dict) -> str:
        if self.price_split_enabled:
            return f"page={task['page']} price={task['min_price']}-{task['max_price']}"
        return f"page={task['page']} direct"

    async def _fetch_one(self, client: httpx.AsyncClient, task: dict) -> dict | None:
        params = self._build_params(task=task)

        max_attempts = self.max_retries + 1
        for attempt in range(1, max_attempts + 1):
            if self.stop_requested:
                return None

            try:
                async with self.semaphore:
                    delay_min, delay_max = self.request_delay_bounds
                    if delay_max > 0:
                        await asyncio.sleep(random.uniform(delay_min, delay_max))

                    if self.attempt_recorder:
                        self.attempt_recorder()

                    response = await client.get(
                        'https://www.wildberries.ru/__internal/u-search/exactmatch/ru/common/v18/search',
                        params=params,
                        cookies=self.cookies,
                        headers=self.headers,
                        timeout=self.timeout
                    )
                    if response.status_code == 200:
                        try:
                            data = response.json()
                        except ValueError:
                            self._record_error(
                                message="WB catalog response is not JSON",
                                attempt=attempt,
                                action="stopped")
                            return None

                        wb_code = self._extract_wb_code(data)
                        if wb_code:
                            self._mark_limit_signal()
                            self._record_error(
                                message="WB catalog response contains error code",
                                wb_code=wb_code,
                                attempt=attempt,
                                action="retry" if attempt < max_attempts else "stopped")
                            if attempt < max_attempts and not self.stop_requested:
                                if self.retry_recorder:
                                    self.retry_recorder()
                                await self._backoff(attempt)
                                continue
                            return None

                        if "products" in data:
                            logger.debug(self._task_label(task))
                            return data
                        else:
                            logger.warning(f"No products {data} | attempt={attempt}")
                            self._record_error(
                                message="WB catalog response has no products",
                                attempt=attempt,
                                action="retry" if attempt < max_attempts else "skipped")
                    else:
                        logger.warning(f"status={response.status_code} "
                                       f"page={task['page']} | attempt={attempt}")
                        if self._is_retryable_status(response.status_code):
                            self._mark_limit_signal()
                        self._record_error(
                            message=f"WB catalog HTTP status {response.status_code}",
                            http_status=response.status_code,
                            attempt=attempt,
                            action="retry" if attempt < max_attempts else "stopped")

            except httpx.RequestError as err:
                logger.error(err)
                self._record_error(
                    message=str(err),
                    attempt=attempt,
                    action="retry" if attempt < max_attempts else "stopped")

            if attempt < max_attempts and not self.stop_requested:
                if self.retry_recorder:
                    self.retry_recorder()
                await self._backoff(attempt)

        logger.error(f"failed {self._task_label(task)}")
        return None

    async def iter_result_batches(self):
        tasks = self._build_tasks()
        total_results = 0

        async with httpx.AsyncClient() as client:
            for i in range(0, len(tasks), self.batch_size):
                if self.stop_requested:
                    logger.warning("Catalog fetching stopped after repeated WB limit signals")
                    break

                batch = tasks[i: i + self.batch_size]

                logger.info(f"Catalog request batch {i // self.batch_size + 1} ({len(batch)}) requests")

                coroutines = [
                    self._fetch_one(client, task) for task in batch
                ]

                batch_results = await asyncio.gather(*coroutines)
                batch_results = [r for r in batch_results if r]
                total_results += len(batch_results)

                logger.success(f"Catalog request batch completed, total responses: {total_results}")
                if batch_results:
                    yield batch_results

                delay_min, delay_max = self.batch_delay_bounds
                if delay_max > 0:
                    await asyncio.sleep(random.uniform(delay_min, delay_max))

        logger.info(f"Catalog fetching completed. Total responses: {total_results}")

    async def fetch_all(self) -> list[dict]:
        streamed_results: list[dict] = []
        async for batch_results in self.iter_result_batches():
            streamed_results.extend(batch_results)
        return streamed_results

        tasks = self._build_tasks()
        results: list[dict] = []

        async with httpx.AsyncClient() as client:
            for i in range(0, len(tasks), self.batch_size):
                if self.stop_requested:
                    logger.warning("Catalog fetching stopped after repeated WB limit signals")
                    break

                batch = tasks[i: i + self.batch_size]

                logger.info(f"Батч {i // self.batch_size + 1} "
                            f"({len(batch)}) запросов")

                coroutines = [
                    self._fetch_one(client, task) for task in batch
                ]

                batch_results = await asyncio.gather(*coroutines)
                batch_results = [r for r in batch_results if r]

                results.extend(batch_results)

                logger.success(f"Батч завершен, всего ответов: {len(results)}")

                delay_min, delay_max = self.batch_delay_bounds
                if delay_max > 0:
                    await asyncio.sleep(random.uniform(delay_min, delay_max))

        logger.info(f"Готово. Всего ответов: {len(results)}")

        return results

if __name__ == "__main__":
    wb_token = get_token()
    cookies = {
        'x_wbaas_token': wb_token,
    }

    ranges = SearchPhraseParser(search_phrase="menu_redirect_subject_v2_631 обувь для девочек", cookies=cookies).parse()

    fetcher = WbCatalogFetcher(search_phrase="menu_redirect_subject_v2_631 обувь для девочек", pages=ranges, cookies=cookies)

    results = asyncio.run(fetcher.fetch_all())

    logger.info(f"Сырые результаты для категории получены")

    for i, raw_data in enumerate(results):
        if "products" not in raw_data:
            print("BAD INDEX:", i)
            print(raw_data)
            break

