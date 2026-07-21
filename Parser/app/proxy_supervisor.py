from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
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
from app.runtime_proxy_assignments import load_runtime_proxy_mapping, runtime_proxy_assignments_url
from config import BASE_DIR


def _safe_log_name(value: str) -> str:
    return "".join(ch if ch.isalnum() or ch in {"-", "_", "."} else "_" for ch in value).strip("._") or "unknown"


def _rank_context_id(assignment: NicheProxyAssignment) -> str:
    return _safe_log_name(f"{assignment.proxy_key}_{assignment.source_subcategory}")


def _human_query_from_raw_search_query(raw_search_query: object, fallback: object = "") -> str:
    query = str(raw_search_query or "").strip()
    if query:
        parts = query.split(maxsplit=1)
        if parts and parts[0].startswith("menu_") and len(parts) > 1:
            return parts[1].strip()
        if not parts or not parts[0].startswith("menu_"):
            return query

    return str(fallback or "").strip()


def _append_runner_log(log_path: Path, message: str) -> None:
    log_path.parent.mkdir(parents=True, exist_ok=True)
    timestamp = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    with log_path.open("a", encoding="utf-8", errors="replace") as file:
        file.write(f"{timestamp} {message}\n")


def _tail_text(value: str, *, max_length: int = 4000) -> str:
    if len(value) <= max_length:
        return value
    return value[-max_length:]


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
    return json.loads(path.read_text(encoding="utf-8-sig"))


def _load_launch_proxy_mapping(path: Path) -> ProxyMapping:
    payload = _load_json(path)
    mapping_payload = payload.get("proxyMapping") if isinstance(payload.get("proxyMapping"), dict) else payload
    if not isinstance(mapping_payload, dict):
        raise ValueError("Launch context must contain proxy mapping data.")
    return ProxyMapping.from_dict(mapping_payload)


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


def _explicit_niches_from_assignments(assignments: list[NicheProxyAssignment]) -> dict[tuple[str, str], dict[str, Any]]:
    result: dict[tuple[str, str], dict[str, Any]] = {}
    for assignment in assignments:
        if not assignment.source_category or not assignment.source_subcategory:
            continue
        if assignment.wb_category_id is None or not assignment.source_path or not assignment.search_query:
            continue
        result[(assignment.source_category, assignment.source_subcategory)] = {
            "wbCategoryId": assignment.wb_category_id,
            "sourceCategory": assignment.source_category,
            "sourceSubcategory": assignment.source_subcategory,
            "sourcePath": assignment.source_path,
            "searchQuery": assignment.search_query,
            "parserSearchText": assignment.parser_search_text or assignment.source_subcategory,
            "scopeAcceptanceMode": assignment.scope_acceptance_mode or "menu_token_trusted",
            "allowedSubjectIds": list(assignment.allowed_subject_ids),
        }
    return result


def _write_runtime_files(
    *,
    proxy_mapping: ProxyMapping,
    explicit_niches: dict[tuple[str, str], dict[str, Any]],
    outbox_root_dir: Path,
    parser_cycle_id: str,
) -> tuple[Path, Path, Path]:
    runtime_dir = outbox_root_dir / "_runtime"
    runtime_dir.mkdir(parents=True, exist_ok=True)
    suffix = "".join(ch if ch.isalnum() or ch in {"-", "_"} else "_" for ch in parser_cycle_id)[:120]
    mapping_path = runtime_dir / f"proxy_mapping.{suffix}.json"
    niches_path = runtime_dir / f"explicit_niches.{suffix}.json"
    rank_config_path = runtime_dir / f"rank_config.{suffix}.json"

    mapping_payload = {
        "defaultProxy": _proxy_to_dict(proxy_mapping.default_proxy),
        "proxies": [_proxy_to_dict(proxy) for key, proxy in sorted(proxy_mapping.proxies.items()) if key != proxy_mapping.default_proxy.key],
        "niches": [
            {
                "wbCategoryId": item.get("wbCategoryId"),
                "sourceCategory": item.get("sourceCategory"),
                "sourceSubcategory": item.get("sourceSubcategory"),
                "sourcePath": item.get("sourcePath"),
                "searchQuery": item.get("searchQuery"),
                "parserSearchText": item.get("parserSearchText"),
                "scopeAcceptanceMode": item.get("scopeAcceptanceMode"),
                "allowedSubjectIds": item.get("allowedSubjectIds") or [],
                "proxyKey": assignment.proxy_key,
                "enabled": assignment.enabled,
            }
            for assignment in proxy_mapping.assignments
            for item in [explicit_niches.get((assignment.source_category, assignment.source_subcategory), {})]
        ],
    }
    niches_payload = {"niches": list(explicit_niches.values())}
    rank_contexts: list[dict[str, Any]] = []
    for assignment in proxy_mapping.assignments:
        if not assignment.enabled:
            continue
        item = explicit_niches.get((assignment.source_category, assignment.source_subcategory), {})
        if not item:
            continue
        wb_category_id = item.get("wbCategoryId")
        source_path = item.get("sourcePath")
        raw_search_query = str(item.get("searchQuery") or "").strip()
        human_query = _human_query_from_raw_search_query(
            raw_search_query,
            item.get("parserSearchText") or item.get("sourceSubcategory") or assignment.source_subcategory,
        )
        if wb_category_id is None or not raw_search_query:
            raise ValueError(
                "Service rank context requires wbCategoryId and searchQuery for "
                f"{assignment.source_category!r} / {assignment.source_subcategory!r}."
            )
        rank_contexts.append(
            {
                "id": _rank_context_id(assignment),
                "type": "category_result",
                "source_category": item.get("sourceCategory") or assignment.source_category,
                "source_subcategory": item.get("sourceSubcategory") or assignment.source_subcategory,
                "source_path": source_path,
                "source_region_dest": item.get("sourceRegionDest") or "12354108",
                "query": raw_search_query,
                "raw_search_query": raw_search_query,
                "human_query": human_query,
                "wb_category_id": wb_category_id,
                "sort": "popular",
                "filters": {},
            }
        )
    rank_payload = {
        "defaults": {
            "marketplace": "wildberries",
            "source_region_dest": "12354108",
            "output_base_dir": str(runtime_dir / "rank"),
            "top_n": 1000,
            "page_size": 100,
            "sort": "popular",
            "timeout_seconds": 10,
            "max_retries": 2,
            "request_delay_min_seconds": 2.0,
            "request_delay_max_seconds": 5.0,
            "acquire_token": True,
            "fail_fast": False,
        },
        "contexts": rank_contexts,
    }
    mapping_path.write_text(json.dumps(mapping_payload, ensure_ascii=False, indent=2), encoding="utf-8")
    niches_path.write_text(json.dumps(niches_payload, ensure_ascii=False, indent=2), encoding="utf-8")
    rank_config_path.write_text(json.dumps(rank_payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return mapping_path, niches_path, rank_config_path


def _proxy_to_dict(proxy: ProxyDefinition) -> dict[str, Any]:
    return {
        "key": proxy.key,
        "type": proxy.type,
        "baseUrl": proxy.base_url,
        "socks5Url": proxy.socks5_url,
        "credentials": proxy.credentials,
        "healthcheck": proxy.healthcheck,
        "rateLimitPerMinute": proxy.rate_limit_per_minute,
        "cooldownSeconds": proxy.cooldown_seconds,
    }


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
    launch_context_path: Path | None = None,
) -> list[ChildProcessPlan]:
    config_path = config_path.resolve()
    payload = _load_json(config_path)
    mode_config = _mode_config(payload, mode)
    effective_cycle_id = (parser_cycle_id or "").strip() or f"parser-cycle-{uuid.uuid4().hex}"
    effective_cycle_kind = (cycle_kind or "").strip() or (
        "diagnostic" if only_proxy or only_subcategory else "production"
    )
    product_env = {
        str(key): str(value)
        for key, value in ((mode_config.get("product") or {}).get("env") or {}).items()
        if value is not None
    }
    for env_key in (
        "PARSER_BATCH_QUEUE_URL",
        "PARSER_OUTPUT_BASE_DIR",
        "PARSER_RATE_LIMIT_STATE_DIR",
    ):
        env_value = os.environ.get(env_key)
        if env_value:
            product_env[env_key] = env_value
    if launch_context_path is not None:
        resolved_launch_context_path = launch_context_path.resolve()
        print(f"[proxy-supervisor] loading immutable launch context: {resolved_launch_context_path}")
        proxy_mapping = _load_launch_proxy_mapping(resolved_launch_context_path)
        explicit_niches = _explicit_niches_from_assignments(proxy_mapping.assignments)
        if not explicit_niches:
            raise ValueError("Launch context does not contain launchable explicit WB niches.")
        runtime_mapping_path, runtime_niches_path, runtime_rank_config_path = _write_runtime_files(
            proxy_mapping=proxy_mapping,
            explicit_niches=explicit_niches,
            outbox_root_dir=outbox_root_dir,
            parser_cycle_id=effective_cycle_id,
        )
        product_env["PARSER_PROXY_MAPPING_FILE"] = str(runtime_mapping_path)
        product_env["PARSER_EXPLICIT_NICHES_FILE"] = str(runtime_niches_path)
        product_env["PARSER_RUNTIME_RANK_CONFIG_FILE"] = str(runtime_rank_config_path)
        product_env["PARSER_LAUNCH_CONTEXT_FILE"] = str(resolved_launch_context_path)
    else:
        runtime_url = runtime_proxy_assignments_url(parser_instance_id=parser_instance_id)
        if runtime_url and proxy_mapping_path is None:
            print(f"[proxy-supervisor] loading proxy assignments from backend: {runtime_url}")
            proxy_mapping = load_runtime_proxy_mapping(runtime_url)
            explicit_niches = _explicit_niches_from_assignments(proxy_mapping.assignments)
            if not explicit_niches:
                raise ValueError("Backend proxy assignments do not contain launchable explicit WB niches.")
            runtime_mapping_path, runtime_niches_path, runtime_rank_config_path = _write_runtime_files(
                proxy_mapping=proxy_mapping,
                explicit_niches=explicit_niches,
                outbox_root_dir=outbox_root_dir,
                parser_cycle_id=effective_cycle_id,
            )
            product_env["PARSER_PROXY_MAPPING_FILE"] = str(runtime_mapping_path)
            product_env["PARSER_EXPLICIT_NICHES_FILE"] = str(runtime_niches_path)
            product_env["PARSER_RUNTIME_RANK_CONFIG_FILE"] = str(runtime_rank_config_path)
        else:
            mapping_path = _proxy_mapping_path(config_path, mode_config, proxy_mapping_path)
            proxy_mapping = ProxyMapping.load_with_local_override(mapping_path)
            explicit_niches = _load_explicit_niches(_explicit_niches_path(config_path, mode_config))
            runtime_mapping_path, runtime_niches_path, runtime_rank_config_path = _write_runtime_files(
                proxy_mapping=proxy_mapping,
                explicit_niches=explicit_niches,
                outbox_root_dir=outbox_root_dir,
                parser_cycle_id=effective_cycle_id,
            )
            product_env["PARSER_PROXY_MAPPING_FILE"] = str(runtime_mapping_path)
            product_env["PARSER_EXPLICIT_NICHES_FILE"] = str(runtime_niches_path)
            product_env["PARSER_RUNTIME_RANK_CONFIG_FILE"] = str(runtime_rank_config_path)
    python_path = python_executable or sys.executable

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
        runtime_dir = outbox_root_dir / "_runtime"
        session_scope = f"{effective_cycle_id}/{assignment.proxy_key}"

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
                    "PARSER_RUNTIME_RANK_CONTEXT_ID": _rank_context_id(assignment),
                    "PARSER_OUTBOX_DIR": str(outbox_root_dir / assignment.proxy_key),
                    "PARSER_PROXY_PREFLIGHT_ENABLED": "true",
                    "PARSER_SKIP_PIPELINE_PROXY_PREFLIGHT": "true",
                    "PARSER_SUPERVISOR_CHILD": "1",
                    "PARSER_CYCLE_ID": effective_cycle_id,
                    "PARSER_CYCLE_KIND": effective_cycle_kind,
                    "PARSER_SESSION_SCOPE": session_scope,
                    "PARSER_BROWSER_PROFILE_DIR": str(runtime_dir / "browser_profiles"),
                    "PARSER_BROWSER_SESSION_CACHE_DIR": str(runtime_dir / "browser_sessions"),
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
    first_plan = plans[0]
    outbox_root = Path(first_plan.environment["PARSER_OUTBOX_DIR"]).parent
    cycle_id = first_plan.environment.get("PARSER_CYCLE_ID") or f"parser-cycle-{uuid.uuid4().hex}"
    log_dir = outbox_root / "_runtime" / "logs" / _safe_log_name(cycle_id)
    runner_log_path = log_dir / "runner.log"
    _append_runner_log(
        runner_log_path,
        f"supervisor started children={len(plans)} logDir={log_dir}",
    )
    print(f"[proxy-supervisor] runner log: {runner_log_path}")

    running: list[tuple[ChildProcessPlan, subprocess.Popen, float, Path, Path]] = []
    for plan in plans:
        env = os.environ.copy()
        env.update(plan.environment)
        proxy_log_dir = log_dir / _safe_log_name(plan.proxy_key)
        proxy_log_dir.mkdir(parents=True, exist_ok=True)
        stdout_path = proxy_log_dir / "stdout.log"
        stderr_path = proxy_log_dir / "stderr.log"
        _append_runner_log(
            runner_log_path,
            "starting "
            f"proxy={plan.proxy_key} niche={plan.source_subcategory!r} "
            f"outbox={plan.environment['PARSER_OUTBOX_DIR']} "
            f"stdout={stdout_path} stderr={stderr_path}",
        )
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
            _append_runner_log(
                runner_log_path,
                "finished "
                f"proxy={plan.proxy_key} exitCode={process.returncode} "
                f"durationSeconds={duration:.1f} stdout={stdout_path} stderr={stderr_path}",
            )
            if stdout:
                print(
                    f"[proxy-supervisor][{plan.proxy_key}] stdout saved to {stdout_path}; "
                    f"tail:\n{_tail_text(stdout)}"
                )
            if stderr:
                print(
                    f"[proxy-supervisor][{plan.proxy_key}] stderr saved to {stderr_path}; "
                    f"tail:\n{_tail_text(stderr)}",
                    file=sys.stderr,
                )
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
        _append_runner_log(runner_log_path, "supervisor interrupted; terminating running children")
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
    launch_context_path: Path | None = None,
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
        launch_context_path=launch_context_path,
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
    parser.add_argument("--launch-context", type=Path)
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
            launch_context_path=args.launch_context,
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
