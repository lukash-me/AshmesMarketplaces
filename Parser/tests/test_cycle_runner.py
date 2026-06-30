from __future__ import annotations

import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

PARSER_ROOT = Path(__file__).resolve().parents[1]
if str(PARSER_ROOT) not in sys.path:
    sys.path.insert(0, str(PARSER_ROOT))

from app import cycle_runner


class FakeCycleOutbox:
    def __init__(self, *args, **kwargs) -> None:
        self.send_calls = 0
        self.poll_calls = 0
        self.cleanup_calls = 0

    def count_by_status(self) -> dict[str, int]:
        return {"send_pending": 1} if self.send_calls == 0 else {"completed": 1}

    def list_active(self) -> list[int]:
        return [1] if self.send_calls == 0 else []

    def send_pending_once(self, *, limit: int | None = None) -> int:
        self.send_calls += 1
        return 1

    def poll_active_once(self, *, limit: int | None = None) -> int:
        self.poll_calls += 1
        return 1

    def cleanup_completed_payloads(self) -> int:
        self.cleanup_calls += 1
        return 1


class CycleRunnerTests(unittest.TestCase):
    def test_cycle_flushes_outbox_runs_pipeline_and_writes_report(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config_path = temp_dir / "cycle_config.json"
            output_dir = temp_dir / "output"
            config_path.write_text(
                json.dumps(
                    {
                        "pipeline_name": "cycle_test",
                        "output_base_dir": str(output_dir),
                        "defaults": {"fail_fast": False, "ingestion": "disabled"},
                        "modes": {
                            "batched_full_enrichment": {
                                "batching": {"batch_size": 2, "worker_id": "parser-test"},
                                "product": {"config": "Parser/presets/home_goods_demo.env"},
                            }
                        },
                    },
                    ensure_ascii=False,
                ),
                encoding="utf-8",
            )
            pipeline_dir = output_dir / "pipelines" / "cycle-pipeline"
            pipeline_dir.mkdir(parents=True)
            observed: dict[str, str | None] = {}

            def fake_run_pipeline(**kwargs):
                observed["max_batches"] = os.environ.get("PARSER_MAX_STREAM_BATCHES")
                observed["subcategory_allowlist"] = os.environ.get("PARSER_SUBCATEGORY_ALLOWLIST")
                observed["mode"] = kwargs["mode"]
                return pipeline_dir

            previous = os.environ.get("PARSER_MAX_STREAM_BATCHES")
            previous_allowlist = os.environ.get("PARSER_SUBCATEGORY_ALLOWLIST")
            os.environ["PARSER_BATCH_QUEUE_URL"] = "http://api.local/api/v1/parser"
            os.environ["PARSER_OUTBOX_DIR"] = str(temp_dir / "outbox")
            os.environ["PARSER_INSTANCE_ID"] = "parser-test"
            try:
                with patch.object(cycle_runner, "DurableBatchOutbox", FakeCycleOutbox), patch.object(
                    cycle_runner,
                    "run_pipeline",
                    side_effect=fake_run_pipeline,
                ), patch.object(
                    cycle_runner,
                    "_resolve_staging_preflight",
                    return_value=SimpleNamespace(requested=False, connection_string=None, connection_string_source="missing"),
                ):
                    result = cycle_runner.run_cycle(
                        config_path=config_path,
                        mode="batched_full_enrichment",
                        stage_to_db=False,
                        connection_string=None,
                        smoke_max_batches=1,
                        smoke_source_subcategory="Коврики для ванной",
                        outbox_flush_passes=2,
                    )
            finally:
                os.environ.pop("PARSER_BATCH_QUEUE_URL", None)
                os.environ.pop("PARSER_OUTBOX_DIR", None)
                os.environ.pop("PARSER_INSTANCE_ID", None)
                if previous is None:
                    os.environ.pop("PARSER_MAX_STREAM_BATCHES", None)
                else:
                    os.environ["PARSER_MAX_STREAM_BATCHES"] = previous
                if previous_allowlist is None:
                    os.environ.pop("PARSER_SUBCATEGORY_ALLOWLIST", None)
                else:
                    os.environ["PARSER_SUBCATEGORY_ALLOWLIST"] = previous_allowlist

            reports = list((output_dir / "cycle_reports").glob("parser_cycle_*.json"))
            report = json.loads(reports[0].read_text(encoding="utf-8"))

        self.assertEqual(result, pipeline_dir)
        self.assertEqual(observed["max_batches"], "1")
        self.assertEqual(observed["subcategory_allowlist"], "Коврики для ванной")
        self.assertEqual(observed["mode"], "batched_full_enrichment")
        self.assertEqual(os.environ.get("PARSER_MAX_STREAM_BATCHES"), previous)
        self.assertEqual(os.environ.get("PARSER_SUBCATEGORY_ALLOWLIST"), previous_allowlist)
        self.assertEqual(report["status"], "succeeded")
        self.assertEqual(report["pipelineRunDir"], str(pipeline_dir))
        self.assertEqual(report["preflightOutbox"]["send_attempts"], 2)
        self.assertEqual(report["finalOutbox"]["cleaned_payloads"], 1)


if __name__ == "__main__":
    unittest.main()
