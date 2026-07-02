from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tempfile
import time
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any

PARSER_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = PARSER_ROOT.parent
if str(PARSER_ROOT) not in sys.path:
    sys.path.insert(0, str(PARSER_ROOT))

from app.proxy_mapping import NicheProxyAssignment, ProxyDefinition, ProxyMapping
from app.proxy_preflight import format_proxy_preflight_result, run_proxy_preflight
from config import BASE_DIR


@dataclass(frozen=True)
class ChildProcessPlan:
    proxy_key: str
    source_category: str
    source_subcategory: str
    command: list[str]
    environment: dict[str, str]
    proxy: ProxyDefinition


@dataclass(frozen=True)
class ChildProcessResult:
    proxy_key: str
    source_subcategory: str
    exit_code: int
    duration_seconds: float
    stdout: str
    stderr: str


def _resolve_path(value: str | Path, *, base_dir: Path) -> Path:
    path = Path(value)
    if path.is_absolute():
        return path

    repo_candidate = REPO_ROOT / path
    if repo_candidate.exists():
        return repo_candidate

    return base_dir / path


def _load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _mode_config(config_payload: dict[str, Any], mode: str) -> dict[str, Any]:
    modes = config_payload.get("modes") or {}
    mode_config = modes.get(mode)
    if not isinstance(mode_config, dict):
        raise ValueError(f"Mode '{mode}' is not defined in parser pipeline config.")
    return mode_config


def _proxy_mapping_path(config_path: Path, mode_config: dict[str, Any], proxy_mapping_path: Path | None) -> Path:
    if proxy_mapping_path is not None:
        return proxy_mapping_path

    configured = mode_config.get("proxy_mapping_file") or (mode_config.get("proxy_mapping") or {}).get("file")
    if not configured:
        raise ValueError("Parser supervisor requires proxy_mapping_file in config or --proxy-mapping.")

    return _resolve_path(str(configured), base_dir=config_path.parent)


def _explicit_niches_path(config_path: Path, mode_config: dict[str, Any]) -> Path:
    product_config = mode_config.get("product") or {}
    product_env = product_config.get("env") or {}
    configured = product_env.get("PARSER_EXPLICIT_NICHES_FILE") or os.environ.get("PARSER_EXPLICIT_NICHES_FILE")
    if not configured:
        raise ValueError("Parser supervisor requires PARSER_EXPLICIT_NICHES_FILE in product env.")
    return _resolve_path(str(configured), base_dir=config_path.parent)


def _load_explicit_niches(path: Path) -> dict[tuple[str, str], dict[str, Any]]:
    payload = _load_json(path)
    items = payload.get("niches") if isinstance(payload, dict) else payload
    if not isinstance(items, list):
        raise ValueError("Explicit niches file must contain a JSON array or an object with 'niches' array.")

    result: dict[tuple[str, str], dict[str, Any]] = {}
    for item in items:
        if not isinstance(item, dict):
            raise ValueError("Explicit niche item must be an object.")
        source_category = str(item.get("sourceCategory") or "").strip()
        source_subcategory = str(item.get("sourceSubcategory") or "").strip()
        if source_category and source_subcategory:
            result[(source_category, source_subcategory)] = item
    return result


def _enabled_assignments(
    proxy_mapping: ProxyMapping,
    *,
    only_proxy: str | None,
    only_subcategory: str | None,
) -> list[NicheProxyAssignment]:
    assignments: list[NicheProxyAssignment] = []
    seen_proxy_keys: set[str] = set()
    for assignment in proxy_mapping.assignments:
        if not assignment.enabled:
            continue
        if only_proxy and assignment.proxy_key != only_proxy:
            continue
        if only_subcategory and assignment.source_subcategory != only_subcategory:
            continue
        if assignment.proxy_key in seen_proxy_keys:
            raise ValueError(
                f"Proxy {assignment.proxy_key!r} is assigned to more than one enabled niche. "
                "Run one proxy per niche or disable duplicate assignments."
            )
        if assignment.proxy_key not in proxy_mapping.proxies:
            raise ValueError(f"Proxy {assignment.proxy_key!r} is not defined in proxy mapping.")
        seen_proxy_keys.add(assignment.proxy_key)
        assignments.append(assignment)
    return assignments


def build_child_process_plans(
    *,
    config_path: Path,
    mode: str,
    parser_instance_id: str,
    outbox_root_dir: Path,
    proxy_mapping_path: Path | None = None,
    smoke_max_batches: int | None = None,
    only_proxy: str | None = None,
    only_subcategory: str | None = None,
    python_executable: str | None = None,
    parser_cycle_id: str | None = None,
    cycle_kind: str | None = None,
) -> list[ChildProcessPlan]:
    config_path = config_path.resolve()
    payload = _load_json(config_path)
    mode_config = _mode_config(payload, mode)
    product_env = {
        str(key): str(value)
        for key, value in ((mode_config.get("product") or {}).get("env") or {}).items()
        if value is not None
    }
    mapping_path = _proxy_mapping_path(config_path, mode_config, proxy_mapping_path)
    proxy_mapping = ProxyMapping.load_with_local_override(mapping_path)
    explicit_niches = _load_explicit_niches(_explicit_niches_path(config_path, mode_config))
    python_path = python_executable or sys.executable
    effective_cycle_id = (parser_cycle_id or "").strip() or f"parser-cycle-{uuid.uuid4().hex}"
    effective_cycle_kind = (cycle_kind or "").strip() or (
        "diagnostic" if only_proxy or only_subcategory else "production"
    )

    plans: list[ChildProcessPlan] = []
    for assignment in _enabled_assignments(
        proxy_mapping,
        only_proxy=(only_proxy or "").strip() or None,
        only_subcategory=(only_subcategory or "").strip() or None,
    ):
        key = (assignment.source_category, assignment.source_subcategory)
        if key not in explicit_niches:
            raise ValueError(
                f"Enabled proxy assignment {assignment.source_category!r} / "
                f"{assignment.source_subcategory!r} was not found in explicit niches."
            )

        command = [
            python_path,
            str(PARSER_ROOT / "app" / "cycle_runner.py"),
            "--config",
            str(config_path),
            "--mode",
            mode,
            "--only-proxy",
            assignment.proxy_key,
            "--only-subcategory",
            assignment.source_subcategory,
            "--skip-rank",
        ]
        if smoke_max_batches is not None and smoke_max_batches > 0:
            command.extend(["--smoke-max-batches", str(smoke_max_batches)])

        plans.append(
            ChildProcessPlan(
                proxy_key=assignment.proxy_key,
                source_category=assignment.source_category,
                source_subcategory=assignment.source_subcategory,
                command=command,
                environment={
                    **product_env,
                    "PARSER_INSTANCE_ID": parser_instance_id,
                    "PARSER_PROXY_KEY": assignment.proxy_key,
                    "PARSER_ONLY_PROXY": assignment.proxy_key,
                    "PARSER_OUTBOX_DIR": str(outbox_root_dir / assignment.proxy_key),
                    "PARSER_PROXY_PREFLIGHT_ENABLED": "true",
                    "PARSER_SKIP_PIPELINE_PROXY_PREFLIGHT": "true",
                    "PARSER_SUPERVISOR_CHILD": "1",
                    "PARSER_CYCLE_ID": effective_cycle_id,
                    "PARSER_CYCLE_KIND": effective_cycle_kind,
                },
                proxy=proxy_mapping.proxies[assignment.proxy_key],
            )
        )

    if not plans:
        raise ValueError("No enabled proxy/niche assignments matched supervisor filters.")
    return plans


def _run_preflight(plans: list[ChildProcessPlan]) -> dict[str, dict[str, str]]:
    proxies = [plan.proxy for plan in plans if plan.proxy.type != "direct"]
    if not proxies:
        print("[proxy-supervisor] proxy preflight skipped: no HTTP proxies in enabled assignments.")
        return {}

    result = run_proxy_preflight(proxies)
    items_by_proxy: dict[str, dict[str, str]] = {}
    for item in format_proxy_preflight_result(result):
        items_by_proxy[str(item["proxyKey"])] = {
            "egressIp": str(item.get("egressIp") or ""),
            "tokenRef": str(item.get("tokenRef") or ""),
            "status": str(item.get("status") or "valid"),
        }
        print(
            "[proxy-supervisor] preflight ok "
            f"proxy={item['proxyKey']} ip={item['egressIp']} tokenRef={item['tokenRef']}"
        )
    return items_by_proxy


def run_child_processes(plans: list[ChildProcessPlan]) -> list[ChildProcessResult]:
    results: list[ChildProcessResult] = []
    with tempfile.TemporaryDirectory(prefix="parser-supervisor-") as temp:
        temp_dir = Path(temp)
        running: list[tuple[ChildProcessPlan, subprocess.Popen, float, Path, Path]] = []
        for plan in plans:
            env = os.environ.copy()
            env.update(plan.environment)
            stdout_path = temp_dir / f"{plan.proxy_key}.stdout.log"
            stderr_path = temp_dir / f"{plan.proxy_key}.stderr.log"
            print(
                "[proxy-supervisor] starting "
                f"proxy={plan.proxy_key} niche={plan.source_subcategory!r} outbox={plan.environment['PARSER_OUTBOX_DIR']}"
            )
            stdout_file = stdout_path.open("w", encoding="utf-8", errors="replace")
            stderr_file = stderr_path.open("w", encoding="utf-8", errors="replace")
            try:
                process = subprocess.Popen(
                    plan.command,
                    cwd=str(REPO_ROOT),
                    env=env,
                    stdout=stdout_file,
                    stderr=stderr_file,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                )
            finally:
                stdout_file.close()
                stderr_file.close()
            running.append((plan, process, time.monotonic(), stdout_path, stderr_path))

        try:
            for plan, process, started_at, stdout_path, stderr_path in running:
                process.wait()
                duration = time.monotonic() - started_at
                stdout = stdout_path.read_text(encoding="utf-8", errors="replace") if stdout_path.exists() else ""
                stderr = stderr_path.read_text(encoding="utf-8", errors="replace") if stderr_path.exists() else ""
                if stdout:
                    print(f"[proxy-supervisor][{plan.proxy_key}] stdout:\n{stdout}")
                if stderr:
                    print(f"[proxy-supervisor][{plan.proxy_key}] stderr:\n{stderr}", file=sys.stderr)
                print(
                    "[proxy-supervisor] finished "
                    f"proxy={plan.proxy_key} exit_code={process.returncode} duration_seconds={duration:.1f}"
                )
                results.append(
                    ChildProcessResult(
                        proxy_key=plan.proxy_key,
                        source_subcategory=plan.source_subcategory,
                        exit_code=int(process.returncode or 0),
                        duration_seconds=duration,
                        stdout=stdout,
                        stderr=stderr,
                    )
                )
        except KeyboardInterrupt:
            for _, process, _, _, _ in running:
                if process.poll() is None:
                    process.terminate()
            raise

    return results


def run_supervisor(
    *,
    config_path: Path,
    mode: str,
    parser_instance_id: str,
    outbox_root_dir: Path,
    proxy_mapping_path: Path | None = None,
    smoke_max_batches: int | None = None,
    only_proxy: str | None = None,
    only_subcategory: str | None = None,
    python_executable: str | None = None,
    skip_proxy_preflight: bool = False,
) -> list[ChildProcessResult]:
    parser_cycle_id = (os.environ.get("PARSER_CYCLE_ID") or "").strip() or f"parser-cycle-{uuid.uuid4().hex}"
    cycle_kind = (os.environ.get("PARSER_CYCLE_KIND") or "").strip() or (
        "diagnostic" if only_proxy or only_subcategory else "production"
    )
    plans = build_child_process_plans(
        config_path=config_path,
        mode=mode,
        proxy_mapping_path=proxy_mapping_path,
        parser_instance_id=parser_instance_id,
        outbox_root_dir=outbox_root_dir,
        smoke_max_batches=smoke_max_batches,
        only_proxy=only_proxy,
        only_subcategory=only_subcategory,
        python_executable=python_executable,
        parser_cycle_id=parser_cycle_id,
        cycle_kind=cycle_kind,
    )
    if not skip_proxy_preflight:
        preflight_items = _run_preflight(plans)
        for plan in plans:
            item = preflight_items.get(plan.proxy_key)
            if item:
                plan.environment["PARSER_PROXY_EGRESS_IP"] = item["egressIp"]
                plan.environment["PARSER_PROXY_TOKEN_REF"] = item["tokenRef"]
                plan.environment["PARSER_PROXY_SESSION_STATUS"] = item["status"]
    return run_child_processes(plans)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run parser niches in parallel proxy-scoped child processes.")
    parser.add_argument(
        "--config",
        type=Path,
        default=BASE_DIR / "presets" / "production" / "market_refresh_selected_niches_batched.prod.json",
    )
    parser.add_argument("--mode", default="batched_full_enrichment")
    parser.add_argument("--proxy-mapping", type=Path)
    parser.add_argument("--smoke-max-batches", type=int)
    parser.add_argument("--only-proxy")
    parser.add_argument("--only-subcategory")
    parser.add_argument("--parser-instance-id", default=os.environ.get("PARSER_INSTANCE_ID") or "parser-local-01")
    parser.add_argument(
        "--outbox-root",
        type=Path,
        default=Path(os.environ.get("PARSER_OUTBOX_DIR") or BASE_DIR / "output" / "outbox"),
    )
    parser.add_argument("--python-executable", default=sys.executable)
    parser.add_argument("--skip-proxy-preflight", action="store_true")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    try:
        results = run_supervisor(
            config_path=args.config,
            mode=args.mode,
            proxy_mapping_path=args.proxy_mapping,
            parser_instance_id=args.parser_instance_id,
            outbox_root_dir=args.outbox_root,
            smoke_max_batches=args.smoke_max_batches,
            only_proxy=args.only_proxy,
            only_subcategory=args.only_subcategory,
            python_executable=args.python_executable,
            skip_proxy_preflight=args.skip_proxy_preflight,
        )
    except Exception as exception:
        print(f"[proxy-supervisor] failed: {exception}", file=sys.stderr)
        raise SystemExit(1)

    successful = [result for result in results if result.exit_code == 0]
    failed = [result for result in results if result.exit_code != 0]
    print(
        "[proxy-supervisor] summary "
        f"children={len(results)} successful={len(successful)} failed={len(failed)}"
    )
    if failed and not successful:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
