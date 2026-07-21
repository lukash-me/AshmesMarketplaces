from __future__ import annotations

import sys
import unittest
from pathlib import Path


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from CategoriesParser import CategoriesParser  # noqa: E402


class CategoryParserTests(unittest.TestCase):
    def test_parent_category_can_match_wb_seo_label(self) -> None:
        parser = CategoriesParser()
        parser.fetch = lambda categories=None: [  # type: ignore[method-assign]
            {
                "name": "Дом",
                "seo": "Товары для дома",
                "childs": [
                    {
                        "name": "Хранение вещей",
                        "childs": [
                            {
                                "id": 62514,
                                "name": "Органайзеры",
                                "seo": "Органайзеры для хранения вещей",
                                "searchQuery": "menu_redirect_subject_v2_62514 органайзеры для хранения",
                            }
                        ],
                    },
                    {
                        "name": "Ванная",
                        "childs": [
                            {
                                "id": 261,
                                "name": "Коврики",
                                "seo": "Коврики для ванной",
                                "searchQuery": "menu_v3_261 коврики в ванную",
                            }
                        ],
                    },
                    {
                        "name": "Освещение",
                        "childs": [
                            {
                                "id": 130194,
                                "name": "Бра",
                                "seo": "Светильники бра",
                                "searchQuery": "menu_v3_130194 бра",
                            }
                        ],
                    },
                ],
            }
        ]

        selected = parser.parse(["Товары для дома"])

        self.assertEqual(
            [category["name"] for category in selected],
            [
                "Органайзеры для хранения вещей",
                "Коврики для ванной",
                "Светильники бра",
            ],
        )

    def test_parent_category_name_matching_remains_available(self) -> None:
        parser = CategoriesParser()
        parser.fetch = lambda categories=None: [  # type: ignore[method-assign]
            {
                "name": "Обувь",
                "childs": [
                    {
                        "id": 631,
                        "name": "Обувь для девочек",
                        "seo": "Обувь для девочек",
                        "searchQuery": "menu_redirect_subject_v2_631 обувь для девочек",
                    }
                ],
            }
        ]

        selected = parser.parse(["Обувь"])

        self.assertEqual([category["name"] for category in selected], ["Обувь для девочек"])


if __name__ == "__main__":
    unittest.main()
