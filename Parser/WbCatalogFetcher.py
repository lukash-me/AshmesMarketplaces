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

class WbCatalogFetcher:
    def __init__(self,
                 pages: List[DataPage],
                 search_phrase: str,
                 cookies: dict,
                 batch_size: int=50,
                 max_concurrent: int=10,
                 timeout: int=10,
                 max_retries: int=4,
                 pause_between_batches: float=random.uniform(2, 4)):

        self.pages = pages
        self.search_phrase = search_phrase
        self.cookies = cookies
        self.headers = HEADERS

        self.batch_size = batch_size
        self.semaphore = asyncio.Semaphore(max_concurrent)
        self.timeout = timeout
        self.max_retries = max_retries
        self.pause_between_batches = pause_between_batches


    def _build_tasks(self) -> list[dict]:
        tasks = []

        for page in self.pages:
            page_count = math.ceil(page.total / 100)

            for page_num in range(1, page_count+1):
                tasks.append({
                    "min_price": page.min_price,
                    "max_price": page.max_price,
                    "page": page_num,
                })

        logger.info(f"Сформировано задач: {len(tasks)}")
        return tasks

    def _build_params(self, task: dict) -> dict:
        return {
            'ab_testing': 'false',
            'appType': '1',
            'autoselectFilters': 'false',
            'curr': 'rub',
            'dest': '12354108',
            'inheritFilters': 'false',
            'lang': 'ru',
            'page': str(task["page"]),
            'priceU': f'{task["min_price"]};{task["max_price"]}',
            'locale': 'ru',
            'query': self.search_phrase,
            'resultset': 'catalog',
            'spp': '30',
            'suppressSpellcheck': 'false',
        }

    async def _fetch_one(self, client: httpx.AsyncClient, task: dict) -> dict | None:
        params = self._build_params(task=task)

        for attempt in range(1, self.max_retries + 1):
            try:
                async with self.semaphore:
                    response = await client.get(
                        'https://www.wildberries.ru/__internal/u-search/exactmatch/ru/common/v18/search',
                        params=params,
                        cookies=self.cookies,
                        headers=self.headers,
                        timeout=self.timeout
                    )
                    if response.status_code == 200:
                        data = response.json()
                        if "products" in data:
                            logger.debug(f"page={task['page']} "
                                         f"price={task['min_price']}-{task['max_price']}")
                            return data
                        else:
                            logger.warning(f"No products {data} | attempt={attempt}")
                    else:
                        logger.warning(f"status={response.status_code} "
                                       f"page={task['page']} | attempt={attempt}")

            except httpx.RequestError as err:
                logger.error(err)

            await asyncio.sleep(0.5 * attempt)

        logger.error(
            f"failed page={task['page']} "
            f"price={task['min_price']}-{task['max_price']}"
        )
        return None

    async def fetch_all(self) -> list[dict]:
        tasks = self._build_tasks()
        results: list[dict] = []

        async with httpx.AsyncClient() as client:
            for i in range(0, len(tasks), self.batch_size):
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

                await asyncio.sleep(self.pause_between_batches)

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

