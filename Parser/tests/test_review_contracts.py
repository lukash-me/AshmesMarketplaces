from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from review_contracts import SelectedProduct, map_feedback_payload  # noqa: E402


class ReviewContractTests(unittest.TestCase):
    def test_maps_selected_product_rows_and_root_attribution(self) -> None:
        payload = json.loads(
            (PARSER_DIR / "tests" / "fixtures" / "root_feedbacks_sample.json").read_text(
                encoding="utf-8"
            )
        )
        selected = {
            "1001": SelectedProduct(
                wb_product_id="1001",
                wb_root_id="root-10",
                source_category="Shoes",
                source_subcategory="Kids",
                source_query="Kids shoes",
                source_region_dest="123",
            )
        }

        mapped = map_feedback_payload(
            payload=payload,
            selected_products=selected,
            parser_run_id="wb_reviews_test",
            parsed_at_utc="2026-05-21T12:00:00Z",
            marketplace="wildberries",
            input_products_parser_run_id="wb_products_test",
            input_products_jsonl="products.jsonl",
            source_wb_root_id="root-10",
        )

        self.assertEqual(mapped.payload_feedback_rows_seen, 3)
        self.assertEqual(mapped.selected_review_rows_seen, 2)
        self.assertEqual(len(mapped.review_rows), 1)
        self.assertEqual(len(mapped.reply_rows), 1)
        self.assertEqual(len(mapped.issues), 1)

        review = mapped.review_rows[0]
        self.assertEqual(review["review_id_on_mp"], "review-selected-1")
        self.assertEqual(review["wb_product_id"], "1001")
        self.assertEqual(review["review_attribution_mode"], "root_payload")
        self.assertEqual(review["pros"], "Comfortable")
        self.assertEqual(review["helpful_plus"], 2)
        self.assertIn("unknownFutureField", review["raw_observed_fields"])

        reply = mapped.reply_rows[0]
        self.assertIsNone(reply["reply_id_on_mp"])
        self.assertEqual(reply["review_id_on_mp"], review["review_id_on_mp"])
        self.assertEqual(reply["review_attribution_mode"], "root_payload")
        self.assertEqual(len(reply["reply_fallback_hash"]), 64)

    def test_empty_payload_is_a_valid_empty_mapping(self) -> None:
        mapped = map_feedback_payload(
            payload={"feedbackCount": 0, "feedbacks": None},
            selected_products={},
            parser_run_id="wb_reviews_test",
            parsed_at_utc="2026-05-21T12:00:00Z",
            marketplace="wildberries",
            input_products_parser_run_id=None,
            input_products_jsonl="products.jsonl",
            source_wb_root_id="root-0",
        )

        self.assertEqual(mapped.payload_feedback_count, 0)
        self.assertEqual(mapped.payload_feedback_rows_seen, 0)
        self.assertEqual(mapped.review_rows, [])
        self.assertEqual(mapped.reply_rows, [])
        self.assertEqual(mapped.issues, [])


if __name__ == "__main__":
    unittest.main()
