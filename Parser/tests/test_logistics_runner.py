from __future__ import annotations

import argparse
import json
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

import logistics_runner  # noqa: E402
from logistics_exporters import iter_jsonl  # noqa: E402
from wb_logistics_client import LogisticsFetchResult  # noqa: E402


class FakeLogisticsClient:
    def __init__(self, payloads: dict[str, dict[str, Any] | None]) -> None:
        self.payloads = payloads
        self.calls: list[tuple[str, str]] = []

    def fetch_product(self, *, wb_product_id: str, dest: str) -> LogisticsFetchResult:
        self.calls.append((wb_product_id, dest))
        payload = self.payloads.get(wb_product_id)
        if payload is None:
            return LogisticsFetchResult(
                wb_product_id=wb_product_id,
                endpoint="https://card.wb.ru/cards/v4/detail",
                params={"nm": wb_product_id, "dest": dest},
                request_fingerprint=f"fp-{wb_product_id}",
                status="failed",
                payload=None,
                attempts=2,
                retries=1,
                http_status=500,
                message="failed",
                is_transient=True,
            )

        return LogisticsFetchResult(
            wb_product_id=wb_product_id,
            endpoint="https://card.wb.ru/cards/v4/detail",
            params={"nm": wb_product_id, "dest": dest},
            request_fingerprint=f"fp-{wb_product_id}",
            status="succeeded",
            payload=payload,
            attempts=1,
            retries=0,
            http_status=200,
        )


class LogisticsRunnerTests(unittest.TestCase):
    def _products_jsonl(self, temp_dir: Path) -> Path:
        products_dir = temp_dir / "products_run"
        products_dir.mkdir()
        (products_dir / "manifest.json").write_text(
            json.dumps({"parser_run_id": "wb_products_test"}),
            encoding="utf-8",
        )
        rows = [
            {
                "wb_product_id": "1001",
                "wb_root_id": "5001",
                "seller_id": 42,
                "seller_name": "Seller",
                "source_category": "Home",
                "source_subcategory": "Storage",
                "source_query": "Storage",
                "source_region_dest": "123",
            },
            {
                "wb_product_id": "1002",
                "wb_root_id": "5002",
                "seller_id": 43,
                "seller_name": "Other",
                "source_category": "Home",
                "source_subcategory": "Storage",
                "source_query": "Storage",
                "source_region_dest": "123",
            },
        ]
        with (products_dir / "products.jsonl").open("w", encoding="utf-8") as file:
            for row in rows:
                file.write(json.dumps(row, ensure_ascii=False) + "\n")
        return products_dir / "products.jsonl"

    def _args(self, *, temp_dir: Path, products_jsonl: Path, **overrides: Any) -> argparse.Namespace:
        values = {
            "products_run_dir": None,
            "products_jsonl": products_jsonl,
            "dest": "123",
            "limit": None,
            "output_dir": temp_dir / "out",
            "delay_ms": 0,
            "timeout_sec": 1,
            "retries": 0,
            "dry_run": False,
            "resume": False,
            "marketplace": "wb",
            "product_id": None,
        }
        values.update(overrides)
        return argparse.Namespace(**values)

    def _payload(self, wb_product_id: str = "1001") -> dict[str, Any]:
        return {
            "products": [
                {
                    "id": int(wb_product_id),
                    "root": 5001,
                    "supplierId": 42,
                    "supplier": "Seller",
                    "totalQuantity": 50,
                    "wh": 301983,
                    "time1": 2,
                    "time2": 117,
                    "dtype": 6597069766664,
                    "dist": 1089,
                    "sizes": [
                        {
                            "name": "",
                            "origName": "0",
                            "rank": 0,
                            "optionId": 1171438114,
                            "stocks": [
                                {
                                    "wh": 301983,
                                    "dtype": 6597069766664,
                                    "dist": 1089,
                                    "qty": 50,
                                    "priority": 50486,
                                    "time1": 2,
                                    "time2": 117,
                                }
                            ],
                            "price": {
                                "basic": 128600,
                                "product": 43700,
                                "logistics": 0,
                                "return": 0,
                            },
                        }
                    ],
                }
            ]
        }

    def test_manifest_and_rows_are_written_from_card_detail_payload(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            products_jsonl = self._products_jsonl(temp_dir)
            fake = FakeLogisticsClient({"1001": self._payload("1001")})

            run_dir = logistics_runner.run_logistics(
                args=self._args(temp_dir=temp_dir, products_jsonl=products_jsonl, limit=1),
                client_factory=lambda: fake,
            )

            manifest = json.loads((run_dir / "manifest.json").read_text(encoding="utf-8"))
            snapshots = list(iter_jsonl(run_dir / "logistics_snapshots.jsonl") or [])
            warehouse_rows = list(iter_jsonl(run_dir / "warehouse_availability.jsonl") or [])

        self.assertEqual(manifest["run_kind"], "wb_logistics")
        self.assertEqual(manifest["status"], "succeeded")
        self.assertEqual(manifest["input_products_run_id"], "wb_products_test")
        self.assertEqual(snapshots[0]["source_region_dest"], "123")
        self.assertEqual(snapshots[0]["wb_product_id"], "1001")
        self.assertEqual(snapshots[0]["wb_root_id"], "5001")
        self.assertEqual(snapshots[0]["seller_id"], "42")
        self.assertEqual(snapshots[0]["seller_name"], "Seller")
        self.assertEqual(snapshots[0]["total_quantity_observed"], 50)
        self.assertIsNone(snapshots[0]["quantity_is_capped"])
        self.assertIsNone(snapshots[0]["quantity_cap_observed"])
        self.assertEqual(snapshots[0]["quantity_semantics"], "unknown_or_capped")
        self.assertEqual(snapshots[0]["product_wh_raw"], 301983)
        self.assertEqual(snapshots[0]["product_time1_raw"], 2)
        self.assertEqual(snapshots[0]["product_time2_raw"], 117)
        self.assertEqual(snapshots[0]["product_dtype_raw"], 6597069766664)
        self.assertEqual(snapshots[0]["product_dist_raw"], 1089)
        self.assertIn("sizes", snapshots[0]["raw_observed_fields"])
        self.assertEqual(warehouse_rows[0]["warehouse_id_on_mp"], "301983")
        self.assertEqual(warehouse_rows[0]["quantity_observed"], 50)
        self.assertIsNone(warehouse_rows[0]["quantity_is_capped"])
        self.assertEqual(warehouse_rows[0]["price_logistics_raw"], 0)
        self.assertEqual(warehouse_rows[0]["raw_stock"]["wh"], 301983)

    def test_failed_product_response_writes_error_row(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            products_jsonl = self._products_jsonl(temp_dir)
            fake = FakeLogisticsClient({"1001": None})

            run_dir = logistics_runner.run_logistics(
                args=self._args(temp_dir=temp_dir, products_jsonl=products_jsonl, limit=1),
                client_factory=lambda: fake,
            )

            manifest = json.loads((run_dir / "manifest.json").read_text(encoding="utf-8"))
            errors = list(iter_jsonl(run_dir / "errors.jsonl") or [])

        self.assertEqual(manifest["status"], "failed")
        self.assertEqual(errors[0]["wb_product_id"], "1001")
        self.assertEqual(errors[0]["error_type"], "request_failed")
        self.assertEqual(errors[0]["http_status"], 500)
        self.assertTrue(errors[0]["is_transient"])

    def test_dry_run_does_not_call_network_or_write_normal_rows(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            products_jsonl = self._products_jsonl(temp_dir)
            fake = FakeLogisticsClient({"1001": self._payload("1001")})

            run_dir = logistics_runner.run_logistics(
                args=self._args(temp_dir=temp_dir, products_jsonl=products_jsonl, limit=1, dry_run=True),
                client_factory=lambda: fake,
            )

            snapshots = list(iter_jsonl(run_dir / "logistics_snapshots.jsonl") or [])
            warehouse_rows = list(iter_jsonl(run_dir / "warehouse_availability.jsonl") or [])

        self.assertEqual(fake.calls, [])
        self.assertEqual(snapshots, [])
        self.assertEqual(warehouse_rows, [])

    def test_resume_skips_already_written_product_ids(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            products_jsonl = self._products_jsonl(temp_dir)
            first_fake = FakeLogisticsClient({"1001": self._payload("1001")})
            run_dir = logistics_runner.run_logistics(
                args=self._args(temp_dir=temp_dir, products_jsonl=products_jsonl, limit=1),
                client_factory=lambda: first_fake,
            )

            second_fake = FakeLogisticsClient({"1001": self._payload("1001")})
            logistics_runner.run_logistics(
                args=self._args(
                    temp_dir=temp_dir,
                    products_jsonl=products_jsonl,
                    limit=1,
                    output_dir=run_dir,
                    resume=True,
                ),
                client_factory=lambda: second_fake,
            )
            snapshots = list(iter_jsonl(run_dir / "logistics_snapshots.jsonl") or [])

        self.assertEqual(second_fake.calls, [])
        self.assertEqual(len(snapshots), 1)


if __name__ == "__main__":
    unittest.main()
