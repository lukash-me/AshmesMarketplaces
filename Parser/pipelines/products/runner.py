from __future__ import annotations

import argparse
import asyncio
import json
import os
import random
import re
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

PARSER_ROOT = Path(__file__).resolve().parents[2]
if str(PARSER_ROOT) not in sys.path:
    sys.path.insert(0, str(PARSER_ROOT))

try:
    import httpx  # noqa: F401
    import pydantic  # noqa: F401
    import pyexcel  # noqa: F401
    import requests
    from loguru import logger
except ModuleNotFoundError as exception:
    missing = exception.name
    print(
        "Parser dependencies are missing in the current Python environment.\n"
        f"Missing module: {missing}\n\n"
        "Use the project parser virtual environment:\n"
        r"  .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\products\runner.py --smoke-only"
        "\n\nOr install dependencies into the current environment:\n"
        r"  python -m pip install -r requirements.txt",
        file=sys.stderr,
    )
    raise SystemExit(1) from exception

from add_price_wb_wallet import add_price_with_wb_wallet
from CategoriesParser import CategoriesParser
from config import BASE_DIR, ParserConfig
from exporters import append_jsonl, product_to_canonical_row, write_csv, write_xlsx
from images_parser import add_images
from manifest import CategoryResult, RunManifest, get_git_commit, utc_now_iso
from models import Items
from run_scope import parser_run_scope_from_env
from SearchPhraseParser import SearchPhraseParser
from WbCatalogFetcher import WbCatalogFetcher
from app.browser_sessions import COOKIE_NAME, get_cookies_for_proxy, session_snapshot
from app.proxy_mapping import ProxyMapping
from app.proxy_rate_limiter import global_sync_proxy_rate_limiter
from app.proxy_transport import http_proxy_url_from_definition, requests_proxy_kwargs
from app.wb_proxy_preflight import run_wb_proxy_preflight


STATIC_MENU_URL = "https://static-basket-01.wbbasket.ru/vol0/data/main-menu-ru-ru-v3.json"


@dataclass(frozen=True)
class ProductDiscoveryBatch:
    batch_index: int
    rows: list[dict[str, Any]]
    parent_run_dir: Path
    parent_parser_run_id: str


@dataclass(frozen=True)
class ProductDiscoveryBatchResult:
    status: str
    reason: str | None = None


def _load_proxy_mapping() -> ProxyMapping | None:
    path = os.environ.get("PARSER_PROXY_MAPPING_FILE")
    if not path:
        return None
    return ProxyMapping.load_with_local_override(path)


def _proxy_url_for(mapping: ProxyMapping | None, *, source_category: str, source_subcategory: str | None) -> str | None:
    if mapping is None:
        return None
    resolved = mapping.resolve(source_category, source_subcategory)
    return http_proxy_url_from_definition(resolved.proxy)


def _resolved_proxy_for(mapping: ProxyMapping | None, *, source_category: str, source_subcategory: str | None):
    if mapping is None:
        return None
    return mapping.resolve(source_category, source_subcategory)


def _slug(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-zА-Яа-яЁё0-9_-]+", "_", value, flags=re.UNICODE).strip("_")
    return cleaned[:80] or "subcategory"


def _explicit_niches_file() -> Path | None:
    configured = os.environ.get("PARSER_EXPLICIT_NICHES_FILE")
    if not configured:
        return None

    path = Path(configured)
    return path if path.is_absolute() else PARSER_ROOT.parent / path


def _load_explicit_niches() -> list[dict[str, Any]]:
    path = _explicit_niches_file()
    if path is None:
        return []
    if not path.exists():
        raise FileNotFoundError(f"Explicit niches file was not found: {path}")

    payload = json.loads(path.read_text(encoding="utf-8"))
    items = payload.get("niches") if isinstance(payload, dict) else payload
    if not isinstance(items, list):
        raise ValueError("Explicit niches file must contain a JSON array or an object with 'niches' array.")

    selected: list[dict[str, Any]] = []
    seen_ids: set[str] = set()
    for item in items:
        if not isinstance(item, dict):
            raise ValueError("Explicit niche item must be an object.")

        wb_category_id = str(item.get("wbCategoryId") or item.get("id") or "").strip()
        source_category = str(item.get("sourceCategory") or "").strip()
        source_subcategory = str(item.get("sourceSubcategory") or "").strip()
        search_query = str(item.get("searchQuery") or "").strip()
        parser_search_text = str(item.get("parserSearchText") or source_subcategory).strip()
        source_path = str(item.get("sourcePath") or "").strip()

        if not wb_category_id:
            raise ValueError("Explicit niche wbCategoryId is required.")
        if wb_category_id in seen_ids:
            raise ValueError(f"Duplicate explicit niche wbCategoryId: {wb_category_id}")
        if not source_category or not source_subcategory or not search_query:
            raise ValueError(f"Explicit niche {wb_category_id} requires sourceCategory, sourceSubcategory and searchQuery.")
        if not parser_search_text:
            raise ValueError(f"Explicit niche {wb_category_id} requires parserSearchText or sourceSubcategory.")

        seen_ids.add(wb_category_id)
        selected.append(
            {
                "id": int(wb_category_id),
                "name": source_subcategory,
                "searchQuery": search_query,
                "parserSearchText": parser_search_text,
                "sourceCategory": source_category,
                "sourcePath": source_path,
            }
        )

    return selected


def _resolve_explicit_niches_against_wb_menu(selected: list[dict[str, Any]]) -> list[dict[str, Any]]:
    if not selected:
        return selected

    wb_leaves = CategoriesParser().parse()
    available = {(str(item.get("id") or "").strip(), str(item.get("searchQuery") or "").strip()): item for item in wb_leaves}
    missing = [
        f"{item.get('id')}:{item.get('searchQuery')}"
        for item in selected
        if (str(item.get("id") or "").strip(), str(item.get("searchQuery") or "").strip()) not in available
    ]
    if missing:
        raise ValueError(f"Explicit niches were not found in Wildberries catalog menu: {missing}")

    resolved: list[dict[str, Any]] = []
    for item in selected:
        wb_leaf = available[(str(item.get("id") or "").strip(), str(item.get("searchQuery") or "").strip())]
        source_category = str(wb_leaf.get("sourceCategory") or item.get("sourceCategory") or "").strip()
        source_subcategory = str(item.get("name") or item.get("sourceSubcategory") or wb_leaf.get("sourceSubcategory") or wb_leaf.get("name") or "").strip()
        source_path = str(wb_leaf.get("sourcePath") or item.get("sourcePath") or source_subcategory).strip()
        resolved.append(
            {
                **item,
                "name": source_subcategory,
                "sourceCategory": source_category,
                "sourceSubcategory": source_subcategory,
                "sourcePath": source_path,
                "parserSearchText": str(item.get("parserSearchText") or source_subcategory).strip(),
            }
        )

    return resolved


def _category_source_category(config: ParserConfig, selected_category: dict[str, Any]) -> str:
    return str(selected_category.get("sourceCategory") or config.parent_category).strip()


def _make_run_id() -> str:
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    commit = get_git_commit(BASE_DIR.parent)
    proxy_scope = os.environ.get("PARSER_ONLY_PROXY", "").strip()
    if proxy_scope:
        safe_proxy_scope = re.sub(r"[^A-Za-z0-9]+", "_", proxy_scope).strip("_") or "proxy"
        return f"wb_products_{timestamp}_{safe_proxy_scope}_{os.getpid()}_{commit}"
    return f"wb_products_{timestamp}_{commit}"


def _env_float(name: str, default: float) -> float:
    value = os.environ.get(name)
    if value is None or not value.strip():
        return default
    try:
        return float(value)
    except ValueError:
        return default


def _wait_after_price_split_before_catalog_fetch(proxy_key: str) -> float:
    seconds = _env_float("PARSER_PRICE_SPLIT_TO_CATALOG_DELAY_SECONDS", 0.0)
    jitter = _env_float("PARSER_PRICE_SPLIT_TO_CATALOG_JITTER_SECONDS", 0.0)
    delay = max(0.0, seconds) + random.uniform(0.0, max(0.0, jitter))
    if delay > 0:
        logger.info(
            "Waiting after price split before catalog fetch proxy={} delay_seconds={:.1f}",
            proxy_key,
            delay,
        )
        time.sleep(delay)
    return delay


def _make_manifest(config: ParserConfig, run_dir: Path, parser_run_id: str) -> RunManifest:
    manifest = RunManifest(
        parser_run_id=parser_run_id,
        run_dir=run_dir,
        config_snapshot=config.safe_snapshot(),
        parser_version=get_git_commit(BASE_DIR.parent),
        marketplace=config.marketplace,
        source_region_dest=config.source_region_dest,
        requested_scope={
            "parent_category": config.parent_category,
            "subcategory_allowlist": config.subcategory_allowlist,
            "explicit_niches_file": str(_explicit_niches_file()) if _explicit_niches_file() else None,
            "product_fetch_mode": config.product_fetch_mode,
            **parser_run_scope_from_env(),
        },
    )
    manifest.set_output_files(
        {
            "manifest": run_dir / "manifest.json",
            "products_jsonl": run_dir / "products.jsonl",
            "products_csv": run_dir / "products.csv",
            "errors_jsonl": run_dir / "errors.jsonl",
            "runner_log": run_dir / "runner.log",
            **({"products_xlsx": run_dir / "products.xlsx"} if config.include_xlsx else {}),
        }
    )
    return manifest


def _selected_subcategories(config: ParserConfig) -> list[dict]:
    explicit = _load_explicit_niches()
    if explicit:
        resolved = _resolve_explicit_niches_against_wb_menu(explicit)
        allowlist = (
            [str(value).strip() for value in config.subcategory_allowlist if str(value).strip()]
            if os.environ.get("PARSER_SUBCATEGORY_ALLOWLIST")
            else []
        )
        if not allowlist:
            return resolved

        matched: set[str] = set()
        selected: list[dict] = []
        for item in resolved:
            names = {
                str(item.get("id") or "").casefold(),
                str(item.get("name") or "").casefold(),
                str(item.get("sourceSubcategory") or "").casefold(),
                str(item.get("sourcePath") or "").casefold(),
            }
            for allowed in allowlist:
                allowed_key = allowed.casefold()
                if allowed_key in names:
                    selected.append(item)
                    matched.add(allowed_key)
                    break

        missing = sorted({name.casefold() for name in allowlist} - matched)
        if missing:
            raise ValueError(f"Configured explicit niches were not found: {missing}")
        return selected

    categories = CategoriesParser().parse([config.parent_category])
    selected: list[dict] = []
    matched: set[str] = set()

    for allowed_name in config.subcategory_allowlist:
        allowed_key = allowed_name.casefold()
        for category in categories:
            category_name = category.get("name")
            if not category_name:
                continue

            if str(category_name).casefold() == allowed_key:
                selected.append(category)
                matched.add(allowed_key)
                break

    missing = sorted({name.casefold() for name in config.subcategory_allowlist} - matched)
    if missing:
        raise ValueError(
            "Configured subcategories were not found under parent category "
            f"{config.parent_category!r}: {missing}"
        )

    if not selected:
        raise ValueError("No subcategories selected. Full-category traversal is disabled.")

    return selected


def _network_smoke_check(
        config: ParserConfig,
        manifest: RunManifest,
        selected: list[dict]) -> dict:
    result = {
        "status": "not_attempted",
        "static_menu": {"status": "not_attempted"},
        "filters_probe": {"status": "not_attempted"},
    }

    try:
        first = selected[0]
        source_category = _category_source_category(config, first)
        proxy_mapping = _load_proxy_mapping()
        resolved_proxy = _resolved_proxy_for(
            proxy_mapping,
            source_category=source_category,
            source_subcategory=first.get("name"),
        )
        proxy_url = http_proxy_url_from_definition(resolved_proxy.proxy) if resolved_proxy else None
        cookies = _acquire_cookies(
            config,
            manifest,
            proxy_key=resolved_proxy.proxy.key if resolved_proxy else "direct",
            proxy=resolved_proxy.proxy if resolved_proxy else None,
            source_category=source_category,
            source_subcategory=first.get("name"),
        )
        proxy_key = resolved_proxy.proxy.key if resolved_proxy else "direct"
        try:
            global_sync_proxy_rate_limiter().wait(proxy_key)
            response = requests.get(STATIC_MENU_URL, timeout=config.timeout_seconds, **requests_proxy_kwargs(proxy_url))
            result["static_menu"] = {
                "status": "ok" if response.status_code == 200 else "warning",
                "http_status": response.status_code,
            }
        except Exception as exception:
            result["static_menu"] = {
                "status": "warning",
                "error": str(exception),
            }
            manifest.record_error(
                phase="network",
                message="Static menu smoke check failed, continuing with catalog probe.",
                action="continued",
                details={"exception": str(exception)},
            )

        if os.environ.get("PARSER_PROXY_PREFLIGHT_ENABLED", "").strip().lower() in {"1", "true", "yes", "y", "on"}:
            result["filters_probe"] = {
                "status": "skipped",
                "reason": "Proxy preflight already validates marketplace access.",
            }
            result["status"] = "ok"
            return result

        query = first.get("name") or first.get("searchQuery")
        parser = SearchPhraseParser(
            search_phrase=query,
            cookies=cookies,
            dest=config.source_region_dest,
            timeout=config.timeout_seconds,
            max_retries=config.max_retries,
            request_delay_bounds=(
                config.request_delay_min_seconds,
                config.request_delay_max_seconds,
            ),
            event_recorder=manifest.record_error,
            source_category=source_category,
            source_subcategory=first.get("name"),
            proxy_url=proxy_url,
            proxy_key=proxy_key,
        )
        probe = parser.fetch_data()
        result["filters_probe"] = {
            "status": "ok" if probe else "failed",
            "query": query,
        }
        result["status"] = "ok" if probe else "failed"
    except Exception as exception:
        result["status"] = "failed"
        result["exception"] = str(exception)
        manifest.record_error(
            phase="network",
            message="Network smoke check raised an exception.",
            action="stopped",
            details={"exception": str(exception)},
        )

    return result


def _acquire_cookies(
    config: ParserConfig,
    manifest: RunManifest,
    *,
    proxy_key: str,
    proxy: Any | None,
    source_category: str | None = None,
    source_subcategory: str | None = None,
) -> dict[str, str] | None:
    if config.wb_token_secret:
        manifest.token_acquisition_status = {"status": "provided_by_env", "proxyKey": proxy_key}
        return {COOKIE_NAME: config.wb_token_secret}

    if not config.acquire_token:
        manifest.token_acquisition_status = {"status": "disabled", "proxyKey": proxy_key}
        return None

    try:
        cookies = get_cookies_for_proxy(proxy_key, proxy)
    except Exception as exception:
        manifest.token_acquisition_status = {"status": "failed", "proxyKey": proxy_key, "error": str(exception)}
        manifest.record_error(
            phase="token",
            message="Token acquisition failed.",
            action="skipped",
            source_category=source_category,
            source_subcategory=source_subcategory,
            details={"exception": str(exception), "proxyKey": proxy_key},
        )
        return None

    snapshot = session_snapshot(proxy_key)
    manifest.token_acquisition_status = {
        "status": "ok" if cookies and cookies.get(COOKIE_NAME) else "empty",
        "proxyKey": proxy_key,
        "tokenRef": snapshot.token_ref,
        "userAgent": snapshot.user_agent,
        "profileDir": str(snapshot.profile_dir),
    }
    return cookies


def _is_filters_preflight_payload_valid(payload: Any) -> bool:
    if not isinstance(payload, dict):
        return False
    return isinstance(payload.get("metadata"), dict) and isinstance(payload.get("data"), dict)


def _filters_preflight_error(parser: Any, *, proxy_key: str, source_query: str) -> str:
    if getattr(parser, "aborted_by_rate_limit", False):
        reason = getattr(parser, "final_error", None) or "WB filters rate limit before split."
    else:
        reason = getattr(parser, "final_error", None) or "metadata was not found in WB filters response."
    return (
        "WB filters preflight failed: "
        f"endpoint=search.wb.ru proxy={proxy_key} query={source_query!r} reason={reason}"
    )


def _write_raw_samples(run_dir: Path, subcategory_name: str, results: list[dict], limit: int) -> None:
    raw_dir = run_dir / "raw"
    raw_dir.mkdir(parents=True, exist_ok=True)
    for index, payload in enumerate(results[:limit], start=1):
        path = raw_dir / f"{_slug(subcategory_name)}_{index}.json"
        path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def _final_status(manifest: RunManifest, interrupted: bool) -> str:
    if interrupted:
        return "interrupted"
    if manifest.error_counts and manifest.row_counts["unique_rows"] > 0:
        return "partial"
    if manifest.error_counts and manifest.row_counts["unique_rows"] == 0:
        return "failed"
    return "succeeded"


def _run_parser_core(
    config: ParserConfig,
    *,
    smoke_only: bool = False,
    streaming_batch_size: int | None = None,
    batch_handler: Callable[[ProductDiscoveryBatch], ProductDiscoveryBatchResult | None] | None = None,
    resume_seen_product_ids: set[str] | None = None,
    category_lifecycle_handler: Callable[[dict[str, Any]], None] | None = None,
) -> Path:
    if streaming_batch_size is not None and streaming_batch_size <= 0:
        raise ValueError("streaming_batch_size must be positive.")
    if streaming_batch_size is not None and batch_handler is None:
        raise ValueError("batch_handler is required for streaming parser runs.")

    parser_run_id = _make_run_id()
    run_dir = config.output_base_dir / "runs" / parser_run_id
    run_dir.mkdir(parents=True, exist_ok=False)

    manifest = _make_manifest(config, run_dir, parser_run_id)
    products_jsonl = run_dir / "products.jsonl"
    products_csv = run_dir / "products.csv"
    products_xlsx = run_dir / "products.xlsx"
    logger.add(run_dir / "runner.log", encoding="utf-8")

    rows: list[dict] = []
    seen_keys: set[tuple[str, str]] = set()
    resume_seen = {str(value) for value in (resume_seen_product_ids or set()) if str(value).strip()}
    pending_streaming_rows: list[dict[str, Any]] = []
    streaming_batch_index = 0
    stop_discovery = False
    interrupted = False
    current_monitored_total = 0

    def monitoring_planned_products(discovered_total: int | None) -> int:
        total = max(0, int(discovered_total or 0))
        raw_max_batches = os.environ.get("PARSER_MAX_STREAM_BATCHES")
        try:
            max_batches = int(raw_max_batches) if raw_max_batches else 0
        except ValueError:
            max_batches = 0
        if max_batches > 0 and streaming_batch_size:
            return max_batches * streaming_batch_size
        return total

    def emit_category_lifecycle(
            *,
            event: str,
            proxy_key: str,
            source_category: str,
            source_subcategory: str,
            status: str | None = None,
            error: str | None = None,
            planned_products_count: int = 0,
            phase: str | None = None,
            planned_ranges_count: int | None = None,
            completed_ranges_count: int | None = None,
            range_progress_percent: float | None = None,
            range_checks_count: int | None = None,
            final_ranges_count: int | None = None,
            empty_ranges_count: int | None = None,
            split_ranges_count: int | None = None) -> None:
        if category_lifecycle_handler is None:
            return
        try:
            category_lifecycle_handler(
                {
                    "event": event,
                    "proxy_key": proxy_key,
                    "source_category": source_category,
                    "source_subcategory": source_subcategory,
                    "status": status,
                    "error": error,
                    "planned_products_count": planned_products_count,
                    "phase": phase,
                    "planned_ranges_count": planned_ranges_count,
                    "completed_ranges_count": completed_ranges_count,
                    "range_progress_percent": range_progress_percent,
                    "range_checks_count": range_checks_count,
                    "final_ranges_count": final_ranges_count,
                    "empty_ranges_count": empty_ranges_count,
                    "split_ranges_count": split_ranges_count,
                }
            )
        except Exception as exception:
            manifest.record_error(
                phase="category_lifecycle",
                source_category=source_category,
                source_subcategory=source_subcategory,
                message="Category lifecycle handler failed.",
                action="ignored",
                details={"exception": str(exception), "event": event, "proxyKey": proxy_key},
            )

    def emit_parser_event(event: str, **fields: Any) -> None:
        payload = {
            "event": event,
            "timestampUtc": utc_now_iso(),
            **fields,
        }
        logger.info("PARSER_EVENT {}", json.dumps(payload, ensure_ascii=False, separators=(",", ":")))

    def flush_streaming_batch() -> bool:
        nonlocal pending_streaming_rows, streaming_batch_index
        if streaming_batch_size is None or batch_handler is None or not pending_streaming_rows:
            return True

        streaming_batch_index += 1
        batch = ProductDiscoveryBatch(
            batch_index=streaming_batch_index,
            rows=list(pending_streaming_rows),
            parent_run_dir=run_dir,
            parent_parser_run_id=parser_run_id,
        )
        emit_parser_event(
            "stream_batch_start",
            proxyKey=proxy_key,
            phase="download",
            sourceCategory=source_category,
            sourceSubcategory=subcategory_name,
            batchIndex=streaming_batch_index,
            size=len(batch.rows),
            downloadedProductsCount=manifest.row_counts["unique_rows"],
            plannedProductsCount=current_monitored_total,
        )
        pending_streaming_rows = []
        result = batch_handler(batch)
        status = (result.status if result else "staged").strip().lower()
        if status in {"stop", "stopped"}:
            manifest.record_warning("streaming_batch_limit_reached")
            return False
        if status == "failed" and config.fail_fast:
            manifest.record_error(
                phase="streaming_batch",
                message="Streaming batch handler failed and fail_fast is enabled.",
                action="stopped",
                details={"batch_index": streaming_batch_index, "reason": result.reason if result else None},
            )
            return False
        emit_parser_event(
            "stream_batch_enqueued",
            proxyKey=proxy_key,
            phase="download",
            sourceCategory=source_category,
            sourceSubcategory=subcategory_name,
            batchIndex=streaming_batch_index,
            size=len(batch.rows),
            downloadedProductsCount=manifest.row_counts["unique_rows"],
            plannedProductsCount=current_monitored_total,
        )
        return True

    try:
        selected = _selected_subcategories(config)
        proxy_mapping = _load_proxy_mapping()

        manifest.network_check_result = _network_smoke_check(config, manifest, selected)
        manifest.write()

        if manifest.network_check_result.get("status") != "ok":
            raise RuntimeError("Network smoke check failed.")

        if smoke_only:
            logger.info("Smoke-only run completed without product parsing.")
            return run_dir

        for selected_category in selected:
            subcategory_name = selected_category.get("name")
            source_category = _category_source_category(config, selected_category)
            wb_search_query = selected_category.get("searchQuery")
            source_query = (
                selected_category.get("parserSearchText")
                or subcategory_name
                or wb_search_query
            )
            category_result = CategoryResult(
                source_subcategory=subcategory_name,
                source_query=source_query,
                status="running",
            )
            proxy_key = "direct"

            try:
                logger.info("Parsing subcategory: {}", subcategory_name)
                resolved_proxy = _resolved_proxy_for(
                    proxy_mapping,
                    source_category=source_category,
                    source_subcategory=subcategory_name,
                )
                proxy_key = resolved_proxy.proxy.key if resolved_proxy else "direct"
                only_proxy = os.environ.get("PARSER_ONLY_PROXY", "").strip()
                if only_proxy and proxy_key != only_proxy:
                    raise RuntimeError(
                        "Selected subcategory resolved to unexpected proxy "
                        f"{proxy_key!r}; expected {only_proxy!r}."
                    )
                emit_category_lifecycle(
                    event="start",
                    proxy_key=proxy_key,
                    source_category=source_category,
                    source_subcategory=subcategory_name,
                    planned_products_count=monitoring_planned_products(None),
                    phase="wb_preflight",
                    planned_ranges_count=0,
                    completed_ranges_count=0,
                    range_progress_percent=0.0,
                    range_checks_count=0,
                    final_ranges_count=0,
                    empty_ranges_count=0,
                    split_ranges_count=0,
                )
                proxy_url = http_proxy_url_from_definition(resolved_proxy.proxy) if resolved_proxy else None
                transport_preflight_result = None
                if resolved_proxy and resolved_proxy.proxy.type != "direct":
                    transport_preflight_result = run_wb_proxy_preflight(resolved_proxy.proxy)
                    if not transport_preflight_result.is_transport_success:
                        preflight_error = transport_preflight_result.diagnostic_message()
                        category_result.status = "failed"
                        emit_category_lifecycle(
                            event="finish",
                            proxy_key=proxy_key,
                            source_category=source_category,
                            source_subcategory=subcategory_name,
                            status="failed",
                            error=preflight_error,
                        )
                        manifest.record_error(
                            phase="wb_preflight",
                            source_category=source_category,
                            source_subcategory=subcategory_name,
                            source_query=source_query,
                            message=preflight_error,
                            action="skipped",
                            details={
                                "proxyKey": proxy_key,
                                "connectStatus": transport_preflight_result.connect_status,
                                "tlsEstablished": transport_preflight_result.tls_established,
                                "server": transport_preflight_result.server,
                                "xWbaasToken": transport_preflight_result.x_wbaas_token,
                                "elapsedMs": transport_preflight_result.elapsed_ms,
                            },
                        )
                        manifest.add_category_result(category_result)
                        if config.fail_fast:
                            break
                        continue

                cookies = _acquire_cookies(
                    config,
                    manifest,
                    proxy_key=proxy_key,
                    proxy=resolved_proxy.proxy if resolved_proxy else None,
                    source_category=source_category,
                    source_subcategory=subcategory_name,
                )
                if config.acquire_token and not cookies:
                    category_result.status = "failed"
                    emit_category_lifecycle(
                        event="finish",
                        proxy_key=proxy_key,
                        source_category=source_category,
                        source_subcategory=subcategory_name,
                        status="failed",
                        error="Proxy-scoped token was not acquired.",
                    )
                    manifest.record_error(
                        phase="token",
                        source_category=source_category,
                        source_subcategory=subcategory_name,
                        source_query=source_query,
                        message="Proxy-scoped token was not acquired.",
                        action="skipped",
                        details={"proxyKey": proxy_key},
                    )
                    manifest.add_category_result(category_result)
                    if config.fail_fast:
                        break
                    continue

                smoke_direct_discovery = os.environ.get("PARSER_SMOKE_DIRECT_DISCOVERY", "").strip().lower() in {
                    "1",
                    "true",
                    "yes",
                    "y",
                    "on",
                }
                price_split_enabled = config.product_fetch_mode == "price_split" and not smoke_direct_discovery
                if price_split_enabled and resolved_proxy and resolved_proxy.proxy.type != "direct":
                    filters_preflight_parser = SearchPhraseParser(
                        search_phrase=source_query,
                        cookies=cookies,
                        dest=config.source_region_dest,
                        timeout=config.timeout_seconds,
                        max_retries=config.max_retries,
                        request_delay_bounds=(
                            config.request_delay_min_seconds,
                            config.request_delay_max_seconds,
                        ),
                        event_recorder=manifest.record_error,
                        source_category=source_category,
                        source_subcategory=subcategory_name,
                        proxy_url=proxy_url,
                        proxy_key=proxy_key,
                    )
                    filters_preflight_payload = filters_preflight_parser.fetch_data()
                    if not _is_filters_preflight_payload_valid(filters_preflight_payload):
                        filters_preflight_error = _filters_preflight_error(
                            filters_preflight_parser,
                            proxy_key=proxy_key,
                            source_query=source_query,
                        )
                        category_result.status = "failed"
                        emit_category_lifecycle(
                            event="finish",
                            proxy_key=proxy_key,
                            source_category=source_category,
                            source_subcategory=subcategory_name,
                            status="failed",
                            error=filters_preflight_error,
                        )
                        manifest.record_error(
                            phase="wb_preflight",
                            source_category=source_category,
                            source_subcategory=subcategory_name,
                            source_query=source_query,
                            message=filters_preflight_error,
                            action="skipped",
                            details={
                                "proxyKey": proxy_key,
                                "endpoint": "search.wb.ru",
                                "transportConnectStatus": (
                                    transport_preflight_result.connect_status
                                    if transport_preflight_result is not None
                                    else 0
                                ),
                                "transportTlsEstablished": (
                                    transport_preflight_result.tls_established
                                    if transport_preflight_result is not None
                                    else False
                                ),
                            },
                        )
                        manifest.add_category_result(category_result)
                        if config.fail_fast:
                            break
                        continue

                emit_category_lifecycle(
                    event="progress",
                    proxy_key=proxy_key,
                    source_category=source_category,
                    source_subcategory=subcategory_name,
                    planned_products_count=monitoring_planned_products(None),
                    phase="ranges",
                    planned_ranges_count=0,
                    completed_ranges_count=0,
                    range_progress_percent=0.0,
                    range_checks_count=0,
                    final_ranges_count=0,
                    empty_ranges_count=0,
                    split_ranges_count=0,
                )
                if smoke_direct_discovery and config.product_fetch_mode == "price_split":
                    logger.info("Smoke direct discovery enabled: skipping price split for {}", subcategory_name)
                if price_split_enabled:
                    def record_split_progress(
                            *,
                            processed_ranges: int,
                            pending_ranges: int,
                            progress_percent: float,
                            range_checks_count: int,
                            final_ranges_count: int,
                            empty_ranges_count: int,
                            split_ranges_count: int) -> None:
                        planned_ranges = max(0, int(processed_ranges) + int(pending_ranges))
                        emit_category_lifecycle(
                            event="progress",
                            proxy_key=proxy_key,
                            source_category=source_category,
                            source_subcategory=subcategory_name,
                            planned_products_count=monitoring_planned_products(None),
                            phase="ranges",
                            planned_ranges_count=planned_ranges,
                            completed_ranges_count=max(0, int(processed_ranges)),
                            range_progress_percent=float(progress_percent),
                            range_checks_count=range_checks_count,
                            final_ranges_count=final_ranges_count,
                            empty_ranges_count=empty_ranges_count,
                            split_ranges_count=split_ranges_count,
                        )

                    price_split_parser = SearchPhraseParser(
                        search_phrase=source_query,
                        cookies=cookies,
                        dest=config.source_region_dest,
                        timeout=config.timeout_seconds,
                        max_retries=config.max_retries,
                        request_delay_bounds=(
                            config.request_delay_min_seconds,
                            config.request_delay_max_seconds,
                        ),
                        event_recorder=manifest.record_error,
                        source_category=source_category,
                        source_subcategory=subcategory_name,
                        proxy_url=proxy_url,
                        proxy_key=proxy_key,
                        split_progress_recorder=record_split_progress,
                    )
                    price_ranges = price_split_parser.parse()

                    if not price_ranges:
                        no_ranges_error = "No price ranges were discovered."
                        if getattr(price_split_parser, "final_error", None):
                            no_ranges_error = str(price_split_parser.final_error)
                        elif price_split_parser.aborted_by_rate_limit:
                            no_ranges_error = "WB filters rate limit while processing full price split."
                        category_result.status = "failed"
                        emit_category_lifecycle(
                            event="finish",
                            proxy_key=proxy_key,
                            source_category=source_category,
                            source_subcategory=subcategory_name,
                            status="failed",
                            error=no_ranges_error,
                            range_checks_count=getattr(price_split_parser, "range_checks_count", 0),
                            final_ranges_count=getattr(price_split_parser, "final_ranges_count", 0),
                            empty_ranges_count=getattr(price_split_parser, "empty_ranges_count", 0),
                            split_ranges_count=getattr(price_split_parser, "split_ranges_count", 0),
                        )
                        manifest.record_error(
                            phase="filters",
                            source_category=source_category,
                            source_subcategory=subcategory_name,
                            source_query=source_query,
                            message=no_ranges_error,
                            action="skipped",
                        )
                        manifest.add_category_result(category_result)
                        if config.fail_fast:
                            break
                        continue
                    discovered_total = sum(
                        max(0, int(getattr(price_range, "total", 0) or 0))
                        for price_range in price_ranges
                    )
                    monitored_total = monitoring_planned_products(discovered_total)
                    current_monitored_total = monitored_total
                    emit_parser_event(
                        "split_completed",
                        proxyKey=proxy_key,
                        phase="download",
                        sourceCategory=source_category,
                        sourceSubcategory=subcategory_name,
                        plannedProductsCount=monitored_total,
                        downloadedProductsCount=manifest.row_counts["unique_rows"],
                        rangeChecksCount=getattr(price_split_parser, "range_checks_count", len(price_ranges)),
                        finalRangesCount=getattr(price_split_parser, "final_ranges_count", len(price_ranges)),
                        emptyRangesCount=getattr(price_split_parser, "empty_ranges_count", 0),
                        splitRangesCount=getattr(price_split_parser, "split_ranges_count", 0),
                    )
                    emit_category_lifecycle(
                        event="progress",
                        proxy_key=proxy_key,
                        source_category=source_category,
                        source_subcategory=subcategory_name,
                        planned_products_count=monitored_total,
                        phase="download",
                        planned_ranges_count=len(price_ranges),
                        completed_ranges_count=len(price_ranges),
                        range_progress_percent=100.0,
                        range_checks_count=getattr(price_split_parser, "range_checks_count", len(price_ranges)),
                        final_ranges_count=getattr(price_split_parser, "final_ranges_count", len(price_ranges)),
                        empty_ranges_count=getattr(price_split_parser, "empty_ranges_count", 0),
                        split_ranges_count=getattr(price_split_parser, "split_ranges_count", 0),
                    )
                else:
                    price_ranges = []
                    manifest.record_warning("price_discovery_skipped")

                if price_split_enabled and price_ranges:
                    _wait_after_price_split_before_catalog_fetch(proxy_key)

                fetcher = WbCatalogFetcher(
                    pages=price_ranges,
                    search_phrase=source_query,
                    cookies=cookies or {},
                    dest=config.source_region_dest,
                    batch_size=config.catalog_request_group_size,
                    max_concurrent=config.max_concurrent,
                    timeout=config.timeout_seconds,
                    max_retries=config.max_retries,
                    request_delay_bounds=(
                        config.request_delay_min_seconds,
                        config.request_delay_max_seconds,
                    ),
                    batch_delay_bounds=(
                        config.batch_delay_min_seconds,
                        config.batch_delay_max_seconds,
                    ),
                    max_catalog_pages=config.max_catalog_pages_per_subcategory,
                    max_limit_signals=config.max_limit_signals_per_subcategory,
                    price_split_enabled=price_split_enabled,
                    event_recorder=manifest.record_error,
                    attempt_recorder=manifest.record_attempt,
                    retry_recorder=manifest.record_retry,
                    backoff_recorder=manifest.record_backoff,
                    source_category=source_category,
                    source_subcategory=subcategory_name,
                    proxy_url=proxy_url,
                    proxy_key=proxy_key,
                )
                raw_samples: list[dict] = []

                def process_result_batch(result_batch: list[dict]) -> bool:
                    nonlocal stop_discovery
                    if config.enable_raw_samples and len(raw_samples) < config.raw_sample_limit:
                        remaining = config.raw_sample_limit - len(raw_samples)
                        raw_samples.extend(result_batch[:remaining])

                    product_entries = []
                    for raw_data in result_batch:
                        if "products" not in raw_data:
                            manifest.record_error(
                                phase="catalog",
                                source_category=source_category,
                                source_subcategory=subcategory_name,
                                source_query=source_query,
                                message="Response without products was skipped.",
                                action="skipped",
                            )
                            continue

                        range_id = raw_data.get("__parser_range_id")
                        page_number = int(raw_data.get("__parser_page") or 1)
                        page_count = int(raw_data.get("__parser_page_count") or page_number)
                        start_offset = max(0, int(raw_data.get("__parser_start_offset") or 0))
                        raw_products = list(raw_data.get("products") or [])
                        raw_data_for_items = {
                            **raw_data,
                            "products": raw_products[start_offset:],
                        }
                        items_info = Items.model_validate(raw_data_for_items)
                        if items_info.products:
                            product_entries.extend(
                                {
                                    "product": product,
                                    "range_id": range_id,
                                    "page_number": page_number,
                                    "page_count": page_count,
                                    "absolute_offset": start_offset + index,
                                    "raw_page_size": len(raw_products),
                                }
                                for index, product in enumerate(items_info.products)
                            )

                    product_models = add_images([item["product"] for item in product_entries])
                    if config.include_wb_wallet_prices:
                        product_models = add_price_with_wb_wallet(product_models)

                    for item_index, product in enumerate(product_models):
                        metadata = product_entries[item_index]
                        range_id = metadata.get("range_id")
                        page_number = int(metadata.get("page_number") or 1)
                        page_count = int(metadata.get("page_count") or page_number)
                        absolute_offset = int(metadata.get("absolute_offset") or 0)
                        raw_page_size = int(metadata.get("raw_page_size") or 0)

                        def remember_cursor() -> None:
                            return

                        if stop_discovery:
                            return False
                        if (
                                config.max_items_per_subcategory
                                and category_result.total_rows >= config.max_items_per_subcategory):
                            manifest.record_warning("item_cap")
                            return False

                        category_result.total_rows += 1
                        manifest.row_counts["total_rows"] += 1
                        product_id = str(product.id)
                        key = (config.marketplace, product_id)
                        if key in seen_keys:
                            remember_cursor()
                            category_result.duplicate_rows += 1
                            manifest.row_counts["duplicate_rows"] += 1
                            continue
                        if product_id in resume_seen:
                            remember_cursor()
                            seen_keys.add(key)
                            category_result.duplicate_rows += 1
                            manifest.row_counts["duplicate_rows"] += 1
                            continue

                        seen_keys.add(key)
                        row = product_to_canonical_row(
                            item=product,
                            parser_run_id=parser_run_id,
                            parsed_at_utc=utc_now_iso(),
                            marketplace=config.marketplace,
                            source_category=source_category,
                            source_subcategory=subcategory_name,
                            source_query=source_query,
                            source_region_dest=config.source_region_dest,
                        )
                        append_jsonl(products_jsonl, row)
                        rows.append(row)
                        category_result.unique_rows += 1
                        manifest.row_counts["unique_rows"] += 1
                        remember_cursor()
                        if streaming_batch_size is not None:
                            pending_streaming_rows.append(row)
                            if len(pending_streaming_rows) >= streaming_batch_size:
                                logger.info(
                                    "STREAM DISCOVERY products_seen={} pending_batch={}",
                                    manifest.row_counts["unique_rows"],
                                    len(pending_streaming_rows),
                                )
                                if not flush_streaming_batch():
                                    stop_discovery = True
                                    return False
                                manifest.write()

                    if streaming_batch_size is not None:
                        logger.info(
                            "STREAM DISCOVERY products_seen={} pending_batch={}",
                            manifest.row_counts["unique_rows"],
                            len(pending_streaming_rows),
                        )
                        manifest.write()
                    return True

                if streaming_batch_size is not None:
                    async def consume_streaming_batches() -> None:
                        async for result_batch in fetcher.iter_result_batches():
                            if not process_result_batch(result_batch):
                                break

                    asyncio.run(consume_streaming_batches())
                else:
                    results = asyncio.run(fetcher.fetch_all())
                    process_result_batch(results)

                if getattr(fetcher, "stop_requested", False) and not stop_discovery:
                    raise RuntimeError("WB catalog rate limit while fetching price ranges.")

                if streaming_batch_size is not None and pending_streaming_rows:
                    logger.info(
                        "STREAM DISCOVERY category boundary flush products_seen={} pending_batch={}",
                        manifest.row_counts["unique_rows"],
                        len(pending_streaming_rows),
                    )
                    if not flush_streaming_batch():
                        stop_discovery = True
                    manifest.write()

                if config.enable_raw_samples and raw_samples:
                    _write_raw_samples(run_dir, subcategory_name, raw_samples, config.raw_sample_limit)

                category_result.status = "succeeded" if category_result.unique_rows else "partial"
                emit_category_lifecycle(
                    event="finish",
                    proxy_key=proxy_key,
                    source_category=source_category,
                    source_subcategory=subcategory_name,
                    status="completed" if category_result.unique_rows else "failed",
                    error=None if category_result.unique_rows else "No products were discovered.",
                )
                manifest.add_category_result(category_result)
                manifest.write()
                if stop_discovery:
                    break
                time.sleep(random.uniform(config.batch_delay_min_seconds, config.batch_delay_max_seconds))
            except KeyboardInterrupt:
                interrupted = True
                category_result.status = "interrupted"
                try:
                    emit_category_lifecycle(
                        event="finish",
                        proxy_key=locals().get("proxy_key", "direct"),
                        source_category=source_category,
                        source_subcategory=subcategory_name,
                        status="failed",
                        error="Parser interrupted.",
                    )
                except Exception:
                    pass
                manifest.add_category_result(category_result)
                raise
            except Exception as exception:
                category_result.status = "failed"
                category_result.error_count += 1
                emit_category_lifecycle(
                    event="finish",
                    proxy_key=locals().get("proxy_key", "direct"),
                    source_category=source_category,
                    source_subcategory=subcategory_name,
                    status="failed",
                    error=str(exception),
                )
                manifest.record_error(
                    phase="subcategory",
                    source_category=source_category,
                    source_subcategory=subcategory_name,
                    source_query=source_query,
                    message="Subcategory parsing failed.",
                    action="skipped",
                    details={"exception": str(exception)},
                )
                manifest.add_category_result(category_result)
                manifest.write()
                if config.fail_fast:
                    break

        flush_streaming_batch()
        write_csv(products_csv, rows)
        if config.include_xlsx:
            write_xlsx(products_xlsx, rows)
    except KeyboardInterrupt:
        interrupted = True
        logger.warning("Parser interrupted. Writing partial manifest.")
    except Exception as exception:
        manifest.record_error(
            phase="runner",
            message="Runner failed.",
            action="stopped",
            details={"exception": str(exception)},
        )
        logger.exception("Runner failed")
    finally:
        if not products_csv.exists():
            write_csv(products_csv, rows)
        if config.include_xlsx and not products_xlsx.exists():
            write_xlsx(products_xlsx, rows)
        manifest.finish(_final_status(manifest, interrupted))
        manifest.write()

    return run_dir


def run_parser(config: ParserConfig, *, smoke_only: bool = False) -> Path:
    return _run_parser_core(config, smoke_only=smoke_only)


def run_parser_streaming(
    config: ParserConfig,
    streaming_batch_size: int,
    batch_handler: Callable[[ProductDiscoveryBatch], ProductDiscoveryBatchResult | None],
    *,
    smoke_only: bool = False,
    resume_seen_product_ids: set[str] | None = None,
    category_lifecycle_handler: Callable[[dict[str, Any]], None] | None = None,
) -> Path:
    return _run_parser_core(
        config,
        smoke_only=smoke_only,
        streaming_batch_size=streaming_batch_size,
        batch_handler=batch_handler,
        resume_seen_product_ids=resume_seen_product_ids,
        category_lifecycle_handler=category_lifecycle_handler,
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Safe manual WB product-card parser runner.")
    parser.add_argument("--config", type=Path, default=BASE_DIR / ".env", help="Path to parser .env config.")
    parser.add_argument("--smoke-only", action="store_true", help="Run only network/config smoke checks.")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    config = ParserConfig.load(args.config)
    run_dir = run_parser(config, smoke_only=args.smoke_only)
    logger.info("Run directory: {}", run_dir)


if __name__ == "__main__":
    main()
