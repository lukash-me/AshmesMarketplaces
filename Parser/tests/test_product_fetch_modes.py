from __future__ import annotations

import json
import os
import sys
import unittest
import asyncio
from pathlib import Path
from types import SimpleNamespace
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

    def test_three_parallel_proxy_scoped_runs_are_allowed(self) -> None:
        with patch.dict(os.environ, {"PARSER_MAX_CONCURRENT": "3"}):
            config = ParserConfig.load(PARSER_DIR / "presets" / "home_goods_demo.env")

        self.assertEqual(config.max_concurrent, 3)

    def test_catalog_request_group_size_is_separate_from_complete_card_batch_size(self) -> None:
        with patch.dict(
            os.environ,
            {
                "PARSER_BATCH_SIZE": "100",
                "PARSER_CATALOG_REQUEST_GROUP_SIZE": "10",
            },
        ):
            config = ParserConfig.load(PARSER_DIR / "presets" / "home_goods_demo.env")

        self.assertEqual(config.batch_size, 100)
        self.assertEqual(config.catalog_request_group_size, 10)

    def test_more_than_three_parallel_product_runs_are_rejected(self) -> None:
        with patch.dict(os.environ, {"PARSER_MAX_CONCURRENT": "4"}):
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

    def test_catalog_fetcher_does_not_inherit_post_request_delay_from_proxy_limiter(self) -> None:
        with patch.dict(
            os.environ,
            {
                "PARSER_PROXY_MIN_REQUEST_GAP_SECONDS": "20",
                "PARSER_PROXY_REQUEST_JITTER_SECONDS": "8",
            },
            clear=False,
        ):
            fetcher = WbCatalogFetcher(
                pages=[],
                search_phrase="органайзер",
                cookies={},
            )

        self.assertEqual(fetcher.post_request_gap_seconds, 0.0)
        self.assertEqual(fetcher.post_request_jitter_seconds, 0.0)

    def test_catalog_fetcher_does_not_invalidate_session_on_rate_limit(self) -> None:
        invalidated: list[tuple[str, str | None]] = []

        class FakeClient:
            async def get(self, *args, **kwargs):
                return SimpleNamespace(status_code=429)

        class NoopLimiter:
            async def wait(self, proxy_key: str) -> None:
                return None

        fetcher = WbCatalogFetcher(
            pages=[],
            search_phrase="органайзер",
            cookies={},
            proxy_key="proxy-1",
            rate_limiter=NoopLimiter(),
            max_retries=0,
            max_total_retryable_attempts=1,
            catalog_rate_limit_cooldown_seconds=0.0,
        )

        with patch(
            "WbCatalogFetcher.invalidate_proxy_session",
            lambda proxy_key, *, reason=None: invalidated.append((proxy_key, reason)),
        ):
            result = asyncio.run(fetcher._fetch_one(FakeClient(), {"page": 1, "min_price": 100, "max_price": 200}))

        self.assertIsNone(result)
        self.assertEqual(invalidated, [])
        self.assertTrue(fetcher.stop_requested)

    def test_catalog_fetcher_invalidates_session_on_auth_or_antibot_status(self) -> None:
        invalidated: list[tuple[str, str | None]] = []

        class FakeClient:
            async def get(self, *args, **kwargs):
                return SimpleNamespace(status_code=403)

        class NoopLimiter:
            async def wait(self, proxy_key: str) -> None:
                return None

        fetcher = WbCatalogFetcher(
            pages=[],
            search_phrase="органайзер",
            cookies={},
            proxy_key="proxy-1",
            rate_limiter=NoopLimiter(),
            max_retries=0,
        )

        with patch(
            "WbCatalogFetcher.invalidate_proxy_session",
            lambda proxy_key, *, reason=None: invalidated.append((proxy_key, reason)),
        ):
            result = asyncio.run(fetcher._fetch_one(FakeClient(), {"page": 1, "min_price": 100, "max_price": 200}))

        self.assertIsNone(result)
        self.assertEqual(invalidated, [("proxy-1", "WB catalog HTTP status 403")])

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
