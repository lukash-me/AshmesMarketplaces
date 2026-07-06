from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from pipelines.reviews import runner as reviews_runner  # noqa: E402
from pipelines.ranks import runner as ranks_runner  # noqa: E402


class ParserContractFileTests(unittest.TestCase):
    def test_reviews_runner_creates_empty_required_contract_files(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir = Path(temp)
            output_paths = reviews_runner._output_paths(run_dir)

            reviews_runner._ensure_contract_files(output_paths)

            for key in (
                "reviews_jsonl",
                "review_replies_jsonl",
                "review_fetch_results_jsonl",
                "review_coverage_jsonl",
                "errors_jsonl",
                "runner_log",
            ):
                self.assertTrue(output_paths[key].exists(), key)
                self.assertEqual(output_paths[key].read_text(encoding="utf-8"), "")

    def test_rank_runner_creates_empty_required_contract_files(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            run_dir = Path(temp)

            ranks_runner._ensure_contract_files(run_dir)

            for file_name in (
                "product_rank_snapshots.jsonl",
                "rank_page_fetches.jsonl",
                "errors.jsonl",
                "runner.log",
            ):
                path = run_dir / file_name
                self.assertTrue(path.exists(), file_name)
                self.assertEqual(path.read_text(encoding="utf-8"), "")


if __name__ == "__main__":
    unittest.main()
