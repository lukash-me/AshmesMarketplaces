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
    def __init__(self, *, output_base_dir: Path, failures: set[str] | None = None) -> None:
        self.output_base_dir = output_base_dir
        self.failures = failures or set()
        self.calls: list[list[str]] = []
        self.counters = {"rank": 0, "products": 0, "reviews": 0}

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
        (run_dir / "manifest.json").write_text(
            json.dumps(
                {
                    "parser_run_id": run_dir.name,
                    "run_kind": "wb_search_rank" if step == "rank" else None,
                    "status": "succeeded",
                    "row_counts": {"rank_rows_written": 100} if step == "rank" else {"unique_rows": 10},
                    "counters": {"reviews_written": 1} if step == "reviews" else None,
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        if step == "products":
            (run_dir / "products.jsonl").write_text("{}", encoding="utf-8")

        marker = {
            "rank": "Rank run directory:",
            "products": "Run directory:",
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
        if "reviews_runner.py" in joined:
            return "reviews"
        return "products"


class MarketRefreshRunnerTests(unittest.TestCase):
    def _config(self, temp_dir: Path) -> runner.PipelineConfig:
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
                    "reviews": {
                        "config": "Parser/presets/home_goods_demo.env",
                        "limit_products": 10,
                        "source_subcategories": ["Органайзеры для хранения вещей"],
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
        self.assertEqual([step["step_name"] for step in manifest["steps"]], ["rank", "products", "reviews"])
        self.assertEqual({step["status"] for step in manifest["steps"]}, {"skipped"})
        self.assertEqual(manifest["status"], "succeeded")
        self.assertTrue(manifest["dry_run"])

    def test_pipeline_runs_rank_products_reviews_in_order(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir)

            run_dir = runner.run_pipeline(config=config, mode="smoke", executor=fake)
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        self.assertEqual([fake._step_for(command) for command in fake.calls], ["rank", "products", "reviews"])
        self.assertEqual(manifest["status"], "succeeded")
        self.assertEqual(len(manifest["suggested_ingestion_commands"]), 2)

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
        self.assertEqual(statuses["reviews"], "skipped")
        self.assertEqual(manifest["status"], "partial")

    def test_rank_failure_does_not_block_products_and_reviews(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config = self._config(temp_dir)
            fake = FakeExecutor(output_base_dir=config.output_base_dir, failures={"rank"})

            run_dir = runner.run_pipeline(config=config, mode="smoke", executor=fake)
            manifest = json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))

        statuses = {step["step_name"]: step["status"] for step in manifest["steps"]}
        self.assertEqual(statuses["rank"], "failed")
        self.assertEqual(statuses["products"], "succeeded")
        self.assertEqual(statuses["reviews"], "succeeded")
        self.assertEqual(manifest["status"], "partial")

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
