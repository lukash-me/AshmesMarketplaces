from __future__ import annotations

import argparse
import json
import os
import platform
import queue
import subprocess
import sys
import threading
import time
from dataclasses import asdict, dataclass, field, replace
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from typing import Any, Callable

from config import BASE_DIR
from manifest import get_git_commit, utc_now_iso


STEP_ORDER = ["rank", "products", "logistics", "reviews", "product_details"]
TERMINAL_SUCCESS = {"succeeded", "skipped"}
RETRYABLE_STATUSES = {"failed", "interrupted", "skipped"}
STAGE_ENV_ENABLED_VALUES = {"1", "true", "yes", "on"}
STAGE_ENV_DISABLED_VALUES = {"", "0", "false", "no", "off"}
INGESTION_CLI_PROJECT = Path("Backend") / "AshmesMarketplaces.ParserIngestionCli" / "AshmesMarketplaces.ParserIngestionCli.csproj"


def _make_pipeline_run_id() -> str:
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    commit = get_git_commit(BASE_DIR.parent)
    return f"market_refresh_{timestamp}_{commit}"


def _repo_root() -> Path:
    return BASE_DIR.parent


def _python_executable() -> str:
    return sys.executable


def _to_abs(path_value: str | Path, *, base_dir: Path) -> Path:
    path = Path(path_value)
    return path if path.is_absolute() else base_dir / path


def _redact_text(value: str | None, secrets: list[str] | tuple[str, ...]) -> str:
    if value is None:
        return ""

    redacted = str(value)
    for secret in secrets:
        if secret:
            redacted = redacted.replace(secret, "<redacted>")
    return redacted


def _sanitize_command(command: list[str], secrets: list[str] | tuple[str, ...]) -> list[str]:
    sanitized: list[str] = []
    skip_next_connection_value = False
    for part in command:
        if skip_next_connection_value:
            sanitized.append("<connection-string>")
            skip_next_connection_value = False
            continue

        sanitized_part = _redact_text(part, secrets)
        sanitized.append(sanitized_part)
        if part == "--connection-string":
            skip_next_connection_value = True
    return sanitized


def _test_scope_env(*, pipeline_run_id: str, test_run: bool, test_label: str | None) -> dict[str, str]:
    if not test_run:
        return {}

    env = {
        "PARSER_IS_TEST_RUN": "true",
        "PARSER_PIPELINE_RUN_ID": pipeline_run_id,
        "PARSER_RUN_PURPOSE": "parser_testing",
    }
    if test_label and test_label.strip():
        env["PARSER_TEST_LABEL"] = test_label.strip()
    return env


def _with_env_overrides(plan: PipelineStepPlan, extra_env: dict[str, str]) -> PipelineStepPlan:
    if not extra_env:
        return plan

    return replace(plan, env_overrides={**plan.env_overrides, **extra_env})


def _command_display(command: list[str], secrets: list[str] | tuple[str, ...] = ()) -> str:
    return " ".join(_sanitize_command(command, secrets))


def _parse_stage_to_db_env(value: str | None) -> bool:
    if value is None:
        return False

    normalized = value.strip().lower()
    if normalized in STAGE_ENV_ENABLED_VALUES:
        return True
    if normalized in STAGE_ENV_DISABLED_VALUES:
        return False

    allowed = ", ".join(sorted(STAGE_ENV_ENABLED_VALUES | STAGE_ENV_DISABLED_VALUES))
    raise ValueError(f"PARSER_STAGE_TO_DB has unsupported value '{value}'. Allowed values: {allowed}.")


def _resolve_connection_string(
    *,
    argument_value: str | None,
    environ: dict[str, str] | os._Environ[str] = os.environ,
) -> tuple[str | None, str]:
    if argument_value and argument_value.strip():
        return argument_value, "argument"

    for key in ("ConnectionStrings__Postgres", "ASHMES_POSTGRES_CONNECTION"):
        value = environ.get(key)
        if value and value.strip():
            return value, key

    return None, "missing"


def _resolve_staging_preflight(
    *,
    stage_to_db_arg: bool,
    connection_string_arg: str | None,
    environ: dict[str, str] | os._Environ[str] = os.environ,
) -> StagingPreflight:
    requested = bool(stage_to_db_arg) or _parse_stage_to_db_env(environ.get("PARSER_STAGE_TO_DB"))
    connection_string, source = _resolve_connection_string(
        argument_value=connection_string_arg,
        environ=environ,
    )
    if requested and not connection_string:
        raise ValueError(
            "Staging was requested, but no PostgreSQL connection string was available. "
            "Pass --connection-string or set ConnectionStrings__Postgres or ASHMES_POSTGRES_CONNECTION."
        )
    return StagingPreflight(
        requested=requested,
        connection_string=connection_string if requested else None,
        connection_string_source=source if requested else "missing",
    )


@dataclass(frozen=True)
class PipelineStepPlan:
    step_name: str
    commands: list[list[str]]
    env_overrides: dict[str, str] = field(default_factory=dict)
    enabled: bool = True
    timeout_seconds: int | None = None
    preflight_error: str | None = None


@dataclass
class PipelineStepRecord:
    step_name: str
    status: str = "pending"
    command: list[list[str]] = field(default_factory=list)
    cwd: str = ""
    env_overrides: dict[str, str] = field(default_factory=dict)
    started_at_utc: str | None = None
    finished_at_utc: str | None = None
    exit_code: int | None = None
    output_run_dirs: list[str] = field(default_factory=list)
    child_manifest_summaries: list[dict[str, Any]] = field(default_factory=list)
    error_summary: str | None = None


@dataclass
class CommandResult:
    exit_code: int
    stdout: str = ""
    stderr: str = ""
    timed_out: bool = False


@dataclass(frozen=True)
class StagingPreflight:
    requested: bool
    connection_string: str | None
    connection_string_source: str


@dataclass(frozen=True)
class ProductBatchRun:
    batch_id: str
    batch_index: int
    size: int
    batch_dir: Path
    product_run_dir: Path


def _console(message: str) -> None:
    print(f"[market-refresh] {message}", flush=True)


class PipelineConfig:
    def __init__(self, *, path: Path, payload: dict[str, Any]) -> None:
        self.path = path
        self.payload = payload
        self.pipeline_name = str(payload.get("pipeline_name") or "market_refresh")
        self.output_base_dir = _to_abs(
            payload.get("output_base_dir") or BASE_DIR / "output",
            base_dir=_repo_root(),
        )
        self.defaults = dict(payload.get("defaults") or {})
        self.modes = dict(payload.get("modes") or {})
        if not self.modes:
            raise ValueError("Pipeline config must include modes.")

    @classmethod
    def load(cls, path: Path) -> "PipelineConfig":
        if not path.exists():
            raise FileNotFoundError(f"Pipeline config was not found: {path}")
        return cls(path=path, payload=json.loads(path.read_text(encoding="utf-8")))

    def mode_config(self, mode: str) -> dict[str, Any]:
        if mode not in self.modes:
            raise ValueError(f"Pipeline mode was not found in config: {mode}")
        return dict(self.modes[mode])

    def safe_snapshot(self) -> dict[str, Any]:
        return self.payload


def _run_dir_from_manifest_text(text: str) -> Path | None:
    markers = [
        "Rank run directory:",
        "Logistics run directory:",
        "Review run directory:",
        "Product details run directory:",
        "Run directory:",
    ]
    for line in reversed(text.splitlines()):
        for marker in markers:
            if marker in line:
                candidate = line.split(marker, 1)[1].strip()
                return Path(candidate) if candidate else None
    return None


def _manifest_summary(run_dir: Path) -> dict[str, Any]:
    manifest_path = run_dir / "manifest.json"
    if not manifest_path.exists():
        return {"run_dir": str(run_dir), "manifest_found": False}

    payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    return {
        "run_dir": str(run_dir),
        "manifest_found": True,
        "parser_run_id": payload.get("parser_run_id"),
        "run_kind": payload.get("run_kind"),
        "status": payload.get("status"),
        "started_at_utc": payload.get("started_at_utc"),
        "finished_at_utc": payload.get("finished_at_utc"),
        "row_counts": payload.get("row_counts"),
        "page_counts": payload.get("page_counts"),
        "counters": payload.get("counters"),
        "coverage": payload.get("coverage"),
        "error_counts": payload.get("error_counts"),
    }


def _child_status(run_dir: Path) -> str | None:
    summary = _manifest_summary(run_dir)
    status = summary.get("status")
    return str(status) if status is not None else None


def _count_jsonl_rows(path: Path, *, limit: int = 1) -> int:
    if not path.exists():
        return 0
    count = 0
    with path.open(encoding="utf-8") as file:
        for line in file:
            if line.strip():
                count += 1
                if count >= limit:
                    break
    return count


def _summary_has_usable_artifact(step_name: str, summary: dict[str, Any]) -> bool:
    run_dir_value = summary.get("run_dir")
    run_dir = Path(run_dir_value) if run_dir_value else None
    row_counts = summary.get("row_counts") or {}
    counters = summary.get("counters") or {}

    if step_name == "products":
        unique_rows = row_counts.get("unique_rows") or row_counts.get("rows_written")
        if isinstance(unique_rows, int) and unique_rows > 0:
            return True
        return bool(run_dir and _count_jsonl_rows(run_dir / "products.jsonl") > 0)

    if step_name == "rank":
        rank_rows = row_counts.get("rank_rows_written") or row_counts.get("rank_rows")
        return isinstance(rank_rows, int) and rank_rows > 0

    if step_name == "logistics":
        return any(
            int(counters.get(name) or 0) > 0
            for name in ("products_succeeded", "snapshot_rows_written", "warehouse_rows_written")
        )

    if step_name == "reviews":
        return any(
            int(counters.get(name) or 0) > 0
            for name in ("roots_processed", "roots_succeeded", "roots_empty", "reviews_written", "replies_written")
        )

    if step_name == "product_details":
        return any(
            int(counters.get(name) or 0) > 0
            for name in ("products_attempted", "products_succeeded", "products_empty", "detail_rows_written")
        )

    return False


def _record_has_usable_artifact(record: dict[str, Any]) -> bool:
    step_name = str(record.get("step_name") or "")
    return any(
        _summary_has_usable_artifact(step_name, summary)
        for summary in record.get("child_manifest_summaries") or []
    )


def _step_is_stageable(step: dict[str, Any]) -> bool:
    status = step.get("status")
    if status == "succeeded":
        return True
    return status == "partial" and bool(step.get("output_run_dirs")) and _record_has_usable_artifact(step)


def _latest_output_run_dir(before: set[Path], output_base_dir: Path) -> Path | None:
    runs_dir = output_base_dir / "runs"
    if not runs_dir.exists():
        return None
    candidates = [
        path
        for path in runs_dir.iterdir()
        if path.is_dir() and path not in before
    ]
    if not candidates:
        return None
    return max(candidates, key=lambda path: path.stat().st_mtime)


def _existing_run_dirs(output_base_dir: Path) -> set[Path]:
    runs_dir = output_base_dir / "runs"
    if not runs_dir.exists():
        return set()
    return {path for path in runs_dir.iterdir() if path.is_dir()}


def _build_rank_plan(mode_config: dict[str, Any], *, repo_root: Path, python_executable: str) -> PipelineStepPlan:
    rank = dict(mode_config.get("rank") or {})
    command = [
        python_executable,
        str(repo_root / "Parser" / "pipelines" / "ranks" / "runner.py"),
        "--config",
        str(_to_abs(rank["config"], base_dir=repo_root)),
    ]
    if rank.get("context_id"):
        command.extend(["--context-id", str(rank["context_id"])])
    if rank.get("top_n") is not None:
        command.extend(["--top-n", str(rank["top_n"])])
    if rank.get("max_pages") is not None:
        command.extend(["--max-pages", str(rank["max_pages"])])
    if rank.get("smoke_only"):
        command.append("--smoke-only")
    return PipelineStepPlan(
        step_name="rank",
        commands=[command],
        enabled=bool(rank),
        timeout_seconds=rank.get("timeout_seconds"),
    )


def _build_products_plan(mode_config: dict[str, Any], *, repo_root: Path, python_executable: str) -> PipelineStepPlan:
    product = dict(mode_config.get("product") or {})
    command = [
        python_executable,
        str(repo_root / "Parser" / "pipelines" / "products" / "runner.py"),
        "--config",
        str(_to_abs(product["config"], base_dir=repo_root)),
    ]
    if product.get("smoke_only"):
        command.append("--smoke-only")
    env_overrides = {str(key): str(value) for key, value in dict(product.get("env") or {}).items()}
    return PipelineStepPlan(
        step_name="products",
        commands=[command],
        env_overrides=env_overrides,
        enabled=bool(product),
        timeout_seconds=product.get("timeout_seconds"),
    )


def _build_logistics_plan(
    mode_config: dict[str, Any],
    *,
    repo_root: Path,
    python_executable: str,
    product_run_dir: Path | None,
    output_base_dir: Path,
) -> PipelineStepPlan:
    logistics = dict(mode_config.get("logistics") or {})
    if not logistics:
        return PipelineStepPlan(step_name="logistics", commands=[], enabled=False)
    if product_run_dir is None:
        return PipelineStepPlan(step_name="logistics", commands=[], enabled=True)

    command = [
        python_executable,
        str(repo_root / "Parser" / "pipelines" / "logistics" / "runner.py"),
        "--products-run-dir",
        str(product_run_dir),
        "--output-dir",
        str(output_base_dir),
        "--marketplace",
        str(logistics.get("marketplace") or "wb"),
    ]
    if logistics.get("dest") is not None:
        command.extend(["--dest", str(logistics["dest"])])
    if logistics.get("delivery_profile") is not None:
        command.extend(["--delivery-profile", str(logistics["delivery_profile"])])
    if logistics.get("delivery_profile_config") is not None:
        command.extend([
            "--delivery-profile-config",
            str(_to_abs(logistics["delivery_profile_config"], base_dir=repo_root)),
        ])
    for destination in logistics.get("delivery_destinations") or []:
        if isinstance(destination, dict):
            key = str(destination.get("key") or destination.get("dest") or "").strip()
            name = str(destination.get("name") or key).strip()
            dest = str(destination.get("dest") or "").strip()
            if key and name and dest:
                command.extend(["--delivery-destination", f"{key}|{name}|{dest}"])
        else:
            command.extend(["--delivery-destination", str(destination)])
    if logistics.get("limit_products") is not None:
        command.extend(["--limit", str(logistics["limit_products"])])
    if logistics.get("delay_ms") is not None:
        command.extend(["--delay-ms", str(logistics["delay_ms"])])
    if logistics.get("timeout_sec") is not None:
        command.extend(["--timeout-sec", str(logistics["timeout_sec"])])
    if logistics.get("retries") is not None:
        command.extend(["--retries", str(logistics["retries"])])
    if logistics.get("max_concurrent") is not None:
        command.extend(["--max-concurrent", str(logistics["max_concurrent"])])
    if logistics.get("checkpoint_interval") is not None:
        command.extend(["--checkpoint-interval", str(logistics["checkpoint_interval"])])
    if logistics.get("retry_queue_passes") is not None:
        command.extend(["--retry-queue-passes", str(logistics["retry_queue_passes"])])
    if logistics.get("adaptive_delay"):
        command.append("--adaptive-delay")

    for product_id in logistics.get("product_ids") or []:
        command.extend(["--product-id", str(product_id)])

    return PipelineStepPlan(
        step_name="logistics",
        commands=[command],
        enabled=bool(logistics.get("enabled", True)),
        timeout_seconds=logistics.get("timeout_seconds"),
    )


def _available_source_subcategories(product_run_dir: Path) -> set[str]:
    products_jsonl = product_run_dir / "products.jsonl"
    if not products_jsonl.exists():
        return set()

    values: set[str] = set()
    with products_jsonl.open(encoding="utf-8") as file:
        for line in file:
            if not line.strip():
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError:
                continue
            value = row.get("source_subcategory")
            if isinstance(value, str) and value.strip():
                values.add(value.strip())
    return values


def _review_scope_preflight_error(product_run_dir: Path, source_subcategories: list[Any]) -> str | None:
    requested = [str(value).strip() for value in source_subcategories if value]
    if not requested:
        return None
    if not (product_run_dir / "products.jsonl").exists():
        return None

    available = _available_source_subcategories(product_run_dir)
    if not available:
        return f"Review scope preflight failed: no source_subcategory values found in {product_run_dir / 'products.jsonl'}."

    missing = [value for value in requested if value not in available]
    if not missing:
        return None

    sample_available = ", ".join(sorted(available)[:10])
    return (
        "Review scope preflight failed: configured source_subcategories are absent from products.jsonl. "
        f"missing={missing}; available_sample=[{sample_available}]"
    )


def _build_reviews_plan(
    mode_config: dict[str, Any],
    *,
    repo_root: Path,
    python_executable: str,
    product_run_dir: Path | None,
) -> PipelineStepPlan:
    reviews = dict(mode_config.get("reviews") or {})
    if not reviews:
        return PipelineStepPlan(step_name="reviews", commands=[], enabled=False)
    if product_run_dir is None:
        return PipelineStepPlan(step_name="reviews", commands=[], enabled=True)

    source_subcategories = reviews.get("source_subcategories") or [None]
    preflight_error = _review_scope_preflight_error(product_run_dir, source_subcategories)
    commands: list[list[str]] = []
    for source_subcategory in source_subcategories:
        command = [
            python_executable,
            str(repo_root / "Parser" / "pipelines" / "reviews" / "runner.py"),
            "--config",
            str(_to_abs(reviews.get("config") or "Parser/.env", base_dir=repo_root)),
            "--products-run-dir",
            str(product_run_dir),
        ]
        if reviews.get("limit_products") is not None:
            command.extend(["--limit-products", str(reviews["limit_products"])])
        if reviews.get("max_concurrent") is not None:
            command.extend(["--max-concurrent", str(reviews["max_concurrent"])])
        if source_subcategory:
            command.extend(["--source-subcategory", str(source_subcategory)])
        if reviews.get("smoke_only"):
            command.append("--smoke-only")
        if reviews.get("fail_fast"):
            command.append("--fail-fast")
        if reviews.get("retain_raw_payloads") is False:
            command.append("--no-retain-raw-payloads")
        elif reviews.get("retain_raw_payloads") is True:
            command.append("--retain-raw-payloads")
        commands.append(command)

    return PipelineStepPlan(
        step_name="reviews",
        commands=commands,
        enabled=True,
        timeout_seconds=reviews.get("timeout_seconds"),
        preflight_error=preflight_error,
    )


def _build_product_details_plan(
    mode_config: dict[str, Any],
    *,
    repo_root: Path,
    python_executable: str,
    product_run_dir: Path | None,
    output_base_dir: Path,
) -> PipelineStepPlan:
    details = dict(mode_config.get("product_details") or {})
    if not details:
        return PipelineStepPlan(step_name="product_details", commands=[], enabled=False)
    if product_run_dir is None:
        return PipelineStepPlan(step_name="product_details", commands=[], enabled=True)

    command = [
        python_executable,
        str(repo_root / "Parser" / "pipelines" / "details" / "runner.py"),
        "--products-run-dir",
        str(product_run_dir),
        "--output-dir",
        str(output_base_dir),
        "--marketplace",
        str(details.get("marketplace") or "wb"),
    ]
    if details.get("limit_products") is not None:
        command.extend(["--limit", str(details["limit_products"])])
    if details.get("delay_ms") is not None:
        command.extend(["--delay-ms", str(details["delay_ms"])])
    if details.get("timeout_sec") is not None:
        command.extend(["--timeout-sec", str(details["timeout_sec"])])
    if details.get("retries") is not None:
        command.extend(["--retries", str(details["retries"])])
    if details.get("max_concurrent") is not None:
        command.extend(["--max-concurrent", str(details["max_concurrent"])])
    if details.get("checkpoint_interval") is not None:
        command.extend(["--checkpoint-interval", str(details["checkpoint_interval"])])
    if details.get("retry_queue_passes") is not None:
        command.extend(["--retry-queue-passes", str(details["retry_queue_passes"])])
    if details.get("adaptive_delay"):
        command.append("--adaptive-delay")

    for product_id in details.get("product_ids") or []:
        command.extend(["--product-id", str(product_id)])

    return PipelineStepPlan(
        step_name="product_details",
        commands=[command],
        enabled=bool(details.get("enabled", True)),
        timeout_seconds=details.get("timeout_seconds"),
    )


def _load_manifest(run_dir: Path) -> dict[str, Any]:
    return json.loads((run_dir / "pipeline_manifest.json").read_text(encoding="utf-8"))


def _write_manifest(run_dir: Path, payload: dict[str, Any]) -> None:
    (run_dir / "pipeline_manifest.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, default=str),
        encoding="utf-8",
    )


def _record_for(manifest: dict[str, Any], step_name: str) -> dict[str, Any]:
    for step in manifest["steps"]:
        if step["step_name"] == step_name:
            return step
    record = asdict(PipelineStepRecord(step_name=step_name))
    manifest["steps"].append(record)
    return record


def _step_succeeded(manifest: dict[str, Any], step_name: str) -> bool:
    record = _record_for(manifest, step_name)
    return record.get("status") == "succeeded"


def _useful_artifact_count(manifest: dict[str, Any]) -> int:
    return sum(
        1
        for step in manifest["steps"]
        if _step_is_stageable(step) and step.get("output_run_dirs")
    )


def _suggested_ingestion_commands(manifest: dict[str, Any]) -> list[list[str]]:
    commands: list[list[str]] = []
    for step in manifest["steps"]:
        if step.get("step_name") == "rank" and _step_is_stageable(step):
            for run_dir in step.get("output_run_dirs") or []:
                commands.append([
                    "dotnet",
                    "run",
                    "--project",
                    str(INGESTION_CLI_PROJECT),
                    "--",
                    "stage-ranks",
                    run_dir,
                    "--connection-string",
                    "<connection-string>",
                ])
        if step.get("step_name") == "products" and _step_is_stageable(step):
            for run_dir in step.get("output_run_dirs") or []:
                commands.append([
                    "dotnet",
                    "run",
                    "--project",
                    str(INGESTION_CLI_PROJECT),
                    "--",
                    "stage-products",
                    run_dir,
                    "--connection-string",
                    "<connection-string>",
                ])
        if step.get("step_name") == "logistics" and _step_is_stageable(step):
            for run_dir in step.get("output_run_dirs") or []:
                commands.append([
                    "dotnet",
                    "run",
                    "--project",
                    str(INGESTION_CLI_PROJECT),
                    "--",
                    "stage-logistics",
                    run_dir,
                    "--connection-string",
                    "<connection-string>",
                ])
        if step.get("step_name") == "reviews" and _step_is_stageable(step):
            for run_dir in step.get("output_run_dirs") or []:
                commands.append([
                    "dotnet",
                    "run",
                    "--project",
                    str(INGESTION_CLI_PROJECT),
                    "--",
                    "stage-reviews",
                    run_dir,
                    "--connection-string",
                    "<connection-string>",
                ])
        if step.get("step_name") == "product_details" and _step_is_stageable(step):
            for run_dir in step.get("output_run_dirs") or []:
                commands.append([
                    "dotnet",
                    "run",
                    "--project",
                    str(INGESTION_CLI_PROJECT),
                    "--",
                    "stage-product-details",
                    run_dir,
                    "--connection-string",
                    "<connection-string>",
                ])
    return commands


def _read_jsonl(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    if not path.exists():
        raise FileNotFoundError(f"JSONL file was not found: {path}")

    with path.open(encoding="utf-8") as file:
        for line in file:
            if not line.strip():
                continue
            rows.append(json.loads(line))
    return rows


def _write_jsonl(path: Path, rows: list[dict[str, Any]]) -> None:
    path.write_text(
        "".join(json.dumps(row, ensure_ascii=False, default=str) + "\n" for row in rows),
        encoding="utf-8",
    )


def _chunks(rows: list[dict[str, Any]], size: int) -> list[list[dict[str, Any]]]:
    if size < 1:
        raise ValueError("Batch size must be positive.")
    return [rows[index : index + size] for index in range(0, len(rows), size)]


def _batch_requested_scope(
    parent_scope: dict[str, Any],
    *,
    pipeline_run_id: str,
    batch_id: str,
    batch_index: int,
    worker_id: str,
    shard_key: str,
    source_niche: str | None,
) -> dict[str, Any]:
    scope = dict(parent_scope)
    scope.update(
        {
            "pipeline_run_id": pipeline_run_id,
            "batch_id": batch_id,
            "batch_index": batch_index,
            "worker_id": worker_id,
            "shard_key": shard_key,
            "is_complete_card_batch": True,
        }
    )
    if source_niche:
        scope["source_niche"] = source_niche
    return scope


def _create_product_batch_run_dirs(
    *,
    product_run_dir: Path,
    batches_dir: Path,
    pipeline_run_id: str,
    batch_size: int,
    worker_id: str,
    shard_key: str,
) -> list[ProductBatchRun]:
    manifest_path = product_run_dir / "manifest.json"
    if not manifest_path.exists():
        raise FileNotFoundError(f"Product manifest was not found: {manifest_path}")

    parent_manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    rows = _read_jsonl(product_run_dir / "products.jsonl")
    batches: list[ProductBatchRun] = []
    batches_dir.mkdir(parents=True, exist_ok=True)

    for batch_index, batch_rows in enumerate(_chunks(rows, batch_size), start=1):
        batches.append(
            _create_product_batch_run_dir_from_rows(
                parent_manifest=parent_manifest,
                batch_rows=batch_rows,
                source_product_run_dir=product_run_dir,
                batches_dir=batches_dir,
                pipeline_run_id=pipeline_run_id,
                batch_index=batch_index,
                worker_id=worker_id,
                shard_key=shard_key,
            )
        )

    return batches


def _create_product_batch_run_dir_from_rows(
    *,
    parent_manifest: dict[str, Any],
    batch_rows: list[dict[str, Any]],
    source_product_run_dir: Path,
    batches_dir: Path,
    pipeline_run_id: str,
    batch_index: int,
    worker_id: str,
    shard_key: str,
) -> ProductBatchRun:
    parent_scope = dict(parent_manifest.get("requested_scope") or {})
    marketplace = str(parent_manifest.get("marketplace") or "wildberries")
    source_region_dest = str(parent_manifest.get("source_region_dest") or "")
    parser_version = str(parent_manifest.get("parser_version") or get_git_commit(_repo_root()))
    config_snapshot = parent_manifest.get("config_snapshot") or {}

    batch_id = f"{pipeline_run_id}:batch:{batch_index:04d}"
    product_parser_run_id = f"{pipeline_run_id}_batch_{batch_index:04d}_products"
    batch_dir = batches_dir / f"batch_{batch_index:04d}"
    product_batch_dir = batch_dir / "products"
    product_batch_dir.mkdir(parents=True, exist_ok=True)
    batch_created_at_utc = utc_now_iso()

    source_niches = sorted(
        {
            str(row.get("source_subcategory") or "").strip()
            for row in batch_rows
            if str(row.get("source_subcategory") or "").strip()
        }
    )
    source_niche = source_niches[0] if len(source_niches) == 1 else None
    rewritten_rows: list[dict[str, Any]] = []
    for row in batch_rows:
        rewritten = dict(row)
        rewritten["parser_run_id"] = product_parser_run_id
        rewritten["parsed_at_utc"] = batch_created_at_utc
        rewritten_rows.append(rewritten)

    _write_jsonl(product_batch_dir / "products.jsonl", rewritten_rows)
    (product_batch_dir / "errors.jsonl").touch()
    (product_batch_dir / "runner.log").write_text(
        f"Batch product artifact generated from {source_product_run_dir}\n",
        encoding="utf-8",
    )

    batch_manifest = {
        "schema_version": int(parent_manifest.get("schema_version") or 1),
        "parser_run_id": product_parser_run_id,
        "started_at_utc": batch_created_at_utc,
        "finished_at_utc": batch_created_at_utc,
        "status": "succeeded",
        "marketplace": marketplace,
        "requested_scope": _batch_requested_scope(
            parent_scope,
            pipeline_run_id=pipeline_run_id,
            batch_id=batch_id,
            batch_index=batch_index,
            worker_id=worker_id,
            shard_key=shard_key,
            source_niche=source_niche,
        ),
        "source_region_dest": source_region_dest,
        "output_files": {
            "manifest": str(product_batch_dir / "manifest.json"),
            "products_jsonl": str(product_batch_dir / "products.jsonl"),
            "errors_jsonl": str(product_batch_dir / "errors.jsonl"),
            "runner_log": str(product_batch_dir / "runner.log"),
        },
        "row_counts": {
            "total_rows": len(batch_rows),
            "unique_rows": len(batch_rows),
            "duplicate_rows": 0,
        },
        "category_results": [],
        "error_counts": {},
        "warning_counts": {},
        "wb_error_codes_observed": {},
        "retry_summary": {"attempts": 0, "retries": 0},
        "backoff_summary": {"count": 0, "seconds_total": 0.0},
        "token_acquisition_status": {"status": "not_attempted"},
        "network_check_result": {"status": "inherited_from_parent_run"},
        "parser_version": parser_version,
        "config_snapshot": config_snapshot,
    }
    (product_batch_dir / "manifest.json").write_text(
        json.dumps(batch_manifest, ensure_ascii=False, indent=2, default=str),
        encoding="utf-8",
    )
    return ProductBatchRun(
        batch_id=batch_id,
        batch_index=batch_index,
        size=len(batch_rows),
        batch_dir=batch_dir,
        product_run_dir=product_batch_dir,
    )


def _complete_batch_staging_command(*, batch_dir: Path, connection_string: str) -> list[str]:
    return [
        "dotnet",
        "run",
        "--project",
        str(INGESTION_CLI_PROJECT),
        "--",
        "stage-complete-batch",
        str(batch_dir),
        "--connection-string",
        connection_string,
    ]


def _complete_pipeline_command(*, pipeline_run_id: str, connection_string: str) -> list[str]:
    return [
        "dotnet",
        "run",
        "--project",
        str(INGESTION_CLI_PROJECT),
        "--",
        "complete-parser-pipeline",
        pipeline_run_id,
        "--connection-string",
        connection_string,
    ]


def _batch_scope_env(
    *,
    pipeline_run_id: str,
    batch: ProductBatchRun,
    worker_id: str,
    shard_key: str,
    source_niche: str | None = None,
) -> dict[str, str]:
    env = {
        "PARSER_PIPELINE_RUN_ID": pipeline_run_id,
        "PARSER_BATCH_ID": batch.batch_id,
        "PARSER_BATCH_INDEX": str(batch.batch_index),
        "PARSER_WORKER_ID": worker_id,
        "PARSER_SHARD_KEY": shard_key,
        "PARSER_IS_COMPLETE_CARD_BATCH": "true",
    }
    if source_niche:
        env["PARSER_SOURCE_NICHE"] = source_niche
    return env


def _is_batched_full_enrichment(mode: str, mode_config: dict[str, Any]) -> bool:
    return mode == "batched_full_enrichment" or bool(mode_config.get("batching"))


def _write_batch_manifest(batch: ProductBatchRun, payload: dict[str, Any]) -> None:
    (batch.batch_dir / "batch_manifest.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, default=str),
        encoding="utf-8",
    )


def _product_run_source_subcategories(product_run_dir: Path) -> list[str]:
    values: list[str] = []
    seen: set[str] = set()
    for row in _read_jsonl(product_run_dir / "products.jsonl"):
        value = str(row.get("source_subcategory") or "").strip()
        if value and value not in seen:
            values.append(value)
            seen.add(value)
    return values


def _product_ids_from_run(product_run_dir: Path) -> set[str]:
    return {
        str(row.get("wb_product_id"))
        for row in _read_jsonl(product_run_dir / "products.jsonl")
        if row.get("wb_product_id") is not None
    }


def _root_ids_from_run(product_run_dir: Path) -> set[str]:
    return {
        str(row.get("wb_root_id"))
        for row in _read_jsonl(product_run_dir / "products.jsonl")
        if row.get("wb_root_id") is not None
    }


def _step_output_dirs(batch_manifest: dict[str, Any], step_name: str) -> list[Path]:
    record = _record_for(batch_manifest, step_name)
    return [Path(value) for value in (record.get("output_run_dirs") or [])]


def _status_is_failed(status: Any) -> bool:
    normalized = str(status or "").strip().lower()
    return normalized.startswith("failed") or "transient" in normalized


def _validate_complete_card_batch(
    *,
    batch: ProductBatchRun,
    batch_manifest: dict[str, Any],
    require_logistics: bool,
    require_reviews: bool,
    require_product_details: bool,
) -> tuple[bool, str | None]:
    product_ids = _product_ids_from_run(batch.product_run_dir)
    root_ids = _root_ids_from_run(batch.product_run_dir)
    if not product_ids:
        return False, "products rows count is zero"

    if require_logistics:
        attempted_products: set[str] = set()
        failed_products: set[str] = set()
        for run_dir in _step_output_dirs(batch_manifest, "logistics"):
            for row in _read_jsonl(run_dir / "logistics_snapshots.jsonl"):
                product_id = row.get("wb_product_id")
                if product_id is None:
                    continue
                product_id_text = str(product_id)
                attempted_products.add(product_id_text)
                if _status_is_failed(row.get("status")):
                    failed_products.add(product_id_text)
        missing = sorted(product_ids - attempted_products)
        if missing:
            return False, f"logistics attempt missing for {len(missing)} product(s)"
        if failed_products:
            return False, f"logistics failed for {len(failed_products)} product(s)"

    if require_product_details:
        attempted_products = set()
        failed_products = set()
        for run_dir in _step_output_dirs(batch_manifest, "product_details"):
            for row in _read_jsonl(run_dir / "product_detail_fetch_results.jsonl"):
                product_id = row.get("wb_product_id")
                if product_id is None:
                    continue
                product_id_text = str(product_id)
                attempted_products.add(product_id_text)
                if _status_is_failed(row.get("status")):
                    failed_products.add(product_id_text)
        missing = sorted(product_ids - attempted_products)
        if missing:
            return False, f"product details attempt missing for {len(missing)} product(s)"
        if failed_products:
            return False, f"product details failed for {len(failed_products)} product(s)"

    if require_reviews and root_ids:
        attempted_roots: set[str] = set()
        failed_roots: set[str] = set()
        for run_dir in _step_output_dirs(batch_manifest, "reviews"):
            for row in _read_jsonl(run_dir / "review_fetch_results.jsonl"):
                root_id = row.get("source_wb_root_id")
                if root_id is None:
                    continue
                root_id_text = str(root_id)
                attempted_roots.add(root_id_text)
                if _status_is_failed(row.get("status")):
                    failed_roots.add(root_id_text)
        missing = sorted(root_ids - attempted_roots)
        if missing:
            return False, f"reviews/replies attempt missing for {len(missing)} root(s)"
        if failed_roots:
            return False, f"reviews/replies failed for {len(failed_roots)} root(s)"

    return True, None


def _resume_seen_product_ids_from_batches(manifest: dict[str, Any]) -> set[str]:
    seen: set[str] = set()
    for batch in (manifest.get("batching") or {}).get("batches") or []:
        if batch.get("status") not in {"staged", "quarantined"}:
            continue
        product_run_dir = batch.get("product_run_dir")
        if not product_run_dir:
            continue
        try:
            seen.update(_product_ids_from_run(Path(product_run_dir)))
        except Exception:
            continue
    return seen


def _run_single_staging_command(
    *,
    command: list[str],
    kind: str,
    run_dir: str,
    repo_root: Path,
    connection_string: str | None,
    executor: Callable[..., CommandResult],
) -> dict[str, Any]:
    secrets = [connection_string] if connection_string else []
    started = utc_now_iso()
    try:
        result = executor(
            command=command,
            cwd=repo_root,
            env=os.environ.copy(),
            timeout_seconds=None,
            output_callback=None,
        )
    except Exception as exception:
        result = CommandResult(exit_code=1, stderr=_redact_text(str(exception), secrets))

    stdout = _redact_text(result.stdout, secrets)
    stderr = _redact_text(result.stderr, secrets)
    parsed_json = _parse_cli_json(stdout)
    return {
        "kind": kind,
        "run_dir": run_dir,
        "command": _sanitize_command(command, secrets),
        "started_at_utc": started,
        "finished_at_utc": utc_now_iso(),
        "exit_code": result.exit_code,
        "stdout": stdout,
        "stderr": stderr,
        "parsed_json": parsed_json,
        "rows_read": _json_value(parsed_json, "RowsRead", "rowsRead"),
        "rows_written": _json_value(parsed_json, "RowsWritten", "rowsWritten"),
        "rows_skipped": _json_value(parsed_json, "RowsSkipped", "rowsSkipped"),
        "error_count": _json_value(parsed_json, "ErrorCount", "errorCount"),
        "details": _json_value(parsed_json, "Details", "details"),
    }


def _final_status(manifest: dict[str, Any], interrupted: bool = False) -> str:
    if interrupted:
        return "interrupted"
    if manifest.get("dry_run"):
        return "succeeded"

    staging = manifest.get("staging") or {}
    if staging.get("requested") and staging.get("status") == "failed":
        return "partial" if _useful_artifact_count(manifest) else "failed"

    required = [
        step
        for step in manifest["steps"]
        if step.get("status") != "skipped"
    ]
    if required and all(step.get("status") == "succeeded" for step in required):
        return "succeeded"
    if _useful_artifact_count(manifest):
        return "partial"
    return "failed"


def _execute_subprocess(
    *,
    command: list[str],
    cwd: Path,
    env: dict[str, str],
    timeout_seconds: int | None = None,
    output_callback: Callable[[str], None] | None = None,
) -> CommandResult:
    started = time.monotonic()
    output_lines: list[str] = []

    def emit(line: str) -> None:
        normalized = line.rstrip("\r\n")
        output_lines.append(normalized)
        if output_callback:
            output_callback(normalized)
        else:
            print(normalized, flush=True)

    try:
        process = subprocess.Popen(
            command,
            cwd=cwd,
            env=env,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            bufsize=1,
        )

        assert process.stdout is not None
        lines: queue.Queue[str | None] = queue.Queue()

        def read_output() -> None:
            try:
                for line in process.stdout:
                    lines.put(line)
            finally:
                lines.put(None)

        reader = threading.Thread(target=read_output, daemon=True)
        reader.start()

        timed_out = False
        while True:
            try:
                line = lines.get(timeout=0.2)
            except queue.Empty:
                if timeout_seconds is not None and time.monotonic() - started > timeout_seconds:
                    timed_out = True
                    process.kill()
                    emit(f"Command timed out after {timeout_seconds} seconds.")
                    break
                if process.poll() is not None and not reader.is_alive():
                    break
                continue

            if line is None:
                break
            emit(line)

        reader.join(timeout=1)
        process.stdout.close()
        exit_code = process.wait()
        return CommandResult(
            exit_code=124 if timed_out else int(exit_code),
            stdout="\n".join(output_lines),
            stderr="",
            timed_out=timed_out,
        )
    except Exception as exception:
        return CommandResult(
            exit_code=1,
            stdout="\n".join(output_lines),
            stderr=str(exception),
        )


def _execute_subprocess_capture(
    *,
    command: list[str],
    cwd: Path,
    env: dict[str, str],
    timeout_seconds: int | None = None,
    output_callback: Callable[[str], None] | None = None,
) -> CommandResult:
    try:
        completed = subprocess.run(
            command,
            cwd=cwd,
            env=env,
            text=True,
            capture_output=True,
            timeout=timeout_seconds,
            check=False,
        )
        if output_callback and completed.stdout:
            for line in completed.stdout.splitlines():
                output_callback(line)
        if output_callback and completed.stderr:
            for line in completed.stderr.splitlines():
                output_callback(line)
        return CommandResult(
            exit_code=int(completed.returncode),
            stdout=completed.stdout or "",
            stderr=completed.stderr or "",
        )
    except subprocess.TimeoutExpired as exception:
        return CommandResult(
            exit_code=124,
            stdout=exception.stdout or "",
            stderr=exception.stderr or f"Command timed out after {timeout_seconds} seconds.",
            timed_out=True,
        )
    except Exception as exception:
        return CommandResult(exit_code=1, stderr=str(exception))


def _parse_cli_json(text: str) -> dict[str, Any] | None:
    if not text.strip():
        return None

    try:
        parsed = json.loads(text)
        return parsed if isinstance(parsed, dict) else None
    except json.JSONDecodeError:
        pass

    start = text.find("{")
    end = text.rfind("}")
    if start < 0 or end <= start:
        return None
    try:
        parsed = json.loads(text[start : end + 1])
        return parsed if isinstance(parsed, dict) else None
    except json.JSONDecodeError:
        return None


def _json_value(payload: dict[str, Any] | None, pascal: str, camel: str) -> Any:
    if not payload:
        return None
    return payload.get(pascal, payload.get(camel))


def _initial_staging_manifest(*, requested: bool, connection_string_source: str) -> dict[str, Any]:
    return {
        "requested": bool(requested),
        "enabled": bool(requested),
        "status": "pending" if requested else "not_requested",
        "connection_string_source": connection_string_source if requested else "missing",
        "started_at_utc": None,
        "finished_at_utc": None,
        "skip_reason": None,
        "unsupported_steps": [],
        "commands": [],
    }


def _staging_command_specs(manifest: dict[str, Any], connection_string: str) -> list[dict[str, Any]]:
    specs: list[dict[str, Any]] = []
    command_by_step = {
        "rank": ("ranks", "stage-ranks"),
        "products": ("products", "stage-products"),
        "logistics": ("logistics", "stage-logistics"),
        "reviews": ("reviews", "stage-reviews"),
        "product_details": ("product_details", "stage-product-details"),
    }

    for step_name in STEP_ORDER:
        kind_command = command_by_step.get(step_name)
        if kind_command is None:
            continue

        step = _record_for(manifest, step_name)
        if not _step_is_stageable(step):
            continue

        kind, cli_command = kind_command
        for run_dir in step.get("output_run_dirs") or []:
            command = [
                "dotnet",
                "run",
                "--project",
                str(INGESTION_CLI_PROJECT),
                "--",
                cli_command,
                run_dir,
                "--connection-string",
                connection_string,
            ]
            specs.append({
                "kind": kind,
                "run_dir": run_dir,
                "command": command,
            })
    return specs


def _run_staging(
    *,
    manifest: dict[str, Any],
    pipeline_run_dir: Path,
    repo_root: Path,
    connection_string: str | None,
    connection_string_source: str,
    parser_status: str,
    dry_run: bool,
    executor: Callable[..., CommandResult],
) -> None:
    staging = _initial_staging_manifest(requested=True, connection_string_source=connection_string_source)
    manifest["staging"] = staging
    secrets = [connection_string] if connection_string else []

    if dry_run:
        staging["status"] = "skipped"
        staging["enabled"] = False
        staging["skip_reason"] = "dry_run"
        _emit_pipeline(pipeline_run_dir, "STAGING SKIPPED: dry-run is active")
        return

    if parser_status not in {"succeeded", "partial"}:
        staging["status"] = "skipped"
        staging["enabled"] = False
        staging["skip_reason"] = f"parser_status={parser_status}"
        _emit_pipeline(pipeline_run_dir, f"STAGING SKIPPED: parser status is {parser_status}")
        return

    if not connection_string:
        staging["status"] = "failed"
        staging["enabled"] = False
        staging["skip_reason"] = "connection_string_missing"
        _emit_pipeline(pipeline_run_dir, "STAGING FAILED: connection string is missing")
        return

    specs = _staging_command_specs(manifest, connection_string)
    if not specs:
        staging["status"] = "skipped"
        staging["enabled"] = False
        staging["skip_reason"] = "no_artifacts"
        _emit_pipeline(pipeline_run_dir, "STAGING SKIPPED: no successful parser artifacts to stage")
        return

    staging["status"] = "running"
    staging["started_at_utc"] = utc_now_iso()
    _emit_pipeline(pipeline_run_dir, f"STAGING START: {len(specs)} command(s)")
    env = os.environ.copy()

    for index, spec in enumerate(specs, start=1):
        command = spec["command"]
        command_record: dict[str, Any] = {
            "kind": spec["kind"],
            "run_dir": spec["run_dir"],
            "command": _sanitize_command(command, secrets),
            "started_at_utc": utc_now_iso(),
            "finished_at_utc": None,
            "exit_code": None,
            "stdout": "",
            "stderr": "",
            "parsed_json": None,
            "rows_read": None,
            "rows_written": None,
            "rows_skipped": None,
            "error_count": None,
            "details": None,
        }
        staging["commands"].append(command_record)
        _emit_pipeline(
            pipeline_run_dir,
            f"STAGING RUN {index}/{len(specs)} {spec['kind']}: {_command_display(command, secrets)}",
        )

        try:
            result = executor(
                command=command,
                cwd=repo_root,
                env=env,
                timeout_seconds=None,
                output_callback=None,
            )
        except Exception as exception:
            result = CommandResult(exit_code=1, stderr=_redact_text(str(exception), secrets))

        stdout = _redact_text(result.stdout, secrets)
        stderr = _redact_text(result.stderr, secrets)
        parsed_json = _parse_cli_json(stdout)
        command_record.update({
            "finished_at_utc": utc_now_iso(),
            "exit_code": result.exit_code,
            "stdout": stdout,
            "stderr": stderr,
            "parsed_json": parsed_json,
            "rows_read": _json_value(parsed_json, "RowsRead", "rowsRead"),
            "rows_written": _json_value(parsed_json, "RowsWritten", "rowsWritten"),
            "rows_skipped": _json_value(parsed_json, "RowsSkipped", "rowsSkipped"),
            "error_count": _json_value(parsed_json, "ErrorCount", "errorCount"),
            "details": _json_value(parsed_json, "Details", "details"),
        })

        _emit_pipeline(
            pipeline_run_dir,
            "STAGING RESULT "
            f"{index}/{len(specs)} {spec['kind']}: exit_code={result.exit_code} "
            f"rows_written={command_record['rows_written']} errors={command_record['error_count']}",
        )

        if result.exit_code != 0:
            staging["status"] = "failed"
            staging["skip_reason"] = f"{spec['kind']} staging failed"
            _emit_pipeline(
                pipeline_run_dir,
                f"STAGING FAILED: {spec['kind']} command exited with code {result.exit_code}; artifacts were kept",
            )
            break

    staging["finished_at_utc"] = utc_now_iso()
    if staging["status"] == "running":
        staging["status"] = "succeeded"
    _emit_pipeline(pipeline_run_dir, f"STAGING FINISHED: status={staging['status']}")


def _append_pipeline_log(run_dir: Path, message: str) -> None:
    with (run_dir / "pipeline.log").open("a", encoding="utf-8") as file:
        file.write(message.rstrip() + "\n")


def _emit_pipeline(run_dir: Path, message: str) -> None:
    _console(message)
    _append_pipeline_log(run_dir, f"[market-refresh] {message}")


def _format_duration(started_at_utc: str | None, finished_at_utc: str | None) -> str:
    if not started_at_utc or not finished_at_utc:
        return "unknown"
    try:
        started = datetime.fromisoformat(started_at_utc.replace("Z", "+00:00"))
        finished = datetime.fromisoformat(finished_at_utc.replace("Z", "+00:00"))
    except ValueError:
        return "unknown"
    seconds = int((finished - started).total_seconds())
    return f"{seconds}s"


def _summary_text(summary: dict[str, Any]) -> str:
    parts: list[str] = []
    if summary.get("status"):
        parts.append(f"status={summary['status']}")
    row_counts = summary.get("row_counts") or {}
    if row_counts:
        parts.extend(f"{key}={value}" for key, value in row_counts.items())
    page_counts = summary.get("page_counts") or {}
    if page_counts:
        parts.extend(f"pages_{key}={value}" for key, value in page_counts.items())
    counters = summary.get("counters") or {}
    for key in (
        "products_requested",
        "products_succeeded",
        "products_failed",
        "snapshot_rows_written",
        "warehouse_rows_written",
        "products_selected",
        "products_with_reviews",
        "reviews_written",
        "replies_written",
        "errors",
    ):
        if key in counters:
            parts.append(f"{key}={counters[key]}")
    coverage = summary.get("coverage") or {}
    for key in ("attempted_percent", "succeeded_percent", "roots_attempted_percent", "products_attempted_percent"):
        if key in coverage:
            parts.append(f"{key}={coverage[key]}")
    return ", ".join(parts) if parts else "no counters"


def _run_step(
    *,
    manifest: dict[str, Any],
    pipeline_run_dir: Path,
    plan: PipelineStepPlan,
    output_base_dir: Path,
    repo_root: Path,
    dry_run: bool,
    executor: Callable[..., CommandResult],
) -> None:
    record = _record_for(manifest, plan.step_name)
    record.update(asdict(PipelineStepRecord(step_name=plan.step_name)))
    record["command"] = plan.commands
    record["cwd"] = str(repo_root)
    record["env_overrides"] = plan.env_overrides

    if plan.preflight_error:
        record["status"] = "failed"
        record["error_summary"] = plan.preflight_error
        _emit_pipeline(pipeline_run_dir, f"FAIL {plan.step_name}: {plan.preflight_error}")
        return

    if not plan.enabled:
        record["status"] = "skipped"
        record["error_summary"] = "Step disabled by mode config."
        _emit_pipeline(pipeline_run_dir, f"SKIP {plan.step_name}: disabled by mode config")
        return

    if not plan.commands:
        record["status"] = "skipped"
        record["error_summary"] = "Step has no command to run."
        _emit_pipeline(pipeline_run_dir, f"SKIP {plan.step_name}: no command to run")
        return

    if dry_run:
        record["status"] = "skipped"
        record["error_summary"] = "Dry run: command not executed."
        _emit_pipeline(pipeline_run_dir, f"DRY-RUN {plan.step_name}: {len(plan.commands)} command(s)")
        for index, command in enumerate(plan.commands, start=1):
            _emit_pipeline(pipeline_run_dir, f"PLAN {plan.step_name} {index}/{len(plan.commands)}: {' '.join(command)}")
        return

    record["status"] = "running"
    record["started_at_utc"] = utc_now_iso()
    _emit_pipeline(pipeline_run_dir, f"START {plan.step_name}: {len(plan.commands)} command(s)")
    output_run_dirs: list[str] = []
    child_manifest_summaries: list[dict[str, Any]] = []
    exit_codes: list[int] = []
    had_partial_child = False

    for index, command in enumerate(plan.commands, start=1):
        before = _existing_run_dirs(output_base_dir)
        env = os.environ.copy()
        env.update(plan.env_overrides)
        _emit_pipeline(pipeline_run_dir, f"RUN {plan.step_name} {index}/{len(plan.commands)}: {' '.join(command)}")

        def output_callback(line: str, *, step_name: str = plan.step_name) -> None:
            prefixed = f"[{step_name}] {line}"
            print(prefixed, flush=True)
            _append_pipeline_log(pipeline_run_dir, prefixed)

        result = executor(
            command=command,
            cwd=repo_root,
            env=env,
            timeout_seconds=plan.timeout_seconds,
            output_callback=output_callback,
        )
        exit_codes.append(result.exit_code)
        if result.stderr:
            output_callback(result.stderr)

        run_dir = _run_dir_from_manifest_text(result.stdout + "\n" + result.stderr)
        if run_dir is None:
            run_dir = _latest_output_run_dir(before, output_base_dir)

        if run_dir is not None:
            output_run_dirs.append(str(run_dir))
            summary = _manifest_summary(run_dir)
            child_manifest_summaries.append(summary)
            _emit_pipeline(pipeline_run_dir, f"CHILD {plan.step_name} {index}/{len(plan.commands)}: {run_dir}")
            _emit_pipeline(pipeline_run_dir, f"SUMMARY {plan.step_name} {index}/{len(plan.commands)}: {_summary_text(summary)}")

        if result.exit_code != 0:
            record["status"] = "failed"
            record["error_summary"] = result.stderr.strip() or result.stdout.strip() or f"Command failed with exit code {result.exit_code}."
            _emit_pipeline(pipeline_run_dir, f"FAIL {plan.step_name} {index}/{len(plan.commands)}: exit_code={result.exit_code}")
            break

        child_status = _child_status(run_dir) if run_dir is not None else None
        if run_dir is not None and child_status not in {None, "succeeded"}:
            summary = child_manifest_summaries[-1] if child_manifest_summaries else _manifest_summary(run_dir)
            if child_status == "partial" and _summary_has_usable_artifact(plan.step_name, summary):
                had_partial_child = True
                _emit_pipeline(
                    pipeline_run_dir,
                    f"PARTIAL {plan.step_name} {index}/{len(plan.commands)}: child artifact is usable",
                )
                continue
            record["status"] = "failed"
            record["error_summary"] = f"Child manifest status is {child_status}."
            _emit_pipeline(pipeline_run_dir, f"FAIL {plan.step_name} {index}/{len(plan.commands)}: {record['error_summary']}")
            break

    record["finished_at_utc"] = utc_now_iso()
    record["exit_code"] = next((code for code in exit_codes if code != 0), exit_codes[-1] if exit_codes else None)
    record["output_run_dirs"] = output_run_dirs
    record["child_manifest_summaries"] = child_manifest_summaries
    if record["status"] == "running":
        record["status"] = "partial" if had_partial_child else "succeeded"
    _emit_pipeline(
        pipeline_run_dir,
        f"FINISH {plan.step_name}: status={record['status']} duration={_format_duration(record['started_at_utc'], record['finished_at_utc'])}",
    )


def _build_manifest(
    *,
    pipeline_run_id: str,
    pipeline_run_dir: Path,
    config: PipelineConfig,
    mode: str,
    staging_requested: bool,
    connection_string_source: str,
    test_run: bool = False,
    test_label: str | None = None,
) -> dict[str, Any]:
    return {
        "schema_version": 1,
        "pipeline_run_id": pipeline_run_id,
        "run_kind": "market_analytics_refresh",
        "mode": mode,
        "started_at_utc": utc_now_iso(),
        "finished_at_utc": None,
        "status": "running",
        "dry_run": False,
        "is_test_run": bool(test_run),
        "test_label": test_label.strip() if test_label else None,
        "git_commit": get_git_commit(_repo_root()),
        "config_path": str(config.path),
        "config_snapshot": config.safe_snapshot(),
        "environment_summary": {
            "python": sys.version.split()[0],
            "platform": platform.platform(),
            "cwd": str(_repo_root()),
        },
        "steps": [asdict(PipelineStepRecord(step_name=name)) for name in STEP_ORDER],
        "staging": _initial_staging_manifest(
            requested=staging_requested,
            connection_string_source=connection_string_source,
        ),
        "suggested_ingestion_commands": [],
        "output_files": {
            "pipeline_manifest": str(pipeline_run_dir / "pipeline_manifest.json"),
            "pipeline_log": str(pipeline_run_dir / "pipeline.log"),
        },
    }


def _run_batched_pipeline(
    *,
    config: PipelineConfig,
    mode: str,
    mode_config: dict[str, Any],
    manifest: dict[str, Any],
    pipeline_run_id: str,
    pipeline_run_dir: Path,
    output_base_dir: Path,
    repo_root: Path,
    python_executable: str,
    test_env: dict[str, str],
    skip_rank: bool,
    skip_products: bool,
    skip_logistics: bool,
    skip_reviews: bool,
    skip_product_details: bool,
    dry_run: bool,
    stage_to_db: bool,
    connection_string: str | None,
    connection_string_source: str,
    executor: Callable[..., CommandResult],
    staging_executor: Callable[..., CommandResult],
    streaming_product_runner: Callable[..., Path] | None = None,
) -> None:
    batching_config = dict(mode_config.get("batching") or {})
    batch_size = int(batching_config.get("batch_size") or 100)
    max_batches_raw = batching_config.get("max_batches") or os.environ.get("PARSER_MAX_STREAM_BATCHES")
    max_batches = int(max_batches_raw) if max_batches_raw else None
    if max_batches is not None and max_batches <= 0:
        max_batches = None
    worker_id = str(batching_config.get("worker_id") or os.environ.get("PARSER_WORKER_ID") or "local-worker")
    shard_key = str(batching_config.get("shard_key") or os.environ.get("PARSER_SHARD_KEY") or "default")
    existing_batching = manifest.get("batching") or {}
    manifest["batching"] = {
        "enabled": True,
        "batch_size": batch_size,
        "max_batches": max_batches,
        "worker_id": worker_id,
        "shard_key": shard_key,
        "total_batches": int(existing_batching.get("total_batches") or 0),
        "staged_batches": int(existing_batching.get("staged_batches") or 0),
        "quarantined_batches": int(existing_batching.get("quarantined_batches") or 0),
        "staged_products": int(existing_batching.get("staged_products") or 0),
        "quarantined_products": int(existing_batching.get("quarantined_products") or 0),
        "batches": list(existing_batching.get("batches") or []),
        "quarantine": list(existing_batching.get("quarantine") or []),
        "complete_pipeline": existing_batching.get("complete_pipeline"),
    }
    _write_manifest(pipeline_run_dir, manifest)

    rank_plan = _with_env_overrides(
        _build_rank_plan(mode_config, repo_root=repo_root, python_executable=python_executable),
        test_env,
    )
    products_plan = _with_env_overrides(
        _build_products_plan(mode_config, repo_root=repo_root, python_executable=python_executable),
        test_env,
    )

    if skip_rank:
        _record_for(manifest, "rank")["status"] = "skipped"
        _record_for(manifest, "rank")["error_summary"] = "Skipped by CLI flag."
    else:
        _run_step(
            manifest=manifest,
            pipeline_run_dir=pipeline_run_dir,
            plan=rank_plan,
            output_base_dir=output_base_dir,
            repo_root=repo_root,
            dry_run=dry_run,
            executor=executor,
        )
    _write_manifest(pipeline_run_dir, manifest)

    rank_staging_command: dict[str, Any] | None = None
    rank_record = _record_for(manifest, "rank")
    if stage_to_db and not dry_run and _step_is_stageable(rank_record) and rank_record.get("output_run_dirs"):
        rank_dir = str(rank_record["output_run_dirs"][-1])
        command = [
            "dotnet",
            "run",
            "--project",
            str(INGESTION_CLI_PROJECT),
            "--",
            "stage-ranks",
            rank_dir,
            "--connection-string",
            connection_string or "",
        ]
        _emit_pipeline(pipeline_run_dir, f"BATCHED STAGING rank: {_command_display(command, [connection_string] if connection_string else [])}")
        rank_staging_command = _run_single_staging_command(
            command=command,
            kind="ranks",
            run_dir=rank_dir,
            repo_root=repo_root,
            connection_string=connection_string,
            executor=staging_executor,
        )

    streaming_batch_index_offset = max(
        [int(batch.get("batch_index") or 0) for batch in (manifest.get("batching") or {}).get("batches") or []] or [0]
    )

    if skip_products:
        products_record = _record_for(manifest, "products")
        products_record["status"] = "skipped"
        products_record["error_summary"] = "Skipped by CLI flag."
        for step_name in ("logistics", "reviews", "product_details"):
            _record_for(manifest, step_name)["status"] = "skipped"
            _record_for(manifest, step_name)["error_summary"] = "Skipped because products were skipped."
        return

    if not dry_run:
        for step_name in ("logistics", "reviews", "product_details"):
            record = _record_for(manifest, step_name)
            record["status"] = "skipped"
            record["error_summary"] = "Handled per streaming complete-card batch."

        if rank_staging_command is not None:
            manifest["staging"]["commands"].append(rank_staging_command)
            if rank_staging_command["exit_code"] != 0:
                manifest["staging"]["status"] = "failed"
                manifest["staging"]["skip_reason"] = "rank staging failed"
                _write_manifest(pipeline_run_dir, manifest)
                return

        parent_product_run_dir: Path | None = None

        def handle_streaming_batch(discovery_batch: Any) -> SimpleNamespace:
            nonlocal parent_product_run_dir
            parent_product_run_dir = Path(discovery_batch.parent_run_dir)
            parent_manifest_path = parent_product_run_dir / "manifest.json"
            parent_manifest = (
                json.loads(parent_manifest_path.read_text(encoding="utf-8"))
                if parent_manifest_path.exists()
                else {
                    "schema_version": 1,
                    "parser_run_id": getattr(discovery_batch, "parent_parser_run_id", "streaming_products"),
                    "marketplace": "wildberries",
                    "requested_scope": {},
                    "source_region_dest": "",
                    "parser_version": get_git_commit(_repo_root()),
                    "config_snapshot": {},
                }
            )
            batch = _create_product_batch_run_dir_from_rows(
                parent_manifest=parent_manifest,
                batch_rows=list(discovery_batch.rows),
                source_product_run_dir=parent_product_run_dir,
                batches_dir=pipeline_run_dir / "batches",
                pipeline_run_id=pipeline_run_id,
                batch_index=streaming_batch_index_offset + int(discovery_batch.batch_index),
                worker_id=worker_id,
                shard_key=shard_key,
            )
            manifest["batching"]["total_batches"] = max(
                int(manifest["batching"].get("total_batches") or 0),
                batch.batch_index,
            )
            batch_record: dict[str, Any] = {
                "batch_id": batch.batch_id,
                "batch_index": batch.batch_index,
                "size": batch.size,
                "batch_dir": str(batch.batch_dir),
                "product_run_dir": str(batch.product_run_dir),
                "status": "running",
                "steps": [],
                "staging": None,
                "started_at_utc": utc_now_iso(),
                "finished_at_utc": None,
            }
            manifest["batching"]["batches"].append(batch_record)
            product_manifest_path = batch.product_run_dir / "manifest.json"
            product_manifest = (
                json.loads(product_manifest_path.read_text(encoding="utf-8"))
                if product_manifest_path.exists()
                else {}
            )
            batch_created_at_utc = str(product_manifest.get("started_at_utc") or batch_record["started_at_utc"])
            _emit_pipeline(
                pipeline_run_dir,
                (
                    f"STREAM BATCH {batch.batch_index} START: {batch.batch_id} "
                    f"size={batch.size} batch_created_at_utc={batch_created_at_utc}"
                ),
            )

            batch_manifest = {
                "batch_id": batch.batch_id,
                "pipeline_run_id": pipeline_run_id,
                "batch_index": batch.batch_index,
                "size": batch.size,
                "product_run_dir": str(batch.product_run_dir),
                "steps": [
                    {
                        **asdict(PipelineStepRecord(step_name=name)),
                        "step": name,
                    }
                    for name in ("logistics", "reviews", "product_details")
                ],
            }
            batch_env = {
                **test_env,
                **_batch_scope_env(
                    pipeline_run_id=pipeline_run_id,
                    batch=batch,
                    worker_id=worker_id,
                    shard_key=shard_key,
                    source_niche=", ".join(_product_run_source_subcategories(batch.product_run_dir)),
                ),
            }

            batch_steps_failed = False
            for step_name in ("logistics", "reviews", "product_details"):
                if step_name == "logistics" and skip_logistics:
                    continue
                if step_name == "reviews" and skip_reviews:
                    continue
                if step_name == "product_details" and skip_product_details:
                    continue

                if step_name == "logistics":
                    plan = _build_logistics_plan(
                        mode_config,
                        repo_root=repo_root,
                        python_executable=python_executable,
                        product_run_dir=batch.product_run_dir,
                        output_base_dir=batch.batch_dir,
                    )
                elif step_name == "reviews":
                    review_mode_config = mode_config
                    batch_subcategories = _product_run_source_subcategories(batch.product_run_dir)
                    if batch_subcategories:
                        review_mode_config = dict(mode_config)
                        reviews_config = dict(review_mode_config.get("reviews") or {})
                        reviews_config["source_subcategories"] = batch_subcategories
                        review_mode_config["reviews"] = reviews_config
                    plan = _build_reviews_plan(
                        review_mode_config,
                        repo_root=repo_root,
                        python_executable=python_executable,
                        product_run_dir=batch.product_run_dir,
                    )
                else:
                    plan = _build_product_details_plan(
                        mode_config,
                        repo_root=repo_root,
                        python_executable=python_executable,
                        product_run_dir=batch.product_run_dir,
                        output_base_dir=batch.batch_dir,
                    )

                plan = _with_env_overrides(plan, batch_env)
                _run_step(
                    manifest=batch_manifest,
                    pipeline_run_dir=pipeline_run_dir,
                    plan=plan,
                    output_base_dir=batch.batch_dir,
                    repo_root=repo_root,
                    dry_run=False,
                    executor=executor,
                )
                step_record = _record_for(batch_manifest, step_name)
                step_record["step"] = step_name
                batch_record["steps"].append(step_record)
                if step_record.get("status") not in {"succeeded", "partial", "skipped"}:
                    batch_steps_failed = True
                    break

            _write_batch_manifest(batch, batch_manifest)
            validation_ok = False
            validation_reason = None
            if not batch_steps_failed:
                try:
                    validation_ok, validation_reason = _validate_complete_card_batch(
                        batch=batch,
                        batch_manifest=batch_manifest,
                        require_logistics=not skip_logistics,
                        require_reviews=not skip_reviews,
                        require_product_details=not skip_product_details,
                    )
                except Exception as exception:
                    validation_reason = str(exception)

            if batch_steps_failed or not validation_ok:
                reason = "mandatory enrichment step failed" if batch_steps_failed else validation_reason or "batch completeness validation failed"
                batch_record["status"] = "quarantined"
                batch_record["quarantine_reason"] = reason
                manifest["batching"]["quarantine"].append({"batch_id": batch.batch_id, "reason": reason})
                manifest["batching"]["quarantined_batches"] = int(manifest["batching"].get("quarantined_batches") or 0) + 1
                manifest["batching"]["quarantined_products"] = int(manifest["batching"].get("quarantined_products") or 0) + batch.size
                batch_record["finished_at_utc"] = utc_now_iso()
                _write_manifest(pipeline_run_dir, manifest)
                return SimpleNamespace(status="quarantined", reason=reason)

            if stage_to_db:
                command = _complete_batch_staging_command(
                    batch_dir=batch.batch_dir,
                    connection_string=connection_string or "",
                )
                _emit_pipeline(
                    pipeline_run_dir,
                    f"STREAM BATCH {batch.batch_index} STAGING: {_command_display(command, [connection_string] if connection_string else [])}",
                )
                staging_result = _run_single_staging_command(
                    command=command,
                    kind="complete_batch",
                    run_dir=str(batch.batch_dir),
                    repo_root=repo_root,
                    connection_string=connection_string,
                    executor=staging_executor,
                )
                batch_record["staging"] = staging_result
                manifest["staging"]["commands"].append(staging_result)
                if staging_result["exit_code"] != 0:
                    batch_record["status"] = "failed"
                    manifest["staging"]["status"] = "failed"
                    manifest["staging"]["skip_reason"] = "complete batch staging failed"
                    batch_record["finished_at_utc"] = utc_now_iso()
                    _write_manifest(pipeline_run_dir, manifest)
                    return SimpleNamespace(status="failed", reason="complete batch staging failed")
                batch_record["status"] = "staged"
                manifest["batching"]["staged_batches"] = int(manifest["batching"].get("staged_batches") or 0) + 1
                manifest["batching"]["staged_products"] = int(manifest["batching"].get("staged_products") or 0) + batch.size
                _emit_pipeline(
                    pipeline_run_dir,
                    f"STREAM BATCH {batch.batch_index} STAGED: {batch.batch_id} staged_at_utc={utc_now_iso()}",
                )
                if max_batches is not None and int(manifest["batching"].get("staged_batches") or 0) >= max_batches:
                    batch_record["finished_at_utc"] = utc_now_iso()
                    _write_manifest(pipeline_run_dir, manifest)
                    _emit_pipeline(
                        pipeline_run_dir,
                        f"STREAM BATCH LIMIT REACHED: max_batches={max_batches}",
                    )
                    return SimpleNamespace(status="stopped", reason="batch limit reached")
            else:
                batch_record["status"] = "ready"

            batch_record["finished_at_utc"] = utc_now_iso()
            _write_manifest(pipeline_run_dir, manifest)
            return SimpleNamespace(status=batch_record["status"], reason=None)

        def default_streaming_product_runner(**kwargs: Any) -> Path:
            from config import ParserConfig
            from pipelines.products.runner import run_parser_streaming

            product_config = dict(mode_config.get("product") or {})
            config_path = _to_abs(product_config["config"], base_dir=repo_root)
            env_overrides = dict(products_plan.env_overrides)
            original_env = {key: os.environ.get(key) for key in env_overrides}
            try:
                for key, value in env_overrides.items():
                    os.environ[key] = value
                parser_config = ParserConfig.load(config_path)
                return run_parser_streaming(
                    parser_config,
                    streaming_batch_size=kwargs["streaming_batch_size"],
                    batch_handler=kwargs["batch_handler"],
                    smoke_only=bool(product_config.get("smoke_only")),
                    resume_seen_product_ids=kwargs.get("resume_seen_product_ids"),
                )
            finally:
                for key, value in original_env.items():
                    if value is None:
                        os.environ.pop(key, None)
                    else:
                        os.environ[key] = value

        runner_fn = streaming_product_runner or default_streaming_product_runner
        resume_seen_product_ids = _resume_seen_product_ids_from_batches(manifest)
        parent_product_run_dir = runner_fn(
            config=mode_config,
            streaming_batch_size=batch_size,
            batch_handler=handle_streaming_batch,
            smoke_only=False,
            resume_seen_product_ids=resume_seen_product_ids,
        )
        products_record = _record_for(manifest, "products")
        products_record["status"] = "succeeded"
        products_record["output_run_dirs"] = [str(parent_product_run_dir)]
        products_record["error_summary"] = None

        if stage_to_db and manifest["staging"].get("status") != "failed":
            command = _complete_pipeline_command(
                pipeline_run_id=pipeline_run_id,
                connection_string=connection_string or "",
            )
            _emit_pipeline(
                pipeline_run_dir,
                f"BATCHED COMPLETE PIPELINE: {_command_display(command, [connection_string] if connection_string else [])}",
            )
            completion = _run_single_staging_command(
                command=command,
                kind="complete_pipeline",
                run_dir=pipeline_run_id,
                repo_root=repo_root,
                connection_string=connection_string,
                executor=staging_executor,
            )
            manifest["batching"]["complete_pipeline"] = completion
            manifest["staging"]["commands"].append(completion)
            manifest["staging"]["status"] = "succeeded" if completion["exit_code"] == 0 else "failed"
        elif not stage_to_db:
            manifest["staging"]["status"] = "not_requested"

        manifest["staging"]["requested"] = bool(stage_to_db)
        manifest["staging"]["enabled"] = bool(stage_to_db)
        manifest["staging"]["connection_string_source"] = connection_string_source if stage_to_db else "missing"
        _write_manifest(pipeline_run_dir, manifest)
        return

    _run_step(
        manifest=manifest,
        pipeline_run_dir=pipeline_run_dir,
        plan=products_plan,
        output_base_dir=output_base_dir,
        repo_root=repo_root,
        dry_run=dry_run,
        executor=executor,
    )
    _write_manifest(pipeline_run_dir, manifest)

    products_record = _record_for(manifest, "products")
    if dry_run:
        placeholder_batch = ProductBatchRun(
            batch_id=f"{pipeline_run_id}:dry-run:batch_0001",
            batch_index=1,
            size=batch_size,
            batch_dir=pipeline_run_dir / "batches" / "batch_0001",
            product_run_dir=pipeline_run_dir / "dry_run_batch_products",
        )
        batch_record: dict[str, Any] = {
            "batch_id": placeholder_batch.batch_id,
            "batch_index": placeholder_batch.batch_index,
            "size": placeholder_batch.size,
            "batch_dir": str(placeholder_batch.batch_dir),
            "product_run_dir": str(placeholder_batch.product_run_dir),
            "status": "planned",
            "steps": [],
            "staging": None,
            "started_at_utc": None,
            "finished_at_utc": None,
        }
        batch_manifest = {
            "batch_id": placeholder_batch.batch_id,
            "pipeline_run_id": pipeline_run_id,
            "batch_index": placeholder_batch.batch_index,
            "size": placeholder_batch.size,
            "product_run_dir": str(placeholder_batch.product_run_dir),
            "steps": [asdict(PipelineStepRecord(step_name=name)) for name in ("logistics", "reviews", "product_details")],
        }
        for plan in (
            _build_logistics_plan(
                mode_config,
                repo_root=repo_root,
                python_executable=python_executable,
                product_run_dir=placeholder_batch.product_run_dir,
                output_base_dir=placeholder_batch.batch_dir,
            ),
            _build_reviews_plan(
                mode_config,
                repo_root=repo_root,
                python_executable=python_executable,
                product_run_dir=placeholder_batch.product_run_dir,
            ),
            _build_product_details_plan(
                mode_config,
                repo_root=repo_root,
                python_executable=python_executable,
                product_run_dir=placeholder_batch.product_run_dir,
                output_base_dir=placeholder_batch.batch_dir,
            ),
        ):
            _run_step(
                manifest=batch_manifest,
                pipeline_run_dir=pipeline_run_dir,
                plan=plan,
                output_base_dir=placeholder_batch.batch_dir,
                repo_root=repo_root,
                dry_run=True,
                executor=executor,
            )
            batch_record["steps"].append(_record_for(batch_manifest, plan.step_name))

        if stage_to_db:
            batch_record["staging"] = {
                "kind": "complete_batch",
                "run_dir": str(placeholder_batch.batch_dir),
                "command": _sanitize_command(
                    _complete_batch_staging_command(
                        batch_dir=placeholder_batch.batch_dir,
                        connection_string=connection_string or "",
                    ),
                    [connection_string] if connection_string else [],
                ),
                "status": "planned",
            }
            manifest["batching"]["complete_pipeline"] = {
                "kind": "complete_pipeline",
                "run_dir": pipeline_run_id,
                "command": _sanitize_command(
                    _complete_pipeline_command(
                        pipeline_run_id=pipeline_run_id,
                        connection_string=connection_string or "",
                    ),
                    [connection_string] if connection_string else [],
                ),
                "status": "planned",
            }
        manifest["batching"]["total_batches"] = 1
        manifest["batching"]["batches"].append(batch_record)
        for step_name in ("logistics", "reviews", "product_details"):
            _record_for(manifest, step_name)["status"] = "skipped"
            _record_for(manifest, step_name)["error_summary"] = "Batched dry-run: handled per complete-card batch."
        return

    if not _step_is_stageable(products_record) or not products_record.get("output_run_dirs"):
        for step_name in ("logistics", "reviews", "product_details"):
            _record_for(manifest, step_name)["status"] = "skipped"
            _record_for(manifest, step_name)["error_summary"] = "Products step did not produce usable artifacts."
        return

    product_run_dir = Path(products_record["output_run_dirs"][-1])
    batches = _create_product_batch_run_dirs(
        product_run_dir=product_run_dir,
        batches_dir=pipeline_run_dir / "batches",
        pipeline_run_id=pipeline_run_id,
        batch_size=batch_size,
        worker_id=worker_id,
        shard_key=shard_key,
    )
    manifest["batching"]["total_batches"] = len(batches)

    for step_name in ("logistics", "reviews", "product_details"):
        record = _record_for(manifest, step_name)
        record["status"] = "skipped"
        record["error_summary"] = "Handled per complete-card batch."

    if rank_staging_command is not None:
        manifest["staging"]["commands"].append(rank_staging_command)
        if rank_staging_command["exit_code"] != 0:
            manifest["staging"]["status"] = "failed"
            manifest["staging"]["skip_reason"] = "rank staging failed"
            _write_manifest(pipeline_run_dir, manifest)
            return

    for batch in batches:
        batch_record: dict[str, Any] = {
            "batch_id": batch.batch_id,
            "batch_index": batch.batch_index,
            "size": batch.size,
            "batch_dir": str(batch.batch_dir),
            "product_run_dir": str(batch.product_run_dir),
            "status": "running",
            "steps": [],
            "staging": None,
            "started_at_utc": utc_now_iso(),
            "finished_at_utc": None,
        }
        manifest["batching"]["batches"].append(batch_record)
        _emit_pipeline(pipeline_run_dir, f"BATCH {batch.batch_index}/{len(batches)} START: {batch.batch_id} size={batch.size}")

        batch_manifest = {
            "batch_id": batch.batch_id,
            "pipeline_run_id": pipeline_run_id,
            "batch_index": batch.batch_index,
            "size": batch.size,
            "product_run_dir": str(batch.product_run_dir),
            "steps": [asdict(PipelineStepRecord(step_name=name)) for name in ("logistics", "reviews", "product_details")],
        }
        batch_env = {
            **test_env,
            **_batch_scope_env(
                pipeline_run_id=pipeline_run_id,
                batch=batch,
                worker_id=worker_id,
                shard_key=shard_key,
                source_niche=", ".join(_product_run_source_subcategories(batch.product_run_dir)),
            ),
        }

        batch_steps_failed = False
        for step_name in ("logistics", "reviews", "product_details"):
            if step_name == "logistics" and skip_logistics:
                continue
            if step_name == "reviews" and skip_reviews:
                continue
            if step_name == "product_details" and skip_product_details:
                continue

            if step_name == "logistics":
                plan = _build_logistics_plan(
                    mode_config,
                    repo_root=repo_root,
                    python_executable=python_executable,
                    product_run_dir=batch.product_run_dir,
                    output_base_dir=batch.batch_dir,
                )
            elif step_name == "reviews":
                review_mode_config = mode_config
                batch_subcategories = _product_run_source_subcategories(batch.product_run_dir)
                if batch_subcategories:
                    review_mode_config = dict(mode_config)
                    reviews_config = dict(review_mode_config.get("reviews") or {})
                    reviews_config["source_subcategories"] = batch_subcategories
                    review_mode_config["reviews"] = reviews_config
                plan = _build_reviews_plan(
                    review_mode_config,
                    repo_root=repo_root,
                    python_executable=python_executable,
                    product_run_dir=batch.product_run_dir,
                )
            else:
                plan = _build_product_details_plan(
                    mode_config,
                    repo_root=repo_root,
                    python_executable=python_executable,
                    product_run_dir=batch.product_run_dir,
                    output_base_dir=batch.batch_dir,
                )

            plan = _with_env_overrides(plan, batch_env)
            _run_step(
                manifest=batch_manifest,
                pipeline_run_dir=pipeline_run_dir,
                plan=plan,
                output_base_dir=batch.batch_dir,
                repo_root=repo_root,
                dry_run=False,
                executor=executor,
            )
            step_record = _record_for(batch_manifest, step_name)
            batch_record["steps"].append(step_record)
            if step_record.get("status") not in {"succeeded", "partial", "skipped"}:
                batch_steps_failed = True
                break

        _write_batch_manifest(batch, batch_manifest)
        if batch_steps_failed:
            batch_record["status"] = "quarantined"
            manifest["batching"]["quarantine"].append(
                {
                    "batch_id": batch.batch_id,
                    "reason": "mandatory enrichment step failed",
                }
            )
            batch_record["finished_at_utc"] = utc_now_iso()
            _write_manifest(pipeline_run_dir, manifest)
            continue

        if stage_to_db:
            command = _complete_batch_staging_command(
                batch_dir=batch.batch_dir,
                connection_string=connection_string or "",
            )
            _emit_pipeline(
                pipeline_run_dir,
                f"BATCH {batch.batch_index}/{len(batches)} STAGING: {_command_display(command, [connection_string] if connection_string else [])}",
            )
            staging_result = _run_single_staging_command(
                command=command,
                kind="complete_batch",
                run_dir=str(batch.batch_dir),
                repo_root=repo_root,
                connection_string=connection_string,
                executor=staging_executor,
            )
            batch_record["staging"] = staging_result
            manifest["staging"]["commands"].append(staging_result)
            if staging_result["exit_code"] != 0:
                batch_record["status"] = "failed"
                manifest["staging"]["status"] = "failed"
                manifest["staging"]["skip_reason"] = "complete batch staging failed"
                batch_record["finished_at_utc"] = utc_now_iso()
                _write_manifest(pipeline_run_dir, manifest)
                continue
            batch_record["status"] = "staged"
        else:
            batch_record["status"] = "ready"

        batch_record["finished_at_utc"] = utc_now_iso()
        _write_manifest(pipeline_run_dir, manifest)

    if stage_to_db and manifest["staging"].get("status") != "failed":
        command = _complete_pipeline_command(
            pipeline_run_id=pipeline_run_id,
            connection_string=connection_string or "",
        )
        _emit_pipeline(
            pipeline_run_dir,
            f"BATCHED COMPLETE PIPELINE: {_command_display(command, [connection_string] if connection_string else [])}",
        )
        completion = _run_single_staging_command(
            command=command,
            kind="complete_pipeline",
            run_dir=pipeline_run_id,
            repo_root=repo_root,
            connection_string=connection_string,
            executor=staging_executor,
        )
        manifest["batching"]["complete_pipeline"] = completion
        manifest["staging"]["commands"].append(completion)
        manifest["staging"]["status"] = "succeeded" if completion["exit_code"] == 0 else "failed"
    elif not stage_to_db:
        manifest["staging"]["status"] = "not_requested"

    manifest["staging"]["requested"] = bool(stage_to_db)
    manifest["staging"]["enabled"] = bool(stage_to_db)
    manifest["staging"]["connection_string_source"] = connection_string_source if stage_to_db else "missing"


def run_pipeline(
    *,
    config: PipelineConfig,
    mode: str,
    skip_rank: bool = False,
    skip_products: bool = False,
    skip_logistics: bool = False,
    skip_reviews: bool = False,
    skip_product_details: bool = False,
    resume_run_dir: Path | None = None,
    force_step: str | None = None,
    fail_fast: bool = False,
    dry_run: bool = False,
    stage_to_db: bool = False,
    connection_string: str | None = None,
    connection_string_source: str = "missing",
    test_run: bool = False,
    test_label: str | None = None,
    executor: Callable[..., CommandResult] = _execute_subprocess,
    staging_executor: Callable[..., CommandResult] = _execute_subprocess_capture,
    streaming_product_runner: Callable[..., Path] | None = None,
) -> Path:
    if stage_to_db and not connection_string:
        raise ValueError(
            "Staging was requested, but no PostgreSQL connection string was available. "
            "Pass --connection-string or set ConnectionStrings__Postgres or ASHMES_POSTGRES_CONNECTION."
        )

    repo_root = _repo_root()
    mode_config = config.mode_config(mode)
    output_base_dir = config.output_base_dir
    pipelines_dir = output_base_dir / "pipelines"
    pipelines_dir.mkdir(parents=True, exist_ok=True)

    if resume_run_dir:
        pipeline_run_dir = resume_run_dir
        manifest = _load_manifest(pipeline_run_dir)
        pipeline_run_id = manifest["pipeline_run_id"]
        _emit_pipeline(pipeline_run_dir, f"RESUME {pipeline_run_id}: mode={mode}")
    else:
        pipeline_run_id = _make_pipeline_run_id()
        pipeline_run_dir = pipelines_dir / pipeline_run_id
        pipeline_run_dir.mkdir(parents=True, exist_ok=False)
        manifest = _build_manifest(
            pipeline_run_id=pipeline_run_id,
            pipeline_run_dir=pipeline_run_dir,
            config=config,
            mode=mode,
            staging_requested=stage_to_db,
            connection_string_source=connection_string_source,
            test_run=test_run,
            test_label=test_label,
        )
    manifest["staging"] = _initial_staging_manifest(
        requested=stage_to_db,
        connection_string_source=connection_string_source,
    )
    manifest["dry_run"] = bool(dry_run)
    manifest["is_test_run"] = bool(test_run)
    manifest["test_label"] = test_label.strip() if test_label else None
    _emit_pipeline(
        pipeline_run_dir,
        f"PIPELINE {pipeline_run_id}: mode={mode} dry_run={bool(dry_run)} stage_to_db={bool(stage_to_db)}",
    )

    effective_fail_fast = bool(fail_fast or config.defaults.get("fail_fast"))
    python_executable = _python_executable()
    test_env = _test_scope_env(pipeline_run_id=pipeline_run_id, test_run=test_run, test_label=test_label)
    interrupted = False
    product_run_dir: Path | None = None
    blocked_steps: set[str] = set()

    if _is_batched_full_enrichment(mode, mode_config):
        try:
            _run_batched_pipeline(
                config=config,
                mode=mode,
                mode_config=mode_config,
                manifest=manifest,
                pipeline_run_id=pipeline_run_id,
                pipeline_run_dir=pipeline_run_dir,
                output_base_dir=output_base_dir,
                repo_root=repo_root,
                python_executable=python_executable,
                test_env=test_env,
                skip_rank=skip_rank,
                skip_products=skip_products,
                skip_logistics=skip_logistics,
                skip_reviews=skip_reviews,
                skip_product_details=skip_product_details,
                dry_run=dry_run,
                stage_to_db=stage_to_db,
                connection_string=connection_string,
                connection_string_source=connection_string_source,
                executor=executor,
                staging_executor=staging_executor,
                streaming_product_runner=streaming_product_runner,
            )
        except KeyboardInterrupt:
            interrupted = True
            _emit_pipeline(pipeline_run_dir, "INTERRUPTED by user")
        finally:
            batching = manifest.get("batching") or {}
            has_quarantine = bool(batching.get("quarantine"))
            if dry_run and stage_to_db:
                manifest["staging"]["status"] = "skipped"
                manifest["staging"]["enabled"] = False
                manifest["staging"]["skip_reason"] = "dry_run"
            status = _final_status(manifest, interrupted=interrupted)
            if has_quarantine and status == "succeeded":
                status = "partial"
            manifest["status"] = status
            manifest["finished_at_utc"] = utc_now_iso()
            manifest["suggested_ingestion_commands"] = []
            _write_manifest(pipeline_run_dir, manifest)
            _emit_pipeline(
                pipeline_run_dir,
                "PIPELINE FINISHED: "
                f"status={manifest['status']} staging_status={(manifest.get('staging') or {}).get('status')} "
                f"batches={(manifest.get('batching') or {}).get('total_batches')}",
            )
        return pipeline_run_dir

    product_record = _record_for(manifest, "products")
    if product_record.get("status") == "succeeded" and product_record.get("output_run_dirs"):
        product_run_dir = Path(product_record["output_run_dirs"][-1])

    try:
        rank_plan = _with_env_overrides(
            _build_rank_plan(mode_config, repo_root=repo_root, python_executable=python_executable),
            test_env,
        )
        products_plan = _with_env_overrides(
            _build_products_plan(mode_config, repo_root=repo_root, python_executable=python_executable),
            test_env,
        )

        for step_name in STEP_ORDER:
            if step_name == "rank":
                plan = rank_plan
                skip_requested = skip_rank
            elif step_name == "products":
                plan = products_plan
                skip_requested = skip_products
            elif step_name == "logistics":
                plan_product_run_dir = product_run_dir
                if dry_run and plan_product_run_dir is None and products_plan.enabled:
                    plan_product_run_dir = Path("<products-run-dir>")
                plan = _with_env_overrides(
                    _build_logistics_plan(
                        mode_config,
                        repo_root=repo_root,
                        python_executable=python_executable,
                        product_run_dir=plan_product_run_dir,
                        output_base_dir=output_base_dir,
                    ),
                    test_env,
                )
                skip_requested = skip_logistics
            elif step_name == "reviews":
                plan_product_run_dir = product_run_dir
                if dry_run and plan_product_run_dir is None and products_plan.enabled:
                    plan_product_run_dir = Path("<products-run-dir>")
                plan = _with_env_overrides(
                    _build_reviews_plan(
                        mode_config,
                        repo_root=repo_root,
                        python_executable=python_executable,
                        product_run_dir=plan_product_run_dir,
                    ),
                    test_env,
                )
                skip_requested = skip_reviews
            else:
                plan_product_run_dir = product_run_dir
                if dry_run and plan_product_run_dir is None and products_plan.enabled:
                    plan_product_run_dir = Path("<products-run-dir>")
                plan = _with_env_overrides(
                    _build_product_details_plan(
                        mode_config,
                        repo_root=repo_root,
                        python_executable=python_executable,
                        product_run_dir=plan_product_run_dir,
                        output_base_dir=output_base_dir,
                    ),
                    test_env,
                )
                skip_requested = skip_product_details

            record = _record_for(manifest, step_name)
            if step_name in blocked_steps:
                _write_manifest(pipeline_run_dir, manifest)
                continue

            if skip_requested:
                record["status"] = "skipped"
                record["error_summary"] = "Skipped by CLI flag."
                _emit_pipeline(pipeline_run_dir, f"SKIP {step_name}: CLI flag")
                _write_manifest(pipeline_run_dir, manifest)
                continue

            if resume_run_dir and _step_is_stageable(record) and force_step != step_name:
                _emit_pipeline(pipeline_run_dir, f"SKIP {step_name}: already succeeded in resumed pipeline")
                if step_name == "products" and record.get("output_run_dirs"):
                    product_run_dir = Path(record["output_run_dirs"][-1])
                continue

            if force_step and step_name != force_step:
                _emit_pipeline(pipeline_run_dir, f"SKIP {step_name}: force-step={force_step}")
                continue

            if step_name in {"logistics", "reviews", "product_details"} and product_run_dir is None and not dry_run:
                record["status"] = "skipped"
                record["error_summary"] = "Products step did not produce a run directory."
                _emit_pipeline(pipeline_run_dir, f"SKIP {step_name}: products step did not produce a run directory")
                _write_manifest(pipeline_run_dir, manifest)
                continue

            _run_step(
                manifest=manifest,
                pipeline_run_dir=pipeline_run_dir,
                plan=plan,
                output_base_dir=output_base_dir,
                repo_root=repo_root,
                dry_run=dry_run,
                executor=executor,
            )
            _write_manifest(pipeline_run_dir, manifest)

            updated_record = _record_for(manifest, step_name)
            if step_name == "products" and _step_is_stageable(updated_record) and updated_record.get("output_run_dirs"):
                product_run_dir = Path(updated_record["output_run_dirs"][-1])

            if not dry_run and updated_record.get("status") not in {"succeeded", "partial"}:
                if step_name == "products":
                    for blocked_step in ("logistics", "reviews", "product_details"):
                        _record_for(manifest, blocked_step)["status"] = "skipped"
                        _record_for(manifest, blocked_step)["error_summary"] = "Skipped because products step did not produce usable artifacts."
                        _emit_pipeline(pipeline_run_dir, f"SKIP {blocked_step}: products step did not produce usable artifacts")
                    blocked_steps.update({"logistics", "reviews", "product_details"})
                if effective_fail_fast:
                    _emit_pipeline(pipeline_run_dir, f"STOP: fail-fast after {step_name}")
                    break
    except KeyboardInterrupt:
        interrupted = True
        _emit_pipeline(pipeline_run_dir, "INTERRUPTED by user")
    finally:
        manifest["suggested_ingestion_commands"] = _suggested_ingestion_commands(manifest)
        parser_status = _final_status(manifest, interrupted=interrupted)
        if stage_to_db and not interrupted:
            _run_staging(
                manifest=manifest,
                pipeline_run_dir=pipeline_run_dir,
                repo_root=repo_root,
                connection_string=connection_string,
                connection_string_source=connection_string_source,
                parser_status=parser_status,
                dry_run=dry_run,
                executor=staging_executor,
            )
        manifest["status"] = _final_status(manifest, interrupted=interrupted)
        manifest["finished_at_utc"] = utc_now_iso()
        _write_manifest(pipeline_run_dir, manifest)
        _emit_pipeline(
            pipeline_run_dir,
            "PIPELINE FINISHED: "
            f"status={manifest['status']} staging_status={(manifest.get('staging') or {}).get('status')} "
            f"staging_commands={len(manifest['suggested_ingestion_commands'])}",
        )

    return pipeline_run_dir


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Manual Market Analytics parser refresh orchestrator.")
    parser.add_argument(
        "--config",
        type=Path,
        default=BASE_DIR / "presets" / "market_refresh_home_goods_demo.json",
        help="Pipeline preset JSON path.",
    )
    parser.add_argument("--mode", default="smoke")
    parser.add_argument("--skip-rank", action="store_true")
    parser.add_argument("--skip-products", action="store_true")
    parser.add_argument("--skip-logistics", action="store_true")
    parser.add_argument("--skip-reviews", action="store_true")
    parser.add_argument("--skip-product-details", action="store_true")
    parser.add_argument("--resume-run-dir", type=Path)
    parser.add_argument("--resume-pipeline-dir", type=Path, help="Alias for --resume-run-dir in streaming batched mode.")
    parser.add_argument("--force-step", choices=STEP_ORDER)
    parser.add_argument("--fail-fast", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--stage-to-db", action="store_true", help="After successful parser run, stage artifacts through the .NET ingestion CLI.")
    parser.add_argument("--connection-string", help="PostgreSQL connection string for opt-in staging.")
    parser.add_argument("--test-run", action="store_true", help="Mark all child parser runs as isolated test runs.")
    parser.add_argument("--test-label", help="Human-readable test run label stored in child run requested_scope.")
    parser.add_argument("--no-ingestion", action="store_true", default=True)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    try:
        staging = _resolve_staging_preflight(
            stage_to_db_arg=args.stage_to_db,
            connection_string_arg=args.connection_string,
        )
        config = PipelineConfig.load(args.config)
    except Exception as exception:
        print(_redact_text(str(exception), [args.connection_string] if getattr(args, "connection_string", None) else []), file=sys.stderr)
        raise SystemExit(2)

    run_dir = run_pipeline(
        config=config,
        mode=args.mode,
        skip_rank=args.skip_rank,
        skip_products=args.skip_products,
        skip_logistics=args.skip_logistics,
        skip_reviews=args.skip_reviews,
        skip_product_details=args.skip_product_details,
        resume_run_dir=args.resume_pipeline_dir or args.resume_run_dir,
        force_step=args.force_step,
        fail_fast=args.fail_fast,
        dry_run=args.dry_run,
        stage_to_db=staging.requested,
        connection_string=staging.connection_string,
        connection_string_source=staging.connection_string_source,
        test_run=args.test_run,
        test_label=args.test_label,
    )
    print(f"Pipeline run directory: {run_dir}")
    manifest = _load_manifest(run_dir)
    staging_manifest = manifest.get("staging") or {}
    if staging_manifest.get("requested") and staging_manifest.get("status") == "failed":
        print(
            "Parser artifacts were produced, but DB staging failed. "
            "Artifacts were kept; rerun staging safely with the ingestion CLI.",
            file=sys.stderr,
        )
        raise SystemExit(2)


if __name__ == "__main__":
    main()
