import requests
from loguru import logger
from get_token import get_token
import json
from dto import DataPage
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


# Парсинг на основе поисковых запросов
class WbSearchParser:
    def __init__(self, search_phrase: str, cookies: dict = None):
        self.search_phrase = search_phrase
        self.cookies = cookies

        self.default_step = 500 * 100
        self.max_count_of_good = 5000

        self.min_step = 10 * 100
        self.max_step = 5000 * 100

        self.max_split_depth = 10
        self.low_goods_threshold = 500


    def fetch_data(self, add_params: dict = None):
        params = {
            'ab_testing': 'false',
            'appType': '1',
            'autoselectFilters': 'false',
            'curr': 'rub',
            'dest': '12354108',
            'inheritFilters': 'false',
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

        response = requests.get("https://www.wildberries.ru/__internal/u-search/exactmatch/ru/common/v18/search",
                                params=params,
                                cookies=self.cookies,
                                headers=HEADERS)
        
        if response.status_code == 200:
            return response.json()
        
        logger.error(f"WB status: {response.status_code}")
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
            return
        
        total = self._get_total(data=data)
        min_price, max_price = self._get_min_max_price(data=data)

        if not all([total, min_price, max_price]):
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

        left = self.split_price_range(min_price=min_price, max_price=mid, depth=depth+1)
        right = self.split_price_range(min_price=mid+1, max_price=max_price, depth=depth+1)

        return left + right


    def parse(self):
        logger.info(f"Начало парсинга <{self.search_phrase}>")

        base_data = self.get_price_range(data=self.fetch_data())
        if not base_data:
            logger.error("Не удалось получить данные")
            return
        
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


cookies = {
    'x_wbaas_token': '1.1000.fce283080ef34ad49b48f48373413543.MTV8NS4xNDkuMjIxLjE4M3xNb3ppbGxhLzUuMCAoV2luZG93cyBOVCAxMC4wOyBXaW42NDsgeDY0KSBBcHBsZVdlYktpdC81MzcuMzYgKEtIVE1MLCBsaWtlIEdlY2tvKSBDaHJvbWUvMTQ3LjAuMC4wIFNhZmFyaS81MzcuMzZ8MTc3ODY4Njg4MHxyZXVzYWJsZXwyfGV5Sm9ZWE5vSWpvaUluMD18MHwzfDE3NzgwODIwODB8MQ==.MEUCIGSN8ZMQflBDQonWU0uL9gg3CYiwKLaxEdO8BREwHK0KAiEA+kM6Eiz50S8IQFSAQrjw8jprILmwYARhqkS/eK+vIAM=',
    '_wbauid': '9646763631777477282',
}

if __name__ == "__main__":
    # input_list = [
    #     {"sku": "238702174", "query": "кроссовки мужские"},
    #     {"sku": "210793137", "query": "кроссовки мужские"},
    #     {"sku": "850370777", "query": "кроссовки мужские"},
    # ]
    # results = WbRank(goods=input_list).parse_rank()
    # logger.success(results)

    WbSearchParser(search_phrase="кроссовки мужские", cookies=cookies).parse()