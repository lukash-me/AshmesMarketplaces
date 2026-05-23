from __future__ import annotations

import json
import os
import sys
import unittest
from pathlib import Path
from unittest.mock import patch


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from config import ParserConfig  # noqa: E402
from dto import DataPage  # noqa: E402
from WbCatalogFetcher import WbCatalogFetcher  # noqa: E402


class ProductFetchModeTests(unittest.TestCase):
    def test_default_product_fetch_mode_is_price_split(self) -> None:
        with patch.dict(os.environ, {}, clear=True):
            config = ParserConfig.load(PARSER_DIR / "presets" / "home_goods_demo.env")

        self.assertEqual(config.product_fetch_mode, "price_split")

    def test_direct_product_fetch_mode_can_be_set_by_environment(self) -> None:
        with patch.dict(os.environ, {"PARSER_PRODUCT_FETCH_MODE": "direct"}):
            config = ParserConfig.load(PARSER_DIR / "presets" / "home_goods_demo.env")

        self.assertEqual(config.product_fetch_mode, "direct")

    def test_invalid_product_fetch_mode_is_rejected(self) -> None:
        with patch.dict(os.environ, {"PARSER_PRODUCT_FETCH_MODE": "bad"}):
            with self.assertRaises(ValueError):
                ParserConfig.load(PARSER_DIR / "presets" / "home_goods_demo.env")

    def test_direct_catalog_params_do_not_include_price_split(self) -> None:
        fetcher = WbCatalogFetcher(
            pages=[],
            search_phrase="органайзер",
            cookies={},
            price_split_enabled=False,
            max_catalog_pages=1,
        )

        self.assertNotIn("priceU", fetcher._build_params({"page": 1}))
        self.assertEqual(fetcher._build_tasks(), [{"page": 1}])

    def test_price_split_params_keep_existing_price_filter(self) -> None:
        fetcher = WbCatalogFetcher(
            pages=[DataPage(min_price=100, max_price=200, total=100)],
            search_phrase="органайзер",
            cookies={},
            price_split_enabled=True,
            max_catalog_pages=1,
        )

        task = fetcher._build_tasks()[0]
        self.assertEqual(fetcher._build_params(task)["priceU"], "100;200")

    def test_market_refresh_uses_direct_for_smoke_and_bounded_only(self) -> None:
        payload = json.loads(
            (PARSER_DIR / "presets" / "market_refresh_home_goods_demo.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(
            payload["modes"]["smoke"]["product"]["env"]["PARSER_PRODUCT_FETCH_MODE"],
            "direct",
        )
        self.assertEqual(
            payload["modes"]["bounded"]["product"]["env"]["PARSER_PRODUCT_FETCH_MODE"],
            "direct",
        )
        self.assertNotIn("env", payload["modes"]["full"]["product"])


if __name__ == "__main__":
    unittest.main()
