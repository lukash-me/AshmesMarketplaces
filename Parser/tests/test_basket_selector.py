from __future__ import annotations

import sys
import unittest
from pathlib import Path
from types import SimpleNamespace


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

import basket_selector  # noqa: E402
from images_parser import add_images  # noqa: E402
from product_details_contracts import build_card_info_url  # noqa: E402


class BasketSelectorTests(unittest.TestCase):
    def test_fallback_routes_current_wb_volume_to_basket_43(self) -> None:
        self.assertEqual(
            basket_selector.calc_numb_basket(11429, use_upstream=False),
            "43",
        )

    def test_fallback_keeps_recent_basket_boundaries(self) -> None:
        cases = {
            9606: "41",
            10374: "42",
            11142: "43",
            11910: "44",
            12678: "45",
            13446: "46",
        }

        for volume, expected_basket in cases.items():
            with self.subTest(volume=volume):
                self.assertEqual(
                    basket_selector.calc_numb_basket(volume, use_upstream=False),
                    expected_basket,
                )

    def test_extracts_basket_ranges_from_wb_upstream_payload(self) -> None:
        payload = {
            "routes": [
                {
                    "vol_range_from": 11142,
                    "vol_range_to": 11909,
                    "host": "basket-43.wbcontent.net",
                },
                {
                    "vol_range_from": 11910,
                    "vol_range_to": 12677,
                    "host": "basket-44.wbcontent.net",
                },
                {
                    "vol_range_from": 1,
                    "vol_range_to": 10,
                    "host": "static-basket-01.wb.ru",
                },
            ]
        }

        ranges = basket_selector.extract_basket_ranges(payload)

        self.assertIn((11142, 11909, "43"), ranges)
        self.assertIn((11910, 12677, "44"), ranges)
        self.assertNotIn((1, 10, "01"), ranges)

    def test_card_info_url_uses_current_basket_for_product_1142965384(self) -> None:
        self.assertIn(
            "basket-43.wbbasket.ru/vol11429/part1142965/1142965384",
            build_card_info_url("1142965384"),
        )

    def test_image_url_uses_current_basket_for_product_1142965384(self) -> None:
        product = SimpleNamespace(id=1142965384, pics=1, image_links=None)

        add_images([product])

        self.assertIn(
            "basket-43.wbbasket.ru/vol11429/part1142965/1142965384/images/big/1.webp",
            product.image_links,
        )


if __name__ == "__main__":
    unittest.main()
