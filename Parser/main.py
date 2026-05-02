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


def parse(categories: List | None):

    wb_token = get_token()

    cookies = {
        'x_wbaas_token': wb_token,
    }

    # Получить категории
    categories = CategoriesParser().parse(categories)

    for category in categories:
        # Собрать промежутки
        price_ranges = SearchPhraseParser(search_phrase=category['searchQuery'], cookies=cookies).parse()

        # Парсинг товаров
        fetcher = WbCatalogFetcher(search_phrase=category['searchQuery'], pages=price_ranges, cookies=cookies)

        results = asyncio.run(fetcher.fetch_all())

        logger.info(f"Сырые результаты для категории {category['name']} получены")

        product_models = []
        for raw_data in results:

            if "products" not in raw_data:
                logger.warning("Ответ без products пропущен")
                logger.debug(raw_data)
                continue
            else:
                logger.debug(f"Нормальные raw_data {raw_data}")

            items_info = Items.model_validate(raw_data)
            if items_info.products:
                product_models.extend(items_info.products)

        logger.info("Добавляем картинки")
        product_models = add_images(product_models)

        logger.info("Добавляем цену с WB-кошельком")
        product_models = add_price_with_wb_wallet(product_models)

        logger.info("Данные добавлены, перехожу к сохранению")
        SaveWbData().wb_save(products=product_models, category_name=category['name'])


if __name__ == "__main__":
    parse(categories=["Обувь"])