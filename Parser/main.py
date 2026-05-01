import asyncio
from loguru import logger

from WbCatalogFetcher import WbCatalogFetcher
from SearchPhraseParser import SearchPhraseParser
from get_token import get_token
from models import Items
from images_parser import add_images
from add_price_wb_wallet import add_price_with_wb_wallet
from saver import SaveWbData


def parse(search_phrase):

    wb_token = get_token()

    cookies = {
        'x_wbaas_token': wb_token,
    }

    # Собрать промежутки
    price_ranges = SearchPhraseParser(search_phrase=search_phrase, cookies=cookies).parse()

    # Парсинг товаров
    fetcher = WbCatalogFetcher(search_phrase=search_phrase, pages=price_ranges, cookies=cookies)

    results = asyncio.run(fetcher.fetch_all())

    logger.info("Сырые результаты получены")

    product_models = []
    for raw_data in results:
        items_info = Items.model_validate(raw_data)
        if items_info.products:
            product_models.extend(items_info.products)

    logger.info("Добавляем картинки")
    product_models = add_images(product_models)

    logger.info("Добавляем цену с WB-кошельком")
    product_models = add_price_with_wb_wallet(product_models)

    logger.info("Данные добавлены, перехожу к сохранению")
    SaveWbData().wb_save(products=product_models)

if __name__ == "__main__":
    parse(search_phrase="кеды женские натуральная кожа белые")