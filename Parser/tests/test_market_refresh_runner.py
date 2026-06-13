from __future__ import annotations

import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

import market_refresh_runner as runner  # noqa: E402


class FakeExecutor:
    def __init__(
        self,
        *,
        output_base_dir: Path,
        failures: set[str] | None = None,
        partials: set[str] | None = None,
    ) -> None:
        self.output_base_dir = output_base_dir
        self.failures = failures or set()
        self.partials = partials or set()
        self.calls: list[list[str]] = []
        self.counters = {"rank": 0, "products": 0, "logistics": 0, "reviews": 0}

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
        step = self._step_for(command)
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

        marker = {
            "rank": "Rank run directory:",
            "products": "Run directory:",
            "logistics": "Logistics run directory:",
            "reviews": "Review run directory:",
        }[step]
        output = f"{marker} {run_dir}"
        if output_callback:
            output_callback(output)
        return runner.CommandResult(exit_code=0, stdout=output + "\n")

    @staticmethod
    def _step_for(command: list[str]) -> str:
        joined = " ".join(command)
        if "rank_runner.py" in joined:
            return "rank"
        if "logistics_runner.py" in joined:
            return "logistics"
        if "reviews_runner.py" in joined:
            return "reviews"
        return "products"


class FakeStagingExecutor:
    def __init__(self, *, failure_kind: str | None = None, leak_secret: str | None = None) -> None:
        self.failure_kind = failure_kind
        self.leak_secret = leak_secret
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
