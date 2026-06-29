from __future__ import annotations

import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from typing import Any


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from app import market_refresh_runner as runner  # noqa: E402


class FakeExecutor:
    def __init__(
        self,
        *,
        output_base_dir: Path,
        failures: set[str] | None = None,
        partials: set[str] | None = None,
        incomplete_steps: set[str] | None = None,
        events: list[str] | None = None,
    ) -> None:
        self.output_base_dir = output_base_dir
        self.failures = failures or set()
        self.partials = partials or set()
        self.incomplete_steps = incomplete_steps or set()
        self.events = events
        self.calls: list[list[str]] = []
        self.envs: list[dict[str, str]] = []
        self.counters = {"rank": 0, "products": 0, "logistics": 0, "reviews": 0, "product_details": 0}

    def __call__(
        self,
        *,
        command: list[str],
        cwd: Path,
        env: dict[str, str],
        timeout_seconds: int | None = None,
        output_callback=None,
    ) -> runner.CommandResult:
        self.calls.append(command)
        self.envs.append(env)
        step = self._step_for(command)
        if self.events is not None:
            self.events.append(f"exec:{step}")
        self.counters[step] += 1
        if step in self.failures:
            return runner.CommandResult(exit_code=2, stderr=f"{step} failed")

        run_dir = self.output_base_dir / "runs" / f"wb_{step}_{self.counters[step]}"
        run_dir.mkdir(parents=True, exist_ok=True)
        status = "partial" if step in self.partials else "succeeded"
        (run_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "parser_run_id": run_dir.name,
                    "run_kind": "wb_search_rank" if step == "rank" else None,
                    "status": status,
                    "row_counts": {"rank_rows_written": 100} if step == "rank" else {"unique_rows": 10},
                    "counters": (
                        {"reviews_written": 1}
                        if step == "reviews"
                        else {"products_requested": 3, "products_succeeded": 3, "snapshot_rows_written": 3}
                        if step == "logistics"
                        else None
                    ),
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        if step == "products":
            product_rows = [
                {"wb_product_id": "1001", "wb_root_id": "5001", "marketplace": "wildberries", "source_subcategory": "Storage"},
                {"wb_product_id": "1002", "wb_root_id": "5002", "marketplace": "wildberries", "source_subcategory": "one"},
                {"wb_product_id": "1003", "wb_root_id": "5003", "marketplace": "wildberries", "source_subcategory": "two"},
            ]
            (run_dir / "products.jsonl").write_text(
                "".join(json.dumps(row, ensure_ascii=False) + "\n" for row in product_rows),
                encoding="utf-8",
            )
        if step in {"logistics", "reviews", "product_details"} and step not in self.incomplete_steps:
            self._write_enrichment_artifacts(step=step, command=command, run_dir=run_dir)

        marker = {
            "rank": "Rank run directory:",
            "products": "Run directory:",
            "logistics": "Logistics run directory:",
            "reviews": "Review run directory:",
            "product_details": "Product details run directory:",
        }[step]
        output = f"{marker} {run_dir}"
        if output_callback:
            output_callback(output)
        return runner.CommandResult(exit_code=0, stdout=output + "\n")

    @staticmethod
    def _products_run_dir(command: list[str]) -> Path | None:
        if "--products-run-dir" not in command:
            return None
        return Path(command[command.index("--products-run-dir") + 1])

    def _input_product_rows(self, command: list[str]) -> list[dict[str, Any]]:
        product_run_dir = self._products_run_dir(command)
        if product_run_dir is None:
            return []
        products_jsonl = product_run_dir / "products.jsonl"
        if not products_jsonl.exists():
            return []
        return [json.loads(line) for line in products_jsonl.read_text(encoding="utf-8").splitlines() if line.strip()]

    def _write_enrichment_artifacts(self, *, step: str, command: list[str], run_dir: Path) -> None:
        rows = self._input_product_rows(command)
        if step == "logistics":
            (run_dir / "logistics_snapshots.jsonl").write_text(
                "".join(
                    json.dumps({"wb_product_id": row["wb_product_id"], "status": "succeeded"}, ensure_ascii=False) + "\n"
                    for row in rows
                ),
                encoding="utf-8",
            )
            (run_dir / "warehouse_availability.jsonl").write_text("", encoding="utf-8")
        elif step == "reviews":
            root_ids = sorted({str(row["wb_root_id"]) for row in rows if row.get("wb_root_id")})
            (run_dir / "review_fetch_results.jsonl").write_text(
                "".join(
                    json.dumps({"source_wb_root_id": root_id, "status": "empty"}, ensure_ascii=False) + "\n"
                    for root_id in root_ids
                ),
                encoding="utf-8",
            )
            (run_dir / "reviews.jsonl").write_text("", encoding="utf-8")
            (run_dir / "review_replies.jsonl").write_text("", encoding="utf-8")
        elif step == "product_details":
            (run_dir / "product_detail_fetch_results.jsonl").write_text(
                "".join(
                    json.dumps({"wb_product_id": row["wb_product_id"], "status": "succeeded"}, ensure_ascii=False) + "\n"
                    for row in rows
                ),
                encoding="utf-8",
            )
            (run_dir / "product_details.jsonl").write_text(
                "".join(
                    json.dumps({"wb_product_id": row["wb_product_id"], "status": "succeeded"}, ensure_ascii=False) + "\n"
                    for row in rows
                ),
                encoding="utf-8",
            )

    @staticmethod
    def _step_for(command: list[str]) -> str:
        joined = " ".join(command)
        if "pipelines/ranks/runner.py" in joined or "pipelines\\ranks\\runner.py" in joined:
            return "rank"
        if "pipelines/logistics/runner.py" in joined or "pipelines\\logistics\\runner.py" in joined:
            return "logistics"
        if "pipelines/reviews/runner.py" in joined or "pipelines\\reviews\\runner.py" in joined:
            return "reviews"
        if "pipelines/details/runner.py" in joined or "pipelines\\details\\runner.py" in joined:
            return "product_details"
        return "products"


class FakeStagingExecutor:
    def __init__(
        self,
        *,
        failure_kind: str | None = None,
        leak_secret: str | None = None,
        events: list[str] | None = None,
    ) -> None:
        self.failure_kind = failure_kind
        self.leak_secret = leak_secret
        self.events = events
        self.calls: list[list[str]] = []

    def __call__(
        self,
        *,
        command: list[str],
        cwd: Path,
        env: dict[str, str],
        timeout_seconds: int | None = None,
        output_callback=None,
    ) -> runner.CommandResult:
        self.calls.append(command)
        kind = self._kind_for(command)
        if self.events is not None:
            self.events.append(f"stage:{kind}")
        if self.failure_kind == kind:
            return runner.CommandResult(
                exit_code=1,
                stdout=f"failed stdout {self.leak_secret or ''}",
                stderr=f"{kind} failed {self.leak_secret or ''}",
            )

        payload = {
            "Mode": kind,
            "ParserRunId": Path(command[command.index("--connection-string") - 1]).name,
            "DryRun": False,
            "RowsRead": 11,
            "RowsWritten": 7,
            "RowsSkipped": 4,
            "ErrorCount": 0,
            "Details": {f"{kind}_rows_staged": 7},
        }
        suffix = f"\nconnection={self.leak_secret}" if self.leak_secret else ""
        return runner.CommandResult(exit_code=0, stdout=json.dumps(payload) + suffix)

    @staticmethod
    def _kind_for(command: list[str]) -> str:
        if "stage-complete-batch" in command:
            return "complete_batch"
        if "complete-parser-pipeline" in command:
            return "complete_pipeline"
        for item in command:
            if item == "stage-ranks":
                return "ranks"
            if item == "stage-products":
                return "products"
            if item == "stage-logistics":
                return "logistics"
            if item == "stage-reviews":
                return "reviews"
        return "unknown"


class MarketRefreshRunnerTests(unittest.TestCase):
    def _config(
        self,
        temp_dir: Path,
        *,
        review_subcategories: list[str] | None = None,
    ) -> runner.PipelineConfig:
        payload: dict[str, Any] = {
            "pipeline_name": "test_refresh",
            "output_base_dir": str(temp_dir / "output"),
            "defaults": {"fail_fast": False, "ingestion": "disabled"},
            "modes": {
                "smoke": {
                    "rank": {
                        "config": "Parser/presets/home_goods_search_rank_demo.json",
                        "context_id": "home_storage_organizers",
                        "top_n": 100,
                        "max_pages": 1,
                    },
                    "product": {
                        "config": "Parser/presets/home_goods_demo.env",
                        "env": {
                            "PARSER_MAX_CATALOG_PAGES_PER_SUBCATEGORY": "1",
                            "PARSER_MAX_ITEMS_PER_SUBCATEGORY": "50",
                        },
                    },
                    "logistics": {
                        "enabled": True,
                        "dest": 12354108,
                        "limit_products": 3,
                        "delay_ms": 500,
                        "timeout_sec": 10,
                        "retries": 1,
                    },
                    "reviews": {
                        "config": "Parser/presets/home_goods_demo.env",
                        "limit_products": 10,
                        "source_subcategories": review_subcategories or ["Storage"],
                        "smoke_only": True,
                    },
                }
            },
        }
        config_path = temp_dir / "pipeline.json"
        config_path.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
        return runner.PipelineConfig.load(config_path)

    def test_batch_product_run_dirs_split_products_and_rewrite_batch_scope(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            product_run_dir = temp_dir / "runs" / "wb_products_parent"
            product_run_dir.mkdir(parents=True)
            source_rows = [
                {
                    "schema_version": 1,
                    "marketplace": "wildberries",
                    "parser_run_id": "wb_products_parent",
                    "parsed_at_utc": "2026-06-15T10:00:00Z",
                    "wb_product_id": str(1000 + index),
                    "wb_root_id": str(9000 + index),
                    "name": f"Product {index}",
                }
                for index in range(250)
            ]
            (product_run_dir / "products.jsonl").write_text(
                "".join(json.dumps(row, ensure_ascii=False) + "\n" for row in source_rows),
                encoding="utf-8",
            )
            (product_run_dir / "manifest.json").write_text(
                json.dumps(
                    {
                        "schema_version": 1,
                        "parser_run_id": "wb_products_parent",
                        "started_at_utc": "2026-06-15T10:00:00Z",
                        "finished_at_utc": "2026-06-15T10:05:00Z",
                        "status": "succeeded",
                        "marketplace": "wildberries",
                        "requested_scope": {"pipeline_run_id": "parent-pipeline"},
                        "source_region_dest": "12354108",
                        "row_counts": {"total_rows": 250, "unique_rows": 250, "duplicate_rows": 0},
                        "category_results": [],
                        "parser_version": "test",
                        "config_snapshot": {},
                    },
                    ensure_ascii=False,
                ),
                encoding="utf-8",
            )

            batches = runner._create_product_batch_run_dirs(
                product_run_dir=product_run_dir,
                batches_dir=temp_dir / "batches",
                pipeline_run_id="market_refresh_test",
                batch_size=100,
                worker_id="worker-a",
                shard_key="three-niches",
            )
            first_manifest = json.loads((batches[0].product_run_dir / "manifest.json").read_text(encoding="utf-8"))
            first_row = json.loads((batches[0].product_run_dir / "products.jsonl").read_text(encoding="utf-8").splitlines()[0])

        self.assertEqual([batch.size for batch in batches], [100, 100, 50])
        self.assertEqual([batch.batch_index for batch in batches], [1, 2, 3])
        self.assertEqual(first_manifest["parser_run_id"], "market_refresh_test_batch_0001_products")
        self.assertTrue(first_manifest["requested_scope"]["is_complete_card_batch"])
        self.assertEqual(first_manifest["requested_scope"]["batch_id"], "market_refresh_test:batch:0001")
        self.assertEqual(first_manifest["requested_scope"]["worker_id"], "worker-a")
        self.assertEqual(first_row["parser_run_id"], "market_refresh_test_batch_0001_products")

    def test_batch_product_run_dir_uses_batch_timestamp_for_manifest_and_rows(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            parent_manifest = {
                "schema_version": 1,
                "parser_run_id": "wb_products_parent",
                "started_at_utc": "2026-06-15T10:00:00Z",
                "finished_at_utc": "2026-06-15T10:05:00Z",
                "status": "succeeded",
                "marketplace": "wildberries",
                "requested_scope": {"pipeline_run_id": "parent-pipeline"},
                "source_region_dest": "12354108",
                "parser_version": "test",
                "config_snapshot": {},
            }
            rows = [
                {
                    "schema_version": 1,
                    "marketplace": "wildberries",
                    "parser_run_id": "wb_products_parent",
                    "parsed_at_utc": "2026-06-15T10:00:01Z",
                    "wb_product_id": "1001",
                    "wb_root_id": "5001",
                    "name": "Product 1",
                    "source_subcategory": "one",
                },
                {
                    "schema_version": 1,
                    "marketplace": "wildberries",
                    "parser_run_id": "wb_products_parent",
                    "parsed_at_utc": "2026-06-15T10:00:02Z",
                    "wb_product_id": "1002",
                    "wb_root_id": "5002",
                    "name": "Product 2",
                    "source_subcategory": "one",
                },
            ]

            batch = runner._create_product_batch_run_dir_from_rows(
                parent_manifest=parent_manifest,
                batch_rows=rows,
                source_product_run_dir=temp_dir / "parent",
                batches_dir=temp_dir / "batches",
                pipeline_run_id="market_refresh_test",
                batch_index=1,
                worker_id="worker-a",
                shard_key="three-niches",
            )
            manifest = json.loads((batch.product_run_dir / "manifest.json").read_text(encoding="utf-8"))
            child_rows = [
                json.loads(line)
                for line in (batch.product_run_dir / "products.jsonl").read_text(encoding="utf-8").splitlines()
            ]

        self.assertNotEqual(manifest["started_at_utc"], parent_manifest["started_at_utc"])
        self.assertEqual(manifest["finished_at_utc"], manifest["started_at_utc"])
        self.assertEqual({row["parsed_at_utc"] for row in child_rows}, {manifest["started_at_utc"]})

    def test_complete_batch_staging_command_uses_single_batch_dir(self) -> None:
        command = runner._complete_batch_staging_command(
            batch_dir=Path("C:/tmp/batches/batch_0001"),
            connection_string="Host=localhost;Password=secret",
        )

        self.assertIn("stage-complete-batch", command)
        self.assertIn("C:/tmp/batches/batch_0001", [part.replace("\\", "/") for part in command])
        self.assertEqual(command[-2:], ["--connection-string", "Host=localhost;Password=secret"])

    def test_batched_full_enrichment_stages_each_complete_batch(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            config.payload["modes"]["batched_full_enrichment"] = {
                **config.payload["modes"]["smoke"],
                "batching": {"batch_size": 2, "worker_id": "worker-a", "shard_key": "three-niches"},
                "product_details": {
                    "enabled": True,
                    "limit_products": 2,
                    "delay_ms": 1,
                    "timeout_sec": 1,
                    "retries": 0,
                },
            }
            config.modes = dict(config.payload["modes"])
            fake = FakeExecutor(output_base_dir=config.output_base_dir)
            staging = FakeStagingExecutor()

            def streaming_runner(**kwargs: Any) -> Path:
                parent_dir = temp_dir / "output" / "runs" / "wb_products_stream"
                parent_dir.mkdir(parents=True, exist_ok=True)
                parent_manifest = {
                    "schema_version": 1,
                    "parser_run_id": "wb_products_stream",
                    "started_at_utc": "2026-06-15T10:00:00Z",
                    "status": "succeeded",
                    "marketplace": "wildberries",
                    "requested_scope": {},
                    "source_region_dest": "12354108",
                    "row_counts": {"total_rows": 3, "unique_rows": 3, "duplicate_rows": 0},
                    "category_results": [],
                    "parser_version": "test",
                    "config_snapshot": {},
                }
                (parent_dir / "manifest.json").write_text(json.dumps(parent_manifest), encoding="utf-8")
                rows = [
                    {"wb_product_id": "1001", "wb_root_id": "5001", "marketplace": "wildberries", "source_subcategory": "one"},
                    {"wb_product_id": "1002", "wb_root_id": "5002", "marketplace": "wildberries", "source_subcategory": "one"},
                    {"wb_product_id": "1003", "wb_root_id": "5003", "marketplace": "wildberries", "source_subcategory": "two"},
                ]
                (parent_dir / "products.jsonl").write_text(
                    "".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows),
                    encoding="utf-8",
                )
                kwargs["batch_handler"](SimpleNamespace(batch_index=1, rows=rows[:2], parent_run_dir=parent_dir))
                kwargs["batch_handler"](SimpleNamespace(batch_index=2, rows=rows[2:], parent_run_dir=parent_dir))
                return parent_dir

            run_dir = runner.run_pipeline(
                config=config,
                mode="batched_full_enrichment",
                stage_to_db=True,
                connection_string="Host=localhost;Password=secret",
                connection_string_source="argument",
                executor=fake,
                staging_executor=staging,
                streaming_product_runner=streaming_runner,
            )
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))
            first_batch_manifest = json.loads(
                (
                    Path(manifest["batching"]["batches"][0]["batch_dir"])
                    / "batch_manifest.json"
                ).read_text(encoding="utf-8")
            )

        self.assertEqual(
            [fake._step_for(command) for command in fake.calls],
            [
                "rank",
                "logistics",
                "reviews",
                "product_details",
                "logistics",
                "reviews",
                "product_details",
            ],
        )
        self.assertEqual(
            [FakeStagingExecutor._kind_for(command) for command in staging.calls],
            ["ranks", "complete_batch", "complete_batch", "complete_pipeline"],
        )
        self.assertEqual(manifest["batching"]["total_batches"], 2)
        self.assertEqual([batch["status"] for batch in manifest["batching"]["batches"]], ["staged", "staged"])
        self.assertEqual(manifest["staging"]["status"], "succeeded")
        self.assertEqual([step["step"] for step in first_batch_manifest["steps"]], ["logistics", "reviews", "product_details"])
        self.assertTrue(all(step["output_run_dirs"] for step in first_batch_manifest["steps"]))

    def test_batched_full_enrichment_streams_first_batch_before_discovery_finishes(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            config.payload["modes"]["batched_full_enrichment"] = {
                **config.payload["modes"]["smoke"],
                "batching": {"batch_size": 2, "worker_id": "worker-a", "shard_key": "three-niches"},
                "product_details": {"enabled": True, "delay_ms": 1, "timeout_sec": 1, "retries": 0},
            }
            config.modes = dict(config.payload["modes"])
            events: list[str] = []
            fake = FakeExecutor(output_base_dir=config.output_base_dir, events=events)
            staging = FakeStagingExecutor(events=events)

            def streaming_runner(**kwargs: Any) -> Path:
                parent_dir = temp_dir / "output" / "runs" / "wb_products_stream"
                parent_dir.mkdir(parents=True, exist_ok=True)
                parent_manifest = {
                    "schema_version": 1,
                    "parser_run_id": "wb_products_stream",
                    "started_at_utc": "2026-06-15T10:00:00Z",
                    "status": "running",
                    "marketplace": "wildberries",
                    "requested_scope": {"pipeline_run_id": "stream-parent"},
                    "source_region_dest": "12354108",
                    "row_counts": {"total_rows": 0, "unique_rows": 0, "duplicate_rows": 0},
                    "category_results": [],
                    "parser_version": "test",
                    "config_snapshot": {},
                }
                (parent_dir / "manifest.json").write_text(json.dumps(parent_manifest), encoding="utf-8")
                rows = [
                    {"wb_product_id": "2001", "wb_root_id": "6001", "marketplace": "wildberries", "source_subcategory": "one"},
                    {"wb_product_id": "2002", "wb_root_id": "6002", "marketplace": "wildberries", "source_subcategory": "one"},
                    {"wb_product_id": "2003", "wb_root_id": "6003", "marketplace": "wildberries", "source_subcategory": "two"},
                ]
                batch_handler = kwargs["batch_handler"]
                events.append("discover:batch1")
                batch_handler(SimpleNamespace(batch_index=1, rows=rows[:2], parent_run_dir=parent_dir))
                events.append("discover:batch2")
                batch_handler(SimpleNamespace(batch_index=2, rows=rows[2:], parent_run_dir=parent_dir))
                parent_manifest["status"] = "succeeded"
                parent_manifest["row_counts"] = {"total_rows": 3, "unique_rows": 3, "duplicate_rows": 0}
                (parent_dir / "products.jsonl").write_text(
                    "".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows),
                    encoding="utf-8",
                )
                (parent_dir / "manifest.json").write_text(json.dumps(parent_manifest), encoding="utf-8")
                return parent_dir

            run_dir = runner.run_pipeline(
                config=config,
                mode="batched_full_enrichment",
                stage_to_db=True,
                connection_string="Host=localhost;Password=secret",
                connection_string_source="argument",
                executor=fake,
                staging_executor=staging,
                streaming_product_runner=streaming_runner,
            )
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertNotIn("exec:products", events)
        self.assertLess(events.index("stage:complete_batch"), events.index("discover:batch2"))
        self.assertEqual(manifest["batching"]["total_batches"], 2)
        self.assertEqual([batch["status"] for batch in manifest["batching"]["batches"]], ["staged", "staged"])

    def test_batched_full_enrichment_stops_after_configured_batch_limit(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            config.payload["modes"]["batched_full_enrichment"] = {
                **config.payload["modes"]["smoke"],
                "batching": {
                    "batch_size": 2,
                    "max_batches": 1,
                    "worker_id": "worker-a",
                    "shard_key": "three-niches",
                },
                "product_details": {"enabled": True, "delay_ms": 1, "timeout_sec": 1, "retries": 0},
            }
            config.modes = dict(config.payload["modes"])
            events: list[str] = []
            fake = FakeExecutor(output_base_dir=config.output_base_dir, events=events)
            staging = FakeStagingExecutor(events=events)

            def streaming_runner(**kwargs: Any) -> Path:
                parent_dir = temp_dir / "output" / "runs" / "wb_products_stream"
                parent_dir.mkdir(parents=True, exist_ok=True)
                parent_manifest = {
                    "schema_version": 1,
                    "parser_run_id": "wb_products_stream",
                    "started_at_utc": "2026-06-15T10:00:00Z",
                    "status": "running",
                    "marketplace": "wildberries",
                    "requested_scope": {},
                    "source_region_dest": "12354108",
                    "row_counts": {"total_rows": 4, "unique_rows": 4, "duplicate_rows": 0},
                    "category_results": [],
                    "parser_version": "test",
                    "config_snapshot": {},
                }
                (parent_dir / "manifest.json").write_text(json.dumps(parent_manifest), encoding="utf-8")
                rows = [
                    {"wb_product_id": "3001", "wb_root_id": "7001", "marketplace": "wildberries", "source_subcategory": "one"},
                    {"wb_product_id": "3002", "wb_root_id": "7002", "marketplace": "wildberries", "source_subcategory": "one"},
                    {"wb_product_id": "3003", "wb_root_id": "7003", "marketplace": "wildberries", "source_subcategory": "two"},
                    {"wb_product_id": "3004", "wb_root_id": "7004", "marketplace": "wildberries", "source_subcategory": "two"},
                ]
                (parent_dir / "products.jsonl").write_text(
                    "".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows),
                    encoding="utf-8",
                )

                events.append("discover:batch1")
                result = kwargs["batch_handler"](
                    SimpleNamespace(batch_index=1, rows=rows[:2], parent_run_dir=parent_dir)
                )
                if result.status not in {"stop", "stopped"}:
                    events.append("discover:batch2")
                    kwargs["batch_handler"](SimpleNamespace(batch_index=2, rows=rows[2:], parent_run_dir=parent_dir))
                return parent_dir

            run_dir = runner.run_pipeline(
                config=config,
                mode="batched_full_enrichment",
                stage_to_db=True,
                connection_string="Host=localhost;Password=secret",
                connection_string_source="argument",
                executor=fake,
                staging_executor=staging,
                streaming_product_runner=streaming_runner,
            )
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertIn("discover:batch1", events)
        self.assertNotIn("discover:batch2", events)
        self.assertEqual(manifest["batching"]["max_batches"], 1)
        self.assertEqual(manifest["batching"]["total_batches"], 1)
        self.assertEqual(manifest["batching"]["staged_batches"], 1)
        self.assertEqual([batch["status"] for batch in manifest["batching"]["batches"]], ["staged"])
        self.assertEqual(
            [FakeStagingExecutor._kind_for(command) for command in staging.calls],
            ["ranks", "complete_batch", "complete_pipeline"],
        )

    def test_batched_full_enrichment_quarantines_incomplete_batch_before_staging(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            config.payload["modes"]["batched_full_enrichment"] = {
                **config.payload["modes"]["smoke"],
                "batching": {"batch_size": 2, "worker_id": "worker-a", "shard_key": "three-niches"},
                "product_details": {"enabled": True, "delay_ms": 1, "timeout_sec": 1, "retries": 0},
            }
            config.modes = dict(config.payload["modes"])
            fake = FakeExecutor(output_base_dir=config.output_base_dir, incomplete_steps={"product_details"})
            staging = FakeStagingExecutor()

            def streaming_runner(**kwargs: Any) -> Path:
                parent_dir = temp_dir / "output" / "runs" / "wb_products_stream"
                parent_dir.mkdir(parents=True, exist_ok=True)
                parent_manifest = {
                    "schema_version": 1,
                    "parser_run_id": "wb_products_stream",
                    "started_at_utc": "2026-06-15T10:00:00Z",
                    "status": "succeeded",
                    "marketplace": "wildberries",
                    "requested_scope": {},
                    "source_region_dest": "12354108",
                    "row_counts": {"total_rows": 2, "unique_rows": 2, "duplicate_rows": 0},
                    "category_results": [],
                    "parser_version": "test",
                    "config_snapshot": {},
                }
                (parent_dir / "manifest.json").write_text(json.dumps(parent_manifest), encoding="utf-8")
                rows = [
                    {"wb_product_id": "3001", "wb_root_id": "7001", "marketplace": "wildberries", "source_subcategory": "one"},
                    {"wb_product_id": "3002", "wb_root_id": "7002", "marketplace": "wildberries", "source_subcategory": "one"},
                ]
                (parent_dir / "products.jsonl").write_text(
                    "".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows),
                    encoding="utf-8",
                )
                kwargs["batch_handler"](SimpleNamespace(batch_index=1, rows=rows, parent_run_dir=parent_dir))
                return parent_dir

            run_dir = runner.run_pipeline(
                config=config,
                mode="batched_full_enrichment",
                stage_to_db=True,
                connection_string="Host=localhost;Password=secret",
                connection_string_source="argument",
                executor=fake,
                staging_executor=staging,
                streaming_product_runner=streaming_runner,
            )
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertEqual([FakeStagingExecutor._kind_for(command) for command in staging.calls], ["ranks", "complete_pipeline"])
        self.assertEqual(manifest["batching"]["batches"][0]["status"], "quarantined")
        self.assertEqual(manifest["batching"]["quarantined_batches"], 1)
        self.assertEqual(manifest["batching"]["quarantined_products"], 2)

    def test_batched_full_enrichment_resume_passes_staged_product_ids_to_streaming_runner(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            config.payload["modes"]["batched_full_enrichment"] = {
                **config.payload["modes"]["smoke"],
                "batching": {"batch_size": 2, "worker_id": "worker-a", "shard_key": "three-niches"},
                "product_details": {"enabled": True, "delay_ms": 1, "timeout_sec": 1, "retries": 0},
            }
            config.modes = dict(config.payload["modes"])

            def first_streaming_runner(**kwargs: Any) -> Path:
                parent_dir = temp_dir / "output" / "runs" / "wb_products_stream_first"
                parent_dir.mkdir(parents=True, exist_ok=True)
                parent_manifest = {
                    "schema_version": 1,
                    "parser_run_id": "wb_products_stream_first",
                    "started_at_utc": "2026-06-15T10:00:00Z",
                    "status": "succeeded",
                    "marketplace": "wildberries",
                    "requested_scope": {},
                    "source_region_dest": "12354108",
                    "row_counts": {"total_rows": 2, "unique_rows": 2, "duplicate_rows": 0},
                    "category_results": [],
                    "parser_version": "test",
                    "config_snapshot": {},
                }
                rows = [
                    {"wb_product_id": "4001", "wb_root_id": "8001", "marketplace": "wildberries", "source_subcategory": "one"},
                    {"wb_product_id": "4002", "wb_root_id": "8002", "marketplace": "wildberries", "source_subcategory": "one"},
                ]
                (parent_dir / "manifest.json").write_text(json.dumps(parent_manifest), encoding="utf-8")
                (parent_dir / "products.jsonl").write_text(
                    "".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows),
                    encoding="utf-8",
                )
                kwargs["batch_handler"](SimpleNamespace(batch_index=1, rows=rows, parent_run_dir=parent_dir))
                return parent_dir

            run_dir = runner.run_pipeline(
                config=config,
                mode="batched_full_enrichment",
                stage_to_db=True,
                connection_string="Host=localhost;Password=secret",
                connection_string_source="argument",
                executor=FakeExecutor(output_base_dir=config.output_base_dir),
                staging_executor=FakeStagingExecutor(),
                streaming_product_runner=first_streaming_runner,
            )
            seen_on_resume: set[str] = set()

            def resumed_streaming_runner(**kwargs: Any) -> Path:
                seen_on_resume.update(str(value) for value in kwargs["resume_seen_product_ids"])
                parent_dir = temp_dir / "output" / "runs" / "wb_products_stream_resume"
                parent_dir.mkdir(parents=True, exist_ok=True)
                parent_manifest = {
                    "schema_version": 1,
                    "parser_run_id": "wb_products_stream_resume",
                    "started_at_utc": "2026-06-15T10:10:00Z",
                    "status": "succeeded",
                    "marketplace": "wildberries",
                    "requested_scope": {},
                    "source_region_dest": "12354108",
                    "row_counts": {"total_rows": 0, "unique_rows": 0, "duplicate_rows": 0},
                    "category_results": [],
                    "parser_version": "test",
                    "config_snapshot": {},
                }
                (parent_dir / "manifest.json").write_text(json.dumps(parent_manifest), encoding="utf-8")
                (parent_dir / "products.jsonl").write_text("", encoding="utf-8")
                return parent_dir

            resumed_dir = runner.run_pipeline(
                config=config,
                mode="batched_full_enrichment",
                resume_run_dir=run_dir,
                stage_to_db=True,
                connection_string="Host=localhost;Password=secret",
                connection_string_source="argument",
                executor=FakeExecutor(output_base_dir=config.output_base_dir),
                staging_executor=FakeStagingExecutor(),
                streaming_product_runner=resumed_streaming_runner,
            )
            manifest = json.loads((resumed_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertEqual(seen_on_resume, {"4001", "4002"})
        self.assertEqual(len(manifest["batching"]["batches"]), 1)

    def test_dry_run_writes_planned_commands_without_execution(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir)

            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                run_dir = runner.run_pipeline(config=config, mode="smoke", dry_run=True, executor=fake)
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertEqual(fake.calls, [])
        self.assertIn("[market-refresh] DRY-RUN rank", output.getvalue())
        self.assertIn("[market-refresh] PLAN products", output.getvalue())
        self.assertIn("[market-refresh] PLAN logistics", output.getvalue())
        self.assertEqual(
            [step["step_name"] for step in manifest["steps"]],
            ["rank", "products", "logistics", "reviews", "product_details"],
        )
        self.assertEqual({step["status"] for step in manifest["steps"]}, {"skipped"})
        self.assertEqual(manifest["status"], "succeeded")
        self.assertTrue(manifest["dry_run"])

    def test_pipeline_runs_rank_products_logistics_reviews_in_order(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir)

            run_dir = runner.run_pipeline(config=config, mode="smoke", executor=fake)
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertEqual([fake._step_for(command) for command in fake.calls], ["rank", "products", "logistics", "reviews"])
        self.assertEqual(manifest["status"], "succeeded")
        self.assertEqual(
            [command[5] for command in manifest["suggested_ingestion_commands"]],
            ["stage-ranks", "stage-products", "stage-logistics", "stage-reviews"],
        )
        self.assertEqual(manifest["staging"]["status"], "not_requested")

    def test_test_run_scope_is_passed_to_child_commands(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir)

            run_dir = runner.run_pipeline(
                config=config,
                mode="smoke",
                test_run=True,
                test_label="delivery-profile-100",
                executor=fake,
            )
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertTrue(manifest["is_test_run"])
        self.assertEqual(manifest["test_label"], "delivery-profile-100")
        self.assertGreater(len(fake.envs), 0)
        for env in fake.envs:
            self.assertEqual(env["PARSER_IS_TEST_RUN"], "true")
            self.assertEqual(env["PARSER_TEST_LABEL"], "delivery-profile-100")
            self.assertEqual(env["PARSER_RUN_PURPOSE"], "parser_testing")
            self.assertEqual(env["PARSER_PIPELINE_RUN_ID"], manifest["pipeline_run_id"])

    def test_product_failure_blocks_reviews_and_keeps_rank_partial(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir, failures={"products"})

            run_dir = runner.run_pipeline(config=config, mode="smoke", executor=fake)
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        statuses = {step["step_name"]: step["status"] for step in manifest["steps"]}
        self.assertEqual(statuses["rank"], "succeeded")
        self.assertEqual(statuses["products"], "failed")
        self.assertEqual(statuses["logistics"], "skipped")
        self.assertEqual(statuses["reviews"], "skipped")
        self.assertEqual(manifest["status"], "partial")

    def test_product_partial_with_usable_rows_continues_logistics_reviews_and_staging(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir, partials={"products"})
            staging = FakeStagingExecutor()

            run_dir = runner.run_pipeline(
                config=config,
                mode="smoke",
                stage_to_db=True,
                connection_string="Host=localhost;Password=secret",
                connection_string_source="argument",
                executor=fake,
                staging_executor=staging,
            )
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        statuses = {step["step_name"]: step["status"] for step in manifest["steps"]}
        self.assertEqual([fake._step_for(command) for command in fake.calls], ["rank", "products", "logistics", "reviews"])
        self.assertEqual(statuses["products"], "partial")
        self.assertEqual(statuses["logistics"], "succeeded")
        self.assertEqual(statuses["reviews"], "succeeded")
        self.assertEqual([FakeStagingExecutor._kind_for(command) for command in staging.calls], ["ranks", "products", "logistics", "reviews"])
        self.assertEqual(manifest["status"], "partial")
        self.assertEqual(manifest["staging"]["status"], "succeeded")

    def test_rank_failure_does_not_block_products_logistics_and_reviews(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir, failures={"rank"})

            run_dir = runner.run_pipeline(config=config, mode="smoke", executor=fake)
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        statuses = {step["step_name"]: step["status"] for step in manifest["steps"]}
        self.assertEqual(statuses["rank"], "failed")
        self.assertEqual(statuses["products"], "succeeded")
        self.assertEqual(statuses["logistics"], "succeeded")
        self.assertEqual(statuses["reviews"], "succeeded")
        self.assertEqual(manifest["status"], "partial")

    def test_logistics_failure_does_not_block_reviews(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir, failures={"logistics"})

            run_dir = runner.run_pipeline(config=config, mode="smoke", executor=fake)
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        statuses = {step["step_name"]: step["status"] for step in manifest["steps"]}
        self.assertEqual([fake._step_for(command) for command in fake.calls], ["rank", "products", "logistics", "reviews"])
        self.assertEqual(statuses["products"], "succeeded")
        self.assertEqual(statuses["logistics"], "failed")
        self.assertEqual(statuses["reviews"], "succeeded")
        self.assertEqual(manifest["status"], "partial")

    def test_skip_logistics_still_allows_reviews(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir)

            run_dir = runner.run_pipeline(config=config, mode="smoke", skip_logistics=True, executor=fake)
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        statuses = {step["step_name"]: step["status"] for step in manifest["steps"]}
        self.assertEqual([fake._step_for(command) for command in fake.calls], ["rank", "products", "reviews"])
        self.assertEqual(statuses["logistics"], "skipped")
        self.assertEqual(statuses["reviews"], "succeeded")
        self.assertEqual(manifest["status"], "succeeded")

    def test_stage_to_db_runs_ranks_products_logistics_reviews_in_order(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir, review_subcategories=["one", "two"])
            fake = FakeExecutor(output_base_dir=config.output_base_dir)
            staging = FakeStagingExecutor()

            run_dir = runner.run_pipeline(
                config=config,
                mode="smoke",
                stage_to_db=True,
                connection_string="Host=localhost;Password=secret",
                connection_string_source="argument",
                executor=fake,
                staging_executor=staging,
            )
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertEqual([FakeStagingExecutor._kind_for(command) for command in staging.calls], ["ranks", "products", "logistics", "reviews", "reviews"])
        self.assertEqual([command[5] for command in manifest["suggested_ingestion_commands"]], ["stage-ranks", "stage-products", "stage-logistics", "stage-reviews", "stage-reviews"])
        self.assertEqual(manifest["staging"]["unsupported_steps"], [])
        self.assertEqual(manifest["status"], "succeeded")
        self.assertEqual(manifest["staging"]["status"], "succeeded")
        self.assertEqual(manifest["staging"]["connection_string_source"], "argument")
        self.assertEqual(manifest["staging"]["commands"][0]["rows_read"], 11)
        self.assertEqual(manifest["staging"]["commands"][0]["rows_written"], 7)
        self.assertEqual(manifest["staging"]["commands"][0]["rows_skipped"], 4)
        self.assertEqual(manifest["staging"]["commands"][0]["error_count"], 0)
        self.assertEqual(manifest["staging"]["commands"][0]["details"], {"ranks_rows_staged": 7})

    def test_stage_to_db_without_connection_string_fails_before_parser_execution(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir)

            with self.assertRaisesRegex(ValueError, "connection string"):
                runner.run_pipeline(
                    config=config,
                    mode="smoke",
                    stage_to_db=True,
                    executor=fake,
                )

        self.assertEqual(fake.calls, [])

    def test_dry_run_stage_to_db_skips_staging(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir)
            staging = FakeStagingExecutor()

            run_dir = runner.run_pipeline(
                config=config,
                mode="smoke",
                dry_run=True,
                stage_to_db=True,
                connection_string="Host=localhost;Password=secret",
                connection_string_source="argument",
                executor=fake,
                staging_executor=staging,
            )
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertEqual(fake.calls, [])
        self.assertEqual(staging.calls, [])
        self.assertEqual(manifest["status"], "succeeded")
        self.assertEqual(manifest["staging"]["status"], "skipped")
        self.assertEqual(manifest["staging"]["skip_reason"], "dry_run")

    def test_stage_to_db_redacts_connection_string_from_manifest_and_log(self) -> None:
        secret = "Host=localhost;Port=5432;Password=super-secret"
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir)
            staging = FakeStagingExecutor(leak_secret=secret)

            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                run_dir = runner.run_pipeline(
                    config=config,
                    mode="smoke",
                    stage_to_db=True,
                    connection_string=secret,
                    connection_string_source="argument",
                    executor=fake,
                    staging_executor=staging,
                )
            manifest_text = (run_dir / "pipeline_manifest.json").read_text(encoding="utf-8")
            log_text = (run_dir / "pipeline.log").read_text(encoding="utf-8")

        self.assertNotIn(secret, manifest_text)
        self.assertNotIn(secret, log_text)
        self.assertNotIn(secret, output.getvalue())
        self.assertIn("<connection-string>", manifest_text)
        self.assertIn("<redacted>", manifest_text)

    def test_staging_failure_marks_pipeline_partial_without_deleting_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir)
            staging = FakeStagingExecutor(failure_kind="products")

            run_dir = runner.run_pipeline(
                config=config,
                mode="smoke",
                stage_to_db=True,
                connection_string="Host=localhost;Password=secret",
                connection_string_source="argument",
                executor=fake,
                staging_executor=staging,
            )
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))
            artifact_dirs = [
                Path(run_dir_value)
                for step in manifest["steps"]
                for run_dir_value in (step.get("output_run_dirs") or [])
            ]
            artifacts_exist = all(path.exists() for path in artifact_dirs)

        self.assertEqual([FakeStagingExecutor._kind_for(command) for command in staging.calls], ["ranks", "products"])
        self.assertEqual(manifest["status"], "partial")
        self.assertEqual(manifest["staging"]["status"], "failed")
        self.assertTrue(artifact_dirs)
        self.assertTrue(artifacts_exist)

    def test_stage_to_db_env_parser(self) -> None:
        for value in ("1", "true", "yes", "on"):
            self.assertTrue(runner._parse_stage_to_db_env(value))

        for value in (None, "", "0", "false", "no", "off"):
            self.assertFalse(runner._parse_stage_to_db_env(value))

        with self.assertRaisesRegex(ValueError, "PARSER_STAGE_TO_DB"):
            runner._parse_stage_to_db_env("maybe")

    def test_staging_preflight_resolves_env_connection_sources(self) -> None:
        enabled = runner._resolve_staging_preflight(
            stage_to_db_arg=False,
            connection_string_arg=None,
            environ={
                "PARSER_STAGE_TO_DB": "yes",
                "ConnectionStrings__Postgres": "from-env",
                "ASHMES_POSTGRES_CONNECTION": "fallback-env",
            },
        )
        disabled = runner._resolve_staging_preflight(
            stage_to_db_arg=False,
            connection_string_arg=None,
            environ={"PARSER_STAGE_TO_DB": "off", "ConnectionStrings__Postgres": "from-env"},
        )

        self.assertTrue(enabled.requested)
        self.assertEqual(enabled.connection_string, "from-env")
        self.assertEqual(enabled.connection_string_source, "ConnectionStrings__Postgres")
        self.assertFalse(disabled.requested)
        self.assertIsNone(disabled.connection_string)

    def test_resume_skips_succeeded_steps(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            first_fake = FakeExecutor(output_base_dir=config.output_base_dir)
            run_dir = runner.run_pipeline(config=config, mode="smoke", executor=first_fake)

            second_fake = FakeExecutor(output_base_dir=config.output_base_dir)
            resumed_dir = runner.run_pipeline(
                config=config,
                mode="smoke",
                resume_run_dir=run_dir,
                executor=second_fake,
            )
            manifest = json.loads((resumed_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertEqual(run_dir, resumed_dir)
        self.assertEqual(second_fake.calls, [])
        self.assertEqual(manifest["status"], "succeeded")

    def test_resume_skips_completed_logistics(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            first_fake = FakeExecutor(output_base_dir=config.output_base_dir)
            run_dir = runner.run_pipeline(config=config, mode="smoke", executor=first_fake)

            second_fake = FakeExecutor(output_base_dir=config.output_base_dir)
            resumed_dir = runner.run_pipeline(
                config=config,
                mode="smoke",
                resume_run_dir=run_dir,
                executor=second_fake,
            )
            manifest = json.loads((resumed_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        statuses = {step["step_name"]: step["status"] for step in manifest["steps"]}
        self.assertEqual(second_fake.calls, [])
        self.assertEqual(statuses["logistics"], "succeeded")

    def test_force_step_reruns_only_requested_step(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            first_fake = FakeExecutor(output_base_dir=config.output_base_dir)
            run_dir = runner.run_pipeline(config=config, mode="smoke", executor=first_fake)

            second_fake = FakeExecutor(output_base_dir=config.output_base_dir)
            runner.run_pipeline(
                config=config,
                mode="smoke",
                resume_run_dir=run_dir,
                force_step="rank",
                executor=second_fake,
            )

        self.assertEqual([second_fake._step_for(command) for command in second_fake.calls], ["rank"])

    def test_force_step_logistics_reruns_only_logistics(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            first_fake = FakeExecutor(output_base_dir=config.output_base_dir)
            run_dir = runner.run_pipeline(config=config, mode="smoke", executor=first_fake)

            second_fake = FakeExecutor(output_base_dir=config.output_base_dir)
            runner.run_pipeline(
                config=config,
                mode="smoke",
                resume_run_dir=run_dir,
                force_step="logistics",
                executor=second_fake,
            )

        self.assertEqual([second_fake._step_for(command) for command in second_fake.calls], ["logistics"])

    def test_streaming_executor_returns_captured_output(self) -> None:
        captured: list[str] = []
        result = runner._execute_subprocess(
            command=[sys.executable, "-c", "print('Rank run directory: C:/tmp/wb_rank_test')"],
            cwd=PARSER_DIR.parent,
            env=os.environ.copy(),
            output_callback=captured.append,
        )

        self.assertEqual(result.exit_code, 0)
        self.assertIn("Rank run directory: C:/tmp/wb_rank_test", result.stdout)
        self.assertIn("Rank run directory: C:/tmp/wb_rank_test", captured)


if __name__ == "__main__":
    unittest.main()
