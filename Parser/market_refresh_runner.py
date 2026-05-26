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
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

from config import BASE_DIR
from manifest import get_git_commit, utc_now_iso


STEP_ORDER = ["rank", "products", "logistics", "reviews"]
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
    markers = ["Rank run directory:", "Logistics run directory:", "Review run directory:", "Run directory:"]
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
        "error_counts": payload.get("error_counts"),
    }


def _child_status(run_dir: Path) -> str | None:
    summary = _manifest_summary(run_dir)
    status = summary.get("status")
    return str(status) if status is not None else None


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
        str(repo_root / "Parser" / "rank_runner.py"),
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
        str(repo_root / "Parser" / "runner.py"),
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
        str(repo_root / "Parser" / "logistics_runner.py"),
        "--products-run-dir",
        str(product_run_dir),
        "--output-dir",
        str(output_base_dir),
        "--marketplace",
        str(logistics.get("marketplace") or "wb"),
    ]
    if logistics.get("dest") is not None:
        command.extend(["--dest", str(logistics["dest"])])
    if logistics.get("limit_products") is not None:
        command.extend(["--limit", str(logistics["limit_products"])])
    if logistics.get("delay_ms") is not None:
        command.extend(["--delay-ms", str(logistics["delay_ms"])])
    if logistics.get("timeout_sec") is not None:
        command.extend(["--timeout-sec", str(logistics["timeout_sec"])])
    if logistics.get("retries") is not None:
        command.extend(["--retries", str(logistics["retries"])])

    for product_id in logistics.get("product_ids") or []:
        command.extend(["--product-id", str(product_id)])

    return PipelineStepPlan(
        step_name="logistics",
        commands=[command],
        enabled=bool(logistics.get("enabled", True)),
        timeout_seconds=logistics.get("timeout_seconds"),
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
    commands: list[list[str]] = []
    for source_subcategory in source_subcategories:
        command = [
            python_executable,
            str(repo_root / "Parser" / "reviews_runner.py"),
            "--config",
            str(_to_abs(reviews.get("config") or "Parser/.env", base_dir=repo_root)),
            "--products-run-dir",
            str(product_run_dir),
        ]
        if reviews.get("limit_products") is not None:
            command.extend(["--limit-products", str(reviews["limit_products"])])
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
        if step.get("status") == "succeeded" and step.get("output_run_dirs")
    )


def _suggested_ingestion_commands(manifest: dict[str, Any]) -> list[list[str]]:
    commands: list[list[str]] = []
    for step in manifest["steps"]:
        if step.get("step_name") == "rank" and step.get("status") == "succeeded":
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
        if step.get("step_name") == "products" and step.get("status") == "succeeded":
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
        if step.get("step_name") == "logistics" and step.get("status") == "succeeded":
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
        if step.get("step_name") == "reviews" and step.get("status") == "succeeded":
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
    return commands


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
    }

    for step_name in STEP_ORDER:
        kind_command = command_by_step.get(step_name)
        if kind_command is None:
            continue

        step = _record_for(manifest, step_name)
        if step.get("status") != "succeeded":
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

    if parser_status != "succeeded":
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

        if run_dir is not None and _child_status(run_dir) not in {None, "succeeded"}:
            record["status"] = "failed"
            record["error_summary"] = f"Child manifest status is {_child_status(run_dir)}."
            _emit_pipeline(pipeline_run_dir, f"FAIL {plan.step_name} {index}/{len(plan.commands)}: {record['error_summary']}")
            break

    record["finished_at_utc"] = utc_now_iso()
    record["exit_code"] = next((code for code in exit_codes if code != 0), exit_codes[-1] if exit_codes else None)
    record["output_run_dirs"] = output_run_dirs
    record["child_manifest_summaries"] = child_manifest_summaries
    if record["status"] == "running":
        record["status"] = "succeeded"
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


def run_pipeline(
    *,
    config: PipelineConfig,
    mode: str,
    skip_rank: bool = False,
    skip_products: bool = False,
    skip_logistics: bool = False,
    skip_reviews: bool = False,
    resume_run_dir: Path | None = None,
    force_step: str | None = None,
    fail_fast: bool = False,
    dry_run: bool = False,
    stage_to_db: bool = False,
    connection_string: str | None = None,
    connection_string_source: str = "missing",
    executor: Callable[..., CommandResult] = _execute_subprocess,
    staging_executor: Callable[..., CommandResult] = _execute_subprocess_capture,
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
        )
    manifest["staging"] = _initial_staging_manifest(
        requested=stage_to_db,
        connection_string_source=connection_string_source,
    )
    manifest["dry_run"] = bool(dry_run)
    _emit_pipeline(
        pipeline_run_dir,
        f"PIPELINE {pipeline_run_id}: mode={mode} dry_run={bool(dry_run)} stage_to_db={bool(stage_to_db)}",
    )

    effective_fail_fast = bool(fail_fast or config.defaults.get("fail_fast"))
    python_executable = _python_executable()
    interrupted = False
    product_run_dir: Path | None = None
    blocked_steps: set[str] = set()

    product_record = _record_for(manifest, "products")
    if product_record.get("status") == "succeeded" and product_record.get("output_run_dirs"):
        product_run_dir = Path(product_record["output_run_dirs"][-1])

    try:
        rank_plan = _build_rank_plan(mode_config, repo_root=repo_root, python_executable=python_executable)
        products_plan = _build_products_plan(mode_config, repo_root=repo_root, python_executable=python_executable)

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
                plan = _build_logistics_plan(
                    mode_config,
                    repo_root=repo_root,
                    python_executable=python_executable,
                    product_run_dir=plan_product_run_dir,
                    output_base_dir=output_base_dir,
                )
                skip_requested = skip_logistics
            else:
                plan_product_run_dir = product_run_dir
                if dry_run and plan_product_run_dir is None and products_plan.enabled:
                    plan_product_run_dir = Path("<products-run-dir>")
                plan = _build_reviews_plan(
                    mode_config,
                    repo_root=repo_root,
                    python_executable=python_executable,
                    product_run_dir=plan_product_run_dir,
                )
                skip_requested = skip_reviews

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

            if resume_run_dir and record.get("status") == "succeeded" and force_step != step_name:
                _emit_pipeline(pipeline_run_dir, f"SKIP {step_name}: already succeeded in resumed pipeline")
                if step_name == "products" and record.get("output_run_dirs"):
                    product_run_dir = Path(record["output_run_dirs"][-1])
                continue

            if force_step and step_name != force_step:
                _emit_pipeline(pipeline_run_dir, f"SKIP {step_name}: force-step={force_step}")
                continue

            if step_name in {"logistics", "reviews"} and product_run_dir is None and not dry_run:
                record["status"] = "skipped"
                record["error_summary"] = "Products step did not produce a run directory."
                _emit_pipeline(pipeline_run_dir, f"SKIP {step_name}: products step did not produce a run directory")
                _write_manifest(pipeline_run_dir, manifest)
                continue

            logistics_record = _record_for(manifest, "logistics")
            if (
                step_name == "reviews"
                and not dry_run
                and not skip_logistics
                and logistics_record.get("status") not in {"succeeded", "skipped"}
            ):
                record["status"] = "skipped"
                record["error_summary"] = "Skipped because logistics step did not succeed."
                _emit_pipeline(pipeline_run_dir, "SKIP reviews: logistics step did not succeed")
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
            if step_name == "products" and updated_record.get("status") == "succeeded" and updated_record.get("output_run_dirs"):
                product_run_dir = Path(updated_record["output_run_dirs"][-1])

            if not dry_run and updated_record.get("status") != "succeeded":
                if step_name == "products":
                    _record_for(manifest, "logistics")["status"] = "skipped"
                    _record_for(manifest, "logistics")["error_summary"] = "Skipped because products step failed."
                    _emit_pipeline(pipeline_run_dir, "SKIP logistics: products step failed")
                    _record_for(manifest, "reviews")["status"] = "skipped"
                    _record_for(manifest, "reviews")["error_summary"] = "Skipped because products step failed."
                    _emit_pipeline(pipeline_run_dir, "SKIP reviews: products step failed")
                    blocked_steps.update({"logistics", "reviews"})
                if step_name == "logistics":
                    _record_for(manifest, "reviews")["status"] = "skipped"
                    _record_for(manifest, "reviews")["error_summary"] = "Skipped because logistics step failed."
                    _emit_pipeline(pipeline_run_dir, "SKIP reviews: logistics step failed")
                    blocked_steps.add("reviews")
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
    parser.add_argument("--mode", choices=["smoke", "bounded", "full"], default="smoke")
    parser.add_argument("--skip-rank", action="store_true")
    parser.add_argument("--skip-products", action="store_true")
    parser.add_argument("--skip-logistics", action="store_true")
    parser.add_argument("--skip-reviews", action="store_true")
    parser.add_argument("--resume-run-dir", type=Path)
    parser.add_argument("--force-step", choices=STEP_ORDER)
    parser.add_argument("--fail-fast", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--stage-to-db", action="store_true", help="After successful parser run, stage artifacts through the .NET ingestion CLI.")
    parser.add_argument("--connection-string", help="PostgreSQL connection string for opt-in staging.")
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
        resume_run_dir=args.resume_run_dir,
        force_step=args.force_step,
        fail_fast=args.fail_fast,
        dry_run=args.dry_run,
        stage_to_db=staging.requested,
        connection_string=staging.connection_string,
        connection_string_source=staging.connection_string_source,
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
