from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from config import ParserConfig
from pipelines.products import runner


class ExplicitNichesTests(unittest.TestCase):
    def test_selected_subcategories_resolves_wb_hierarchy_from_catalog(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "niches.json"
            path.write_text(
                json.dumps(
                    {
                        "niches": [
                            {
                                "wbCategoryId": 8137,
                                "sourceCategory": "Женщинам",
                                "sourceSubcategory": "Женские платья и сарафаны",
                                "sourcePath": "Женщинам / Женские платья и сарафаны",
                                "searchQuery": "menu_v3_8137 платье женские",
                            },
                            {
                                "wbCategoryId": 8194,
                                "sourceCategory": "Обувь",
                                "sourceSubcategory": "Мужские кеды и кроссовки",
                                "sourcePath": "Обувь / Мужская обувь / Мужские кеды и кроссовки",
                                "searchQuery": "menu_redirect_subject_v2_8194 мужские кеды и кроссовки",
                            },
                        ]
                    },
                    ensure_ascii=False,
                ),
                encoding="utf-8",
            )

            wb_menu = [
                {
                    "id": 8137,
                    "name": "Платья и сарафаны",
                    "searchQuery": "menu_v3_8137 платье женские",
                    "sourceCategory": "Женщинам",
                    "sourceSubcategory": "Платья и сарафаны",
                    "sourcePath": "Женщинам / Платья и сарафаны",
                },
                {
                    "id": 8194,
                    "name": "Кеды и кроссовки",
                    "searchQuery": "menu_redirect_subject_v2_8194 мужские кеды и кроссовки",
                    "sourceCategory": "Обувь",
                    "sourceSubcategory": "Кеды и кроссовки",
                    "sourcePath": "Обувь / Мужская / Кеды и кроссовки",
                },
            ]
            with patch.dict("os.environ", {"PARSER_EXPLICIT_NICHES_FILE": str(path)}), patch.object(
                runner.CategoriesParser,
                "parse",
                return_value=wb_menu,
            ):
                selected = runner._selected_subcategories(ParserConfig())

        self.assertEqual([item["sourceCategory"] for item in selected], ["Женщинам", "Обувь"])
        self.assertEqual([item["name"] for item in selected], ["Женские платья и сарафаны", "Мужские кеды и кроссовки"])
        self.assertEqual(
            [item["sourcePath"] for item in selected],
            ["Женщинам / Платья и сарафаны", "Обувь / Мужская / Кеды и кроссовки"],
        )
        self.assertEqual(selected[0]["searchQuery"], "menu_v3_8137 платье женские")

    def test_selected_subcategories_rejects_explicit_niche_absent_from_wb_menu(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "niches.json"
            path.write_text(
                json.dumps(
                    {
                        "niches": [
                            {
                                "wbCategoryId": 999,
                                "sourceCategory": "Fake",
                                "sourceSubcategory": "Fake",
                                "searchQuery": "menu_v3_999 fake",
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )

            with patch.dict("os.environ", {"PARSER_EXPLICIT_NICHES_FILE": str(path)}), patch.object(
                runner.CategoriesParser,
                "parse",
                return_value=[],
            ):
                with self.assertRaisesRegex(ValueError, "not found"):
                    runner._selected_subcategories(ParserConfig())


if __name__ == "__main__":
    unittest.main()
