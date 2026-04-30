import asyncio
from loguru import logger

from WbCatalogFetcher import WbCatalogFetcher
from SearchPhraseParser import SearchPhraseParser


def parse(search_phrase):
    cookies = {
        'x_wbaas_token': '1.1000.fce283080ef34ad49b48f48373413543.MTV8NS4xNDkuMjIxLjE4M3xNb3ppbGxhLzUuMCAoV2luZG93cyBOVCAxMC4wOyBXaW42NDsgeDY0KSBBcHBsZVdlYktpdC81MzcuMzYgKEtIVE1MLCBsaWtlIEdlY2tvKSBDaHJvbWUvMTQ3LjAuMC4wIFNhZmFyaS81MzcuMzZ8MTc3ODY4Njg4MHxyZXVzYWJsZXwyfGV5Sm9ZWE5vSWpvaUluMD18MHwzfDE3NzgwODIwODB8MQ==.MEUCIGSN8ZMQflBDQonWU0uL9gg3CYiwKLaxEdO8BREwHK0KAiEA+kM6Eiz50S8IQFSAQrjw8jprILmwYARhqkS/eK+vIAM=',
        '_wbauid': '9646763631777477282',
    }
    price_ranges = SearchPhraseParser(search_phrase=search_phrase, cookies=cookies).parse()
    fetcher = WbCatalogFetcher(search_phrase=search_phrase, pages=price_ranges, cookies=cookies)

    results = asyncio.run(fetcher.fetch_all())

    logger.info("Результаты получены")

    all_sku = []
    for sku_data in results:
        all_sku.extend([sku.get("id") for sku in sku_data.get("products", [])])

    logger.info(len(all_sku))

    all_sku = set(all_sku)
    logger.success(len(all_sku))

if __name__ == "__main__":
    parse(search_phrase="geforce")