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
from app.browser_sessions import COOKIE_NAME, get_token_for_proxy
from app.proxy_mapping import ProxyMapping
from app.proxy_transport import http_proxy_url_from_definition, requests_proxy_kwargs


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
        source_path = str(item.get("sourcePath") or "").strip()

        if not wb_category_id:
            raise ValueError("Explicit niche wbCategoryId is required.")
        if wb_category_id in seen_ids:
            raise ValueError(f"Duplicate explicit niche wbCategoryId: {wb_category_id}")
        if not source_category or not source_subcategory or not search_query:
            raise ValueError(f"Explicit niche {wb_category_id} requires sourceCategory, sourceSubcategory and searchQuery.")

        seen_ids.add(wb_category_id)
        selected.append(
            {
                "id": int(wb_category_id),
                "name": source_subcategory,
                "searchQuery": search_query,
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
        source_subcategory = str(wb_leaf.get("sourceSubcategory") or wb_leaf.get("name") or item.get("name") or "").strip()
        source_path = str(wb_leaf.get("sourcePath") or item.get("sourcePath") or source_subcategory).strip()
        resolved.append(
            {
                **item,
                "name": source_subcategory,
                "sourceCategory": source_category,
                "sourceSubcategory": source_subcategory,
                "sourcePath": source_path,
            }
        )

    return resolved


def _category_source_category(config: ParserConfig, selected_category: dict[str, Any]) -> str:
    return str(selected_category.get("sourceCategory") or config.parent_category).strip()


def _make_run_id() -> str:
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    commit = get_git_commit(BASE_DIR.parent)
    return f"wb_products_{timestamp}_{commit}"


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
        try:
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
        token = get_token_for_proxy(proxy_key, proxy)
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

    manifest.token_acquisition_status = {"status": "ok" if token else "empty", "proxyKey": proxy_key}
    return {COOKIE_NAME: token} if token else None


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
            source_query = subcategory_name or selected_category.get("searchQuery")
            source_query = selected_category.get("searchQuery") or source_query
            category_result = CategoryResult(
                source_subcategory=subcategory_name,
                source_query=source_query,
                status="running",
            )

            try:
                logger.info("Parsing subcategory: {}", subcategory_name)
                resolved_proxy = _resolved_proxy_for(
                    proxy_mapping,
                    source_category=source_category,
                    source_subcategory=subcategory_name,
                )
                proxy_url = http_proxy_url_from_definition(resolved_proxy.proxy) if resolved_proxy else None
                cookies = _acquire_cookies(
                    config,
                    manifest,
                    proxy_key=resolved_proxy.proxy.key if resolved_proxy else "direct",
                    proxy=resolved_proxy.proxy if resolved_proxy else None,
                    source_category=source_category,
                    source_subcategory=subcategory_name,
                )
                if config.acquire_token and not cookies:
                    category_result.status = "failed"
                    manifest.record_error(
                        phase="token",
                        source_category=source_category,
                        source_subcategory=subcategory_name,
                        source_query=source_query,
                        message="Proxy-scoped token was not acquired.",
                        action="skipped",
                        details={"proxyKey": resolved_proxy.proxy.key if resolved_proxy else "direct"},
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
                if smoke_direct_discovery and config.product_fetch_mode == "price_split":
                    logger.info("Smoke direct discovery enabled: skipping price split for {}", subcategory_name)
                if price_split_enabled:
                    price_ranges = SearchPhraseParser(
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
                    ).parse()

                    if not price_ranges:
                        category_result.status = "failed"
                        manifest.record_error(
                            phase="filters",
                            source_category=source_category,
                            source_subcategory=subcategory_name,
                            source_query=source_query,
                            message="No price ranges were discovered.",
                            action="skipped",
                        )
                        manifest.add_category_result(category_result)
                        if config.fail_fast:
                            break
                        continue
                else:
                    price_ranges = []
                    manifest.record_warning("price_discovery_skipped")

                fetcher = WbCatalogFetcher(
                    pages=price_ranges,
                    search_phrase=source_query,
                    cookies=cookies or {},
                    dest=config.source_region_dest,
                    batch_size=config.batch_size,
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
                )
                raw_samples: list[dict] = []

                def process_result_batch(result_batch: list[dict]) -> bool:
                    nonlocal stop_discovery
                    if config.enable_raw_samples and len(raw_samples) < config.raw_sample_limit:
                        remaining = config.raw_sample_limit - len(raw_samples)
                        raw_samples.extend(result_batch[:remaining])

                    product_models = []
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

                        items_info = Items.model_validate(raw_data)
                        if items_info.products:
                            product_models.extend(items_info.products)

                    product_models = add_images(product_models)
                    if config.include_wb_wallet_prices:
                        product_models = add_price_with_wb_wallet(product_models)

                    for product in product_models:
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
                            category_result.duplicate_rows += 1
                            manifest.row_counts["duplicate_rows"] += 1
                            continue
                        if product_id in resume_seen:
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

                if config.enable_raw_samples and raw_samples:
                    _write_raw_samples(run_dir, subcategory_name, raw_samples, config.raw_sample_limit)

                category_result.status = "succeeded" if category_result.unique_rows else "partial"
                manifest.add_category_result(category_result)
                manifest.write()
                if stop_discovery:
                    break
                time.sleep(random.uniform(config.batch_delay_min_seconds, config.batch_delay_max_seconds))
            except KeyboardInterrupt:
                interrupted = True
                category_result.status = "interrupted"
                manifest.add_category_result(category_result)
                raise
            except Exception as exception:
                category_result.status = "failed"
                category_result.error_count += 1
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
) -> Path:
    return _run_parser_core(
        config,
        smoke_only=smoke_only,
        streaming_batch_size=streaming_batch_size,
        batch_handler=batch_handler,
        resume_seen_product_ids=resume_seen_product_ids,
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
