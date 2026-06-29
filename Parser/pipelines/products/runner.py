from __future__ import annotations

import argparse
import asyncio
import json
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
from get_token import get_token
from images_parser import add_images
from manifest import CategoryResult, RunManifest, get_git_commit, utc_now_iso
from models import Items
from run_scope import parser_run_scope_from_env
from SearchPhraseParser import SearchPhraseParser
from WbCatalogFetcher import WbCatalogFetcher


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


def _slug(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-zА-Яа-я0-9_-]+", "_", value, flags=re.UNICODE).strip("_")
    return cleaned[:80] or "subcategory"


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
        selected: list[dict],
        cookies: dict | None) -> dict:
    result = {
        "status": "not_attempted",
        "static_menu": {"status": "not_attempted"},
        "filters_probe": {"status": "not_attempted"},
    }

    try:
        response = requests.get(STATIC_MENU_URL, timeout=config.timeout_seconds)
        result["static_menu"] = {
            "status": "ok" if response.status_code == 200 else "failed",
            "http_status": response.status_code,
        }
        if response.status_code != 200:
            manifest.record_error(
                phase="network",
                message="Static menu smoke check failed.",
                http_status=response.status_code,
                action="stopped",
            )
            result["status"] = "failed"
            return result

        first = selected[0]
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
            source_category=config.parent_category,
            source_subcategory=first.get("name"),
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


def _acquire_token(config: ParserConfig, manifest: RunManifest) -> str | None:
    if config.wb_token_secret:
        manifest.token_acquisition_status = {"status": "provided_by_env"}
        return config.wb_token_secret

    if not config.acquire_token:
        manifest.token_acquisition_status = {"status": "disabled"}
        return None

    try:
        token = get_token()
    except Exception as exception:
        manifest.token_acquisition_status = {"status": "failed", "error": str(exception)}
        manifest.record_error(
            phase="token",
            message="Token acquisition failed.",
            action="stopped",
            details={"exception": str(exception)},
        )
        return None

    manifest.token_acquisition_status = {"status": "ok" if token else "empty"}
    return token


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
        token = _acquire_token(config, manifest)
        cookies = {"x_wbaas_token": token} if token else None

        manifest.network_check_result = _network_smoke_check(config, manifest, selected, cookies)
        manifest.write()

        if manifest.network_check_result.get("status") != "ok":
            raise RuntimeError("Network smoke check failed.")

        if smoke_only:
            logger.info("Smoke-only run completed without product parsing.")
            return run_dir

        for selected_category in selected:
            subcategory_name = selected_category.get("name")
            source_query = subcategory_name or selected_category.get("searchQuery")
            category_result = CategoryResult(
                source_subcategory=subcategory_name,
                source_query=source_query,
                status="running",
            )

            try:
                logger.info("Parsing subcategory: {}", subcategory_name)
                price_split_enabled = config.product_fetch_mode == "price_split"
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
                        source_category=config.parent_category,
                        source_subcategory=subcategory_name,
                    ).parse()

                    if not price_ranges:
                        category_result.status = "failed"
                        manifest.record_error(
                            phase="filters",
                            source_category=config.parent_category,
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
                    source_category=config.parent_category,
                    source_subcategory=subcategory_name,
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
                                source_category=config.parent_category,
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
                            source_category=config.parent_category,
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
                    source_category=config.parent_category,
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
