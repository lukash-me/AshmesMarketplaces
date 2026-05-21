from __future__ import annotations

import gzip
import json
import sys
import tempfile
import unittest
from pathlib import Path


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from review_contracts import SelectedProduct, map_feedback_payload  # noqa: E402
from review_exporters import ReviewCanonicalExporter, write_raw_root_payload  # noqa: E402


class ReviewExporterTests(unittest.TestCase):
    def test_dedupes_reviews_and_fallback_replies(self) -> None:
        payload = json.loads(
            (PARSER_DIR / "tests" / "fixtures" / "root_feedbacks_sample.json").read_text(
                encoding="utf-8"
            )
        )
        mapped = map_feedback_payload(
            payload=payload,
            selected_products={"1001": SelectedProduct("1001", "root-10")},
            parser_run_id="wb_reviews_test",
            parsed_at_utc="2026-05-21T12:00:00Z",
            marketplace="wildberries",
            input_products_parser_run_id="wb_products_test",
            input_products_jsonl="products.jsonl",
            source_wb_root_id="root-10",
        )

        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            exporter = ReviewCanonicalExporter(
                reviews_path=temp_path / "reviews.jsonl",
                replies_path=temp_path / "review_replies.jsonl",
            )

            first = exporter.write_rows(
                review_rows=mapped.review_rows,
                reply_rows=mapped.reply_rows,
            )
            second = exporter.write_rows(
                review_rows=mapped.review_rows,
                reply_rows=mapped.reply_rows,
            )

            self.assertEqual(first.reviews_written, 1)
            self.assertEqual(first.replies_written, 1)
            self.assertEqual(second.review_duplicates, 1)
            self.assertEqual(second.reply_duplicates, 1)

    def test_retains_compressed_root_payload_without_overwrite(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "raw" / "root_feedbacks" / "root-10.json.gz"
            write_raw_root_payload(path, {"feedbacks": [{"id": "first"}]})
            write_raw_root_payload(path, {"feedbacks": [{"id": "second"}]})

            with gzip.open(path, "rt", encoding="utf-8") as file:
                payload = json.load(file)

            self.assertEqual(payload["feedbacks"][0]["id"], "first")


if __name__ == "__main__":
    unittest.main()
