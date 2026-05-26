from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any


PARSER_DIR = Path(__file__).resolve().parents[1]
TOOLS_DIR = PARSER_DIR / "tools"
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import analyze_logistics_artifacts as analyzer  # noqa: E402


class LogisticsArtifactAnalyzerTests(unittest.TestCase):
    def test_summarizes_quantities_stock_consistency_and_price_values(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            run_a = self._write_run(
                temp_dir,
                "run_a",
                dest="12354108",
                snapshots=[
                    self._snapshot("run_a", "1001", "12354108", total=50, wh=301983, time2=117),
                    self._snapshot("run_a", "1002", "12354108", total=12, wh=130744, time2=102),
                ],
                warehouses=[
                    self._warehouse("run_a", "1001", "12354108", "301983", qty=50, time2=117, logistics=0),
                    self._warehouse("run_a", "1002", "12354108", "130744", qty=10, time2=102, logistics=5),
                ],
            )

            report = analyzer.analyze_runs([run_a])

        self.assertEqual(report["quantity_summary"]["min_quantity"], 12)
        self.assertEqual(report["quantity_summary"]["max_quantity"], 50)
        self.assertEqual(report["quantity_summary"]["rows_equal_50"], 1)
        self.assertEqual(report["stock_consistency"]["matching_sum_count"], 1)
        self.assertEqual(report["stock_consistency"]["mismatching_sum_count"], 1)
        self.assertEqual(report["warehouse_summary"]["price_logistics_unique"], [0, 5])

    def test_compares_destinations_only_for_overlapping_products_and_reports_non_overlap(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            run_default = self._write_run(
                temp_dir,
                "run_default",
                dest="12354108",
                snapshots=[
                    self._snapshot("run_default", "1001", "12354108", total=50, wh=301983, time2=117),
                    self._snapshot("run_default", "1002", "12354108", total=12, wh=130744, time2=102),
                ],
                warehouses=[],
            )
            run_alt = self._write_run(
                temp_dir,
                "run_alt",
                dest="-1257786",
                snapshots=[
                    self._snapshot("run_alt", "1001", "-1257786", total=50, wh=301983, time2=95),
                    self._snapshot("run_alt", "1003", "-1257786", total=7, wh=222, time2=99),
                ],
                warehouses=[],
            )

            report = analyzer.analyze_runs([run_default, run_alt])

        destination = report["destination_impact"]
        self.assertEqual(destination["overlap_product_count"], 1)
        self.assertEqual(destination["changed_counts_by_field"]["product_time2_raw"], 1)
        self.assertEqual(destination["non_overlapping_products"]["12354108"], ["1002"])
        self.assertEqual(destination["non_overlapping_products"]["-1257786"], ["1003"])

    def test_repeat_stability_uses_same_product_and_destination(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            run_1 = self._write_run(
                temp_dir,
                "run_1",
                dest="12354108",
                snapshots=[self._snapshot("run_1", "1001", "12354108", total=50, wh=301983, time2=117)],
                warehouses=[self._warehouse("run_1", "1001", "12354108", "301983", qty=50, time2=117, logistics=0)],
            )
            run_2 = self._write_run(
                temp_dir,
                "run_2",
                dest="12354108",
                snapshots=[self._snapshot("run_2", "1001", "12354108", total=49, wh=301983, time2=117)],
                warehouses=[self._warehouse("run_2", "1001", "12354108", "301983", qty=49, time2=117, logistics=0)],
            )

            report = analyzer.analyze_runs([run_1, run_2])

        stability = report["repeat_stability"]
        self.assertEqual(stability["repeated_product_dest_groups"], 1)
        self.assertEqual(stability["changed_product_groups"], 1)
        self.assertEqual(stability["changed_product_fields"]["total_quantity_observed"], 1)
        self.assertEqual(stability["repeated_warehouse_groups"], 1)
        self.assertEqual(stability["changed_warehouse_fields"]["quantity_observed"], 1)

    def test_partial_and_failed_runs_are_included_in_markdown(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            partial = self._write_run(
                temp_dir,
                "run_partial",
                dest="12354108",
                status="partial",
                products_failed=1,
                snapshots=[self._snapshot("run_partial", "1001", "12354108", total=50, wh=301983, time2=117)],
                warehouses=[],
                errors=[{"error_type": "request_failed", "wb_product_id": "1002"}],
            )
            failed = self._write_run(
                temp_dir,
                "run_failed",
                dest="-1257786",
                status="failed",
                products_failed=2,
                snapshots=[],
                warehouses=[],
                errors=[{"error_type": "request_failed", "wb_product_id": "1001"}],
            )

            report = analyzer.analyze_runs([partial, failed])
            markdown = analyzer.render_markdown(report)

        self.assertEqual(report["errors_summary"]["runs_not_succeeded"], 2)
        self.assertEqual(report["errors_summary"]["runs_without_snapshots"], 1)
        self.assertIn("run_partial", markdown)
        self.assertIn("run_failed", markdown)
        self.assertIn("request_failed", markdown)
        self.assertNotIn("https://card.wb.ru/cards/v4/detail?", markdown)

    def _write_run(
        self,
        temp_dir: Path,
        name: str,
        *,
        dest: str,
        snapshots: list[dict[str, Any]],
        warehouses: list[dict[str, Any]],
        errors: list[dict[str, Any]] | None = None,
        status: str = "succeeded",
        products_failed: int = 0,
    ) -> Path:
        run_dir = temp_dir / name
        run_dir.mkdir()
        (run_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "parser_run_id": name,
                    "status": status,
                    "source_region_dest": dest,
                    "counters": {
                        "products_succeeded": len(snapshots),
                        "products_failed": products_failed,
                    },
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        self._write_jsonl(run_dir / "logistics_snapshots.jsonl", snapshots)
        self._write_jsonl(run_dir / "warehouse_availability.jsonl", warehouses)
        self._write_jsonl(run_dir / "errors.jsonl", errors or [])
        return run_dir

    @staticmethod
    def _write_jsonl(path: Path, rows: list[dict[str, Any]]) -> None:
        with path.open("w", encoding="utf-8") as file:
            for row in rows:
                file.write(json.dumps(row, ensure_ascii=False) + "\n")

    @staticmethod
    def _snapshot(
        run_id: str,
        product_id: str,
        dest: str,
        *,
        total: int,
        wh: int,
        time2: int,
    ) -> dict[str, Any]:
        return {
            "parser_run_id": run_id,
            "wb_product_id": product_id,
            "wb_root_id": f"r-{product_id}",
            "source_region_dest": dest,
            "total_quantity_observed": total,
            "product_wh_raw": wh,
            "product_time1_raw": 2,
            "product_time2_raw": time2,
            "product_dtype_raw": 6597069766664,
            "product_dist_raw": 100,
        }

    @staticmethod
    def _warehouse(
        run_id: str,
        product_id: str,
        dest: str,
        warehouse_id: str,
        *,
        qty: int,
        time2: int,
        logistics: int,
    ) -> dict[str, Any]:
        return {
            "parser_run_id": run_id,
            "wb_product_id": product_id,
            "source_region_dest": dest,
            "option_id": 1,
            "size_orig_name": "0",
            "warehouse_id_on_mp": warehouse_id,
            "quantity_observed": qty,
            "stock_time1_raw": 2,
            "stock_time2_raw": time2,
            "stock_dtype_raw": 6597069766664,
            "stock_dist_raw": 100,
            "price_logistics_raw": logistics,
            "price_return_raw": 0,
        }


if __name__ == "__main__":
    unittest.main()
