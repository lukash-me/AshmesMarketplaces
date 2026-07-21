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
    last_server_base_url: str | None = None

    def __init__(self, *args, **kwargs) -> None:
        FakeCycleOutbox.last_server_base_url = kwargs.get("server_base_url")
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
                                "product": {
                                    "config": "Parser/presets/home_goods_demo.env",
                                    "env": {"PARSER_BATCH_QUEUE_URL": "http://localhost:5019/api/v1/parser"},
                                },
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
        self.assertEqual(FakeCycleOutbox.last_server_base_url, "http://api.local/api/v1/parser")

    def test_cycle_fails_when_pipeline_manifest_failed(self) -> None:
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
                                "batching": {"batch_size": 100, "worker_id": "parser-test"},
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
            (pipeline_dir / "pipeline_manifest.json").write_text(
                json.dumps({"status": "failed"}, ensure_ascii=False),
                encoding="utf-8",
            )

            def fake_run_pipeline(**kwargs):
                return pipeline_dir

            with patch.object(cycle_runner, "run_pipeline", side_effect=fake_run_pipeline), patch.object(
                cycle_runner,
                "_resolve_staging_preflight",
                return_value=SimpleNamespace(requested=False, connection_string=None, connection_string_source="missing"),
            ):
                with self.assertRaisesRegex(RuntimeError, "Pipeline finished with status failed"):
                    cycle_runner.run_cycle(
                        config_path=config_path,
                        mode="batched_full_enrichment",
                        stage_to_db=False,
                        connection_string=None,
                    )

            reports = list((output_dir / "cycle_reports").glob("parser_cycle_*.json"))
            report = json.loads(reports[0].read_text(encoding="utf-8"))

        self.assertEqual(report["status"], "failed")
        self.assertEqual(report["pipelineRunDir"], str(pipeline_dir))

    def test_cycle_sets_only_proxy_for_child_process_scope(self) -> None:
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
                                "batching": {"batch_size": 100, "worker_id": "parser-test"},
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
                observed["only_proxy"] = os.environ.get("PARSER_ONLY_PROXY")
                return pipeline_dir

            previous = os.environ.get("PARSER_ONLY_PROXY")
            try:
                with patch.object(cycle_runner, "run_pipeline", side_effect=fake_run_pipeline), patch.object(
                    cycle_runner,
                    "_resolve_staging_preflight",
                    return_value=SimpleNamespace(requested=False, connection_string=None, connection_string_source="missing"),
                ):
                    cycle_runner.run_cycle(
                        config_path=config_path,
                        mode="batched_full_enrichment",
                        stage_to_db=False,
                        connection_string=None,
                        only_proxy="proxy-2",
                    )
            finally:
                if previous is None:
                    os.environ.pop("PARSER_ONLY_PROXY", None)
                else:
                    os.environ["PARSER_ONLY_PROXY"] = previous

        self.assertEqual(observed["only_proxy"], "proxy-2")
        self.assertEqual(os.environ.get("PARSER_ONLY_PROXY"), previous)

    def test_cycle_applies_product_env_before_outbox_creation(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config_path = temp_dir / "cycle_config.json"
            output_dir = temp_dir / "output"
            configured_outbox = temp_dir / "configured-outbox"
            config_path.write_text(
                json.dumps(
                    {
                        "pipeline_name": "cycle_test",
                        "output_base_dir": str(output_dir),
                        "defaults": {"fail_fast": False, "ingestion": "disabled"},
                        "modes": {
                            "batched_full_enrichment": {
                                "batching": {"batch_size": 100, "worker_id": "parser-test"},
                                "product": {
                                    "config": "Parser/presets/home_goods_demo.env",
                                    "env": {
                                        "PARSER_BATCH_QUEUE_URL": "http://api.local/api/v1/parser",
                                        "PARSER_OUTBOX_DIR": str(configured_outbox),
                                    },
                                },
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

            class ObservingOutbox(FakeCycleOutbox):
                def __init__(self, *args, **kwargs) -> None:
                    observed["root_dir"] = str(kwargs["root_dir"])
                    observed["server_base_url"] = kwargs["server_base_url"]
                    super().__init__(*args, **kwargs)

            def fake_run_pipeline(**kwargs):
                return pipeline_dir

            previous_queue_url = os.environ.get("PARSER_BATCH_QUEUE_URL")
            previous_outbox_dir = os.environ.get("PARSER_OUTBOX_DIR")
            try:
                os.environ.pop("PARSER_BATCH_QUEUE_URL", None)
                os.environ.pop("PARSER_OUTBOX_DIR", None)
                with patch.object(cycle_runner, "DurableBatchOutbox", ObservingOutbox), patch.object(
                    cycle_runner,
                    "run_pipeline",
                    side_effect=fake_run_pipeline,
                ), patch.object(
                    cycle_runner,
                    "_resolve_staging_preflight",
                    return_value=SimpleNamespace(requested=False, connection_string=None, connection_string_source="missing"),
                ):
                    cycle_runner.run_cycle(
                        config_path=config_path,
                        mode="batched_full_enrichment",
                        stage_to_db=False,
                        connection_string=None,
                    )
            finally:
                if previous_queue_url is None:
                    os.environ.pop("PARSER_BATCH_QUEUE_URL", None)
                else:
                    os.environ["PARSER_BATCH_QUEUE_URL"] = previous_queue_url
                if previous_outbox_dir is None:
                    os.environ.pop("PARSER_OUTBOX_DIR", None)
                else:
                    os.environ["PARSER_OUTBOX_DIR"] = previous_outbox_dir

        self.assertEqual(observed["root_dir"], str(configured_outbox))
        self.assertEqual(observed["server_base_url"], "http://api.local/api/v1/parser")
        self.assertEqual(os.environ.get("PARSER_BATCH_QUEUE_URL"), previous_queue_url)
        self.assertEqual(os.environ.get("PARSER_OUTBOX_DIR"), previous_outbox_dir)

    def test_cycle_preserves_runtime_service_env_over_product_preset(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            temp_dir = Path(temp)
            config_path = temp_dir / "cycle_config.json"
            output_dir = temp_dir / "output"
            static_mapping = temp_dir / "static_proxy_mapping.json"
            static_niches = temp_dir / "static_niches.json"
            static_rank = temp_dir / "static_rank.json"
            runtime_mapping = temp_dir / "runtime_proxy_mapping.json"
            runtime_niches = temp_dir / "runtime_niches.json"
            runtime_rank = temp_dir / "runtime_rank.json"
            config_path.write_text(
                json.dumps(
                    {
                        "pipeline_name": "cycle_test",
                        "output_base_dir": str(output_dir),
                        "defaults": {"fail_fast": False, "ingestion": "disabled"},
                        "modes": {
                            "batched_full_enrichment": {
                                "batching": {"batch_size": 100, "worker_id": "parser-test"},
                                "product": {
                                    "config": "Parser/presets/home_goods_demo.env",
                                    "env": {
                                        "PARSER_PROXY_MAPPING_FILE": str(static_mapping),
                                        "PARSER_EXPLICIT_NICHES_FILE": str(static_niches),
                                        "PARSER_RUNTIME_RANK_CONFIG_FILE": str(static_rank),
                                    },
                                },
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
                observed["mapping"] = os.environ.get("PARSER_PROXY_MAPPING_FILE")
                observed["niches"] = os.environ.get("PARSER_EXPLICIT_NICHES_FILE")
                observed["rank"] = os.environ.get("PARSER_RUNTIME_RANK_CONFIG_FILE")
                return pipeline_dir

            previous = {
                key: os.environ.get(key)
                for key in (
                    "PARSER_PROXY_MAPPING_FILE",
                    "PARSER_EXPLICIT_NICHES_FILE",
                    "PARSER_RUNTIME_RANK_CONFIG_FILE",
                )
            }
            try:
                os.environ["PARSER_PROXY_MAPPING_FILE"] = str(runtime_mapping)
                os.environ["PARSER_EXPLICIT_NICHES_FILE"] = str(runtime_niches)
                os.environ["PARSER_RUNTIME_RANK_CONFIG_FILE"] = str(runtime_rank)
                with patch.object(cycle_runner, "run_pipeline", side_effect=fake_run_pipeline), patch.object(
                    cycle_runner,
                    "_resolve_staging_preflight",
                    return_value=SimpleNamespace(requested=False, connection_string=None, connection_string_source="missing"),
                ):
                    cycle_runner.run_cycle(
                        config_path=config_path,
                        mode="batched_full_enrichment",
                        stage_to_db=False,
                        connection_string=None,
                    )
            finally:
                for key, value in previous.items():
                    if value is None:
                        os.environ.pop(key, None)
                    else:
                        os.environ[key] = value

        self.assertEqual(observed["mapping"], str(runtime_mapping))
        self.assertEqual(observed["niches"], str(runtime_niches))
        self.assertEqual(observed["rank"], str(runtime_rank))


if __name__ == "__main__":
    unittest.main()
