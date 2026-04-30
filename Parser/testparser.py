import requests
from loguru import logger
from get_token import get_token
import json
from common_data import HEADERS

TOKEN = '1.1000.fce283080ef34ad49b48f48373413543.MTV8NS4xNDkuMjIxLjE4M3xNb3ppbGxhLzUuMCAoV2luZG93cyBOVCAxMC4wOyBXaW42NDsgeDY0KSBBcHBsZVdlYktpdC81MzcuMzYgKEtIVE1MLCBsaWtlIEdlY2tvKSBDaHJvbWUvMTQ3LjAuMC4wIFNhZmFyaS81MzcuMzZ8MTc3ODY4Njg4MHxyZXVzYWJsZXwyfGV5Sm9ZWE5vSWpvaUluMD18MHwzfDE3NzgwODIwODB8MQ==.MEUCIGSN8ZMQflBDQonWU0uL9gg3CYiwKLaxEdO8BREwHK0KAiEA+kM6Eiz50S8IQFSAQrjw8jprILmwYARhqkS/eK+vIAM='

# Парсинг позиции карточки по поисковому запросу
class WbRank:
    SEARCH_URL = 'https://www.wildberries.ru/__internal/u-search/exactmatch/ru/common/v18/search'
    DEFAULT_PARAMS = {
        'ab_testing': 'false',
        'appType': '1',
        'curr': 'rub',
        'dest': '12354108',
        'hide_vflags': '4294967296',
        'lang': 'ru',
        'locale': 'ru',
        'page': '2',
        'query': 'кроссовки мужские',
        'resultset': 'catalog',
        'sort': 'popular',
        'spp': '30',
        'suppressSpellcheck': 'false',
    }

    def __init__(self, goods: list):
        self.goods = goods
        self.token = TOKEN

    def _update_token(self):
        logger.info("Обновляем токен")
        self.token = get_token()
        logger.info("Обновили токен")

    def get_fetch(self, query: str, retries: int = 2):
        params = self.DEFAULT_PARAMS.copy()
        params["query"] = query

        for attempt in range(1, retries + 2):
            logger.info(f"Попытка {attempt}")
            response = requests.get(
                self.SEARCH_URL,
                params=params,
                cookies={"x_wbaas_token": self.token},
                headers=HEADERS
            )
            if response.status_code == 498:
                logger.warning("498 code")
                self._update_token()
                continue
            if response.status_code != 200:
                logger.warning(response.status_code)
                return None
            
            try:
                return response.json()
            except Exception:
                logger.exception("JSON error")
                return None
            
        logger.warning("Все попытки неуспешны")
        return None

    def get_rank_position(self, data: json, sku: str | int) -> int | None:
        products = data.get("products")
        if not products:
            logger.warning("Нет products")
            return None
        
        try:
            sku = int(sku)
        except ValueError:
            return None

        for index, product in enumerate(products, start=1):
            if product.get("id") == sku:
                return index
            
        return None


    def parse_rank(self) -> list:
        results = []

        for good in self.goods:
            sku = good.get("sku")
            query = good.get("query")

            if not sku or not query:
                logger.warning("Нет артикула или запроса")
                continue
            
            data = self.get_fetch(query=query)
            if not data:
                results.append(
                    {"sku": sku, "query": query, "rank": None}
                )
                continue

            results.append(
                {"sku": sku, "query": query, "rank": self.get_rank_position(data=data, sku=sku)}
            )

        return results


cookies = {
    'x_wbaas_token': '1.1000.fce283080ef34ad49b48f48373413543.MTV8NS4xNDkuMjIxLjE4M3xNb3ppbGxhLzUuMCAoV2luZG93cyBOVCAxMC4wOyBXaW42NDsgeDY0KSBBcHBsZVdlYktpdC81MzcuMzYgKEtIVE1MLCBsaWtlIEdlY2tvKSBDaHJvbWUvMTQ3LjAuMC4wIFNhZmFyaS81MzcuMzZ8MTc3ODY4Njg4MHxyZXVzYWJsZXwyfGV5Sm9ZWE5vSWpvaUluMD18MHwzfDE3NzgwODIwODB8MQ==.MEUCIGSN8ZMQflBDQonWU0uL9gg3CYiwKLaxEdO8BREwHK0KAiEA+kM6Eiz50S8IQFSAQrjw8jprILmwYARhqkS/eK+vIAM=',
    '_wbauid': '9646763631777477282',
}

