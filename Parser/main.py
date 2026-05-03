import asyncio
from loguru import logger

from CategoriesParser import CategoriesParser
from WbCatalogFetcher import WbCatalogFetcher
from SearchPhraseParser import SearchPhraseParser
from get_token import get_token
from models import Items
from images_parser import add_images
from add_price_wb_wallet import add_price_with_wb_wallet
from saver import SaveWbData
from typing import List
import time


def parse(categories: List | None):

    #Получить категории
    categories = CategoriesParser().parse(categories)
    categories = categories[0:2]

    for category in categories:

        wb_token = get_token()
        cookies = {
            'x_wbaas_token': wb_token,
        }

        # Собрать промежутки
        price_ranges = SearchPhraseParser(search_phrase=category.get("name"), cookies=cookies).parse()

        time.sleep(10)

        # Парсинг товаров
        fetcher = WbCatalogFetcher(search_phrase=category.get("name"), pages=price_ranges, cookies=cookies)

        results = asyncio.run(fetcher.fetch_all())

        logger.info(f"Сырые результаты для категории {category.get("name")} получены")

        product_models = []
        for raw_data in results:

            if "products" not in raw_data:
                logger.warning("Ответ без products пропущен")
                logger.debug(raw_data)
                continue

            items_info = Items.model_validate(raw_data)
            if items_info.products:
                product_models.extend(items_info.products)

        logger.info("Добавляем картинки")
        product_models = add_images(product_models)

        logger.info("Добавляем цену с WB-кошельком")
        product_models = add_price_with_wb_wallet(product_models)

        logger.info("Данные добавлены, перехожу к сохранению")
        SaveWbData().wb_save(products=product_models, category_name=category.get("name"))

        time.sleep(20)

if __name__ == "__main__":
    parse(categories=["Обувь"])