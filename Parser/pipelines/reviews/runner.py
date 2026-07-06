from __future__ import annotations

import argparse
import asyncio
import json
import sys
from dataclasses import dataclass, replace
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

PARSER_ROOT = Path(__file__).resolve().parents[2]
if str(PARSER_ROOT) not in sys.path:
    sys.path.insert(0, str(PARSER_ROOT))

try:
    import httpx  # noqa: F401
    from loguru import logger
except ModuleNotFoundError as exception:
    missing = exception.name
    print(
        "Parser dependencies are missing in the current Python environment.\n"
        f"Missing module: {missing}\n\n"
        "Use the project parser virtual environment:\n"
        r"  .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\reviews\runner.py --help"
        "\n\nOr install dependencies into the current environment:\n"
        r"  python -m pip install -r Parser\requirements.txt",
        file=sys.stderr,
    )
    raise SystemExit(1) from exception

from config import BASE_DIR, ReviewsParserConfig
from app.runtime_review_sync_state import (
    ReviewSyncState,
    load_runtime_review_sync_state,
    runtime_review_sync_state_url,
)
from manifest import get_git_commit, utc_now_iso
from review_contracts import (
    ROOT_VARIANT_FILTERED_ATTRIBUTION_MODE,
    SelectedProduct,
    map_feedback_payload,
)
from review_exporters import (
    ReviewCanonicalExporter,
    append_jsonl,
    iter_jsonl,
    write_raw_root_payload,
)
from reviews_manifest import ReviewRunManifest
from run_scope import parser_run_scope_from_env
from wb_reviews_client import ReviewFetchResult, WbReviewsClient


@dataclass(frozen=True)
class RootWorkItem:
    wb_root_id: str
    selected_products: dict[str, SelectedProduct]

    @property
    def selected_wb_product_ids(self) -> list[str]:
        return sorted(self.selected_products.keys())

    @property
    def wb_product_id(self) -> str | None:
        return None


@dataclass(frozen=True)
class ProductWorkItem:
    product: SelectedProduct

    @property
    def wb_root_id(self) -> str:
        return self.product.wb_root_id

    @property
    def wb_product_id(self) -> str:
        return self.product.wb_product_id

    @property
    def selected_products(self) -> dict[str, SelectedProduct]:
        return {self.product.wb_product_id: self.product}

    @property
    def selected_wb_product_ids(self) -> list[str]:
        return [self.product.wb_product_id]


@dataclass(frozen=True)
class ProductsSource:
    products_jsonl: Path
    input_products_parser_run_id: str | None
    limit_products: int | None
    source_subcategory: str | None


def _make_run_id() -> str:
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    commit = get_git_commit(BASE_DIR.parent)
    return f"wb_reviews_{timestamp}_{commit}"


def _output_paths(run_dir: Path) -> dict[str, Path]:
    return {
        "manifest": run_dir / "manifest.json",
        "reviews_jsonl": run_dir / "reviews.jsonl",
        "review_replies_jsonl": run_dir / "review_replies.jsonl",
        "review_fetch_results_jsonl": run_dir / "review_fetch_results.jsonl",
        "review_coverage_jsonl": run_dir / "review_coverage.jsonl",
        "errors_jsonl": run_dir / "errors.jsonl",
        "runner_log": run_dir / "runner.log",
        "raw_root_feedbacks": run_dir / "raw" / "root_feedbacks",
        "raw_product_feedbacks": run_dir / "raw" / "product_feedbacks",
    }


def _ensure_contract_files(output_paths: dict[str, Path]) -> None:
    for key in (
        "reviews_jsonl",
        "review_replies_jsonl",
        "review_fetch_results_jsonl",
        "review_coverage_jsonl",
        "errors_jsonl",
        "runner_log",
    ):
        path = output_paths[key]
        path.parent.mkdir(parents=True, exist_ok=True)
        path.touch(exist_ok=True)


def _resolve_products_source(
    args: argparse.Namespace,
    resume_manifest: ReviewRunManifest | None,
) -> ProductsSource:
    if resume_manifest:
        scope = resume_manifest.requested_scope
        path = Path(scope["input_products_jsonl"])
        return ProductsSource(
            products_jsonl=path,
            input_products_parser_run_id=scope.get("input_products_parser_run_id"),
            limit_products=scope.get("limit_products"),
            source_subcategory=scope.get("source_subcategory"),
        )

    if args.products_jsonl:
        products_jsonl = args.products_jsonl
        product_run_dir = products_jsonl.parent
    elif args.products_run_dir:
        product_run_dir = args.products_run_dir
        products_jsonl = product_run_dir / "products.jsonl"
    else:
        raise ValueError("Provide --products-run-dir or --products-jsonl for a new review run.")

    if not products_jsonl.exists():
        raise FileNotFoundError(f"Products JSONL was not found: {products_jsonl}")

    input_products_parser_run_id = _products_run_id(product_run_dir)
    return ProductsSource(
        products_jsonl=products_jsonl.resolve(),
        input_products_parser_run_id=input_products_parser_run_id,
        limit_products=args.limit_products,
        source_subcategory=args.source_subcategory,
    )


def _products_run_id(product_run_dir: Path) -> str | None:
    manifest_path = product_run_dir / "manifest.json"
    if not manifest_path.exists():
        return None

    try:
        payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    return payload.get("parser_run_id")


def _load_products(
    *,
    source: ProductsSource,
    marketplace: str,
    manifest: ReviewRunManifest,
) -> list[RootWorkItem]:
    products_total = 0
    selected_products = 0
    root_products: dict[str, dict[str, SelectedProduct]] = {}
    selected_product_ids: set[str] = set()

    with source.products_jsonl.open(encoding="utf-8") as file:
        for line_number, line in enumerate(file, start=1):
            if not line.strip():
                continue

            products_total += 1
            try:
                row = json.loads(line)
            except json.JSONDecodeError as exception:
                manifest.add_counter("products_skipped")
                manifest.record_error(
                    phase="input",
                    message="Products JSONL row is not valid JSON.",
                    action="skipped",
                    exception_type=type(exception).__name__,
                    details={"line_number": line_number},
                )
                continue

            if (
                source.source_subcategory
                and row.get("source_subcategory") != source.source_subcategory
            ):
                continue

            if source.limit_products and selected_products >= source.limit_products:
                continue

            row_marketplace = str(row.get("marketplace") or "")
            wb_product_id = _string_id(row.get("wb_product_id"))
            wb_root_id = _string_id(row.get("wb_root_id"))
            if row_marketplace != marketplace or not wb_product_id or not wb_root_id:
                manifest.add_counter("products_skipped")
                manifest.record_error(
                    phase="input",
                    message="Products JSONL row is missing required review-run fields.",
                    action="skipped",
                    details={
                        "line_number": line_number,
                        "marketplace": row_marketplace,
                        "wb_product_id": wb_product_id,
                        "wb_root_id": wb_root_id,
                    },
                )
                continue

            if wb_product_id in selected_product_ids:
                continue

            selected_product_ids.add(wb_product_id)
            selected_products += 1
            product = SelectedProduct(
                wb_product_id=wb_product_id,
                wb_root_id=wb_root_id,
                feedback_count=_int_or_none(row.get("feedback_count")),
                source_category=row.get("source_category"),
                source_subcategory=row.get("source_subcategory"),
                source_query=row.get("source_query"),
                source_region_dest=row.get("source_region_dest"),
            )
            root_products.setdefault(wb_root_id, {})[wb_product_id] = product

    manifest.set_counter("products_total", products_total)
    manifest.set_counter("products_selected", selected_products)
    manifest.set_counter("roots_selected", len(root_products))
    return [
        RootWorkItem(wb_root_id=root_id, selected_products=products)
        for root_id, products in root_products.items()
    ]


def _to_review_work_items(
    root_items: list[RootWorkItem],
    config: ReviewsParserConfig,
) -> list[RootWorkItem | ProductWorkItem]:
    if config.fetch_mode == "root_capped_fallback":
        return root_items

    return [
        ProductWorkItem(product=product)
        for root_item in root_items
        for product in root_item.selected_products.values()
    ]


def _completed_root_ids(fetch_results_path: Path) -> set[str]:
    completed: set[str] = set()
    for row in iter_jsonl(fetch_results_path) or []:
        status = row.get("status")
        if status in {"success", "empty"}:
            root_id = _string_id(row.get("source_wb_root_id"))
            if root_id:
                completed.add(root_id)
    return completed


def _completed_product_ids(fetch_results_path: Path) -> set[str]:
    completed: set[str] = set()
    for row in iter_jsonl(fetch_results_path) or []:
        status = row.get("status")
        if status in {"success", "empty", "skipped"}:
            product_id = _string_id(row.get("source_wb_product_id"))
            if product_id:
                completed.add(product_id)
    return completed


async def _smoke_probe(
    *,
    config: ReviewsParserConfig,
    work_item: RootWorkItem | ProductWorkItem,
) -> ReviewFetchResult:
    async with WbReviewsClient(config) as client:
        if isinstance(work_item, ProductWorkItem):
            return await client.fetch_root_variant(
                wb_product_id=work_item.wb_product_id,
                wb_root_id=work_item.wb_root_id,
                marketplace_feedback_count=work_item.product.feedback_count,
            )
        return await client.smoke_probe(work_item.wb_root_id)


async def _run_worklist(
    *,
    config: ReviewsParserConfig,
    work_items: list[RootWorkItem | ProductWorkItem],
    handler: Callable[[RootWorkItem | ProductWorkItem, ReviewFetchResult], None],
) -> None:
    work_queue: asyncio.Queue[RootWorkItem | ProductWorkItem | None] = asyncio.Queue()
    result_queue: asyncio.Queue[tuple[RootWorkItem | ProductWorkItem, ReviewFetchResult]] = asyncio.Queue()
    for work_item in work_items:
        work_queue.put_nowait(work_item)

    async with WbReviewsClient(config) as client:
        async def worker() -> None:
            while True:
                work_item = await work_queue.get()
                try:
                    if work_item is None:
                        return
                    if isinstance(work_item, ProductWorkItem):
                        sync_state = _load_review_sync_state(work_item)
                        if (
                            sync_state is not None
                            and sync_state.marketplace_feedback_count is not None
                            and work_item.product.feedback_count == sync_state.marketplace_feedback_count
                            and sync_state.has_complete_review_history
                            and not sync_state.unanswered_review_ids
                        ):
                            result = await client.fetch_product(
                                wb_product_id=work_item.wb_product_id,
                                wb_root_id=work_item.wb_root_id,
                                sync_state=sync_state,
                                marketplace_feedback_count=work_item.product.feedback_count,
                            )
                        else:
                            result = await client.fetch_root_variant(
                                wb_product_id=work_item.wb_product_id,
                                wb_root_id=work_item.wb_root_id,
                                marketplace_feedback_count=work_item.product.feedback_count,
                            )
                    else:
                        result = await client.fetch_root(work_item.wb_root_id)
                    await result_queue.put((work_item, result))
                finally:
                    work_queue.task_done()

        workers = [
            asyncio.create_task(worker())
            for _ in range(min(config.max_concurrent, max(len(work_items), 1)))
        ]
        processed = 0
        try:
            while processed < len(work_items):
                work_item, result = await result_queue.get()
                handler(work_item, result)
                processed += 1
                if config.fail_fast and result.status == "failed":
                    raise RuntimeError("Review runner stopped after a failed root fetch.")
        finally:
            for _ in workers:
                work_queue.put_nowait(None)
            await work_queue.join()
            await asyncio.gather(*workers, return_exceptions=True)


def _handle_result(
    *,
    work_item: RootWorkItem | ProductWorkItem,
    fetch_result: ReviewFetchResult,
    config: ReviewsParserConfig,
    manifest: ReviewRunManifest,
    exporter: ReviewCanonicalExporter,
    output_paths: dict[str, Path],
    input_products_parser_run_id: str | None,
    input_products_jsonl: Path,
) -> None:
    manifest.record_fetch_attempts(
        attempts=fetch_result.attempts,
        retries=fetch_result.retries,
        backoff_seconds=fetch_result.backoff_seconds_total,
    )
    is_product_full = isinstance(work_item, ProductWorkItem)
    if is_product_full:
        manifest.add_counter("products_processed")
    else:
        manifest.add_counter("roots_processed")
        manifest.add_counter("products_processed", len(work_item.selected_products))

    raw_payload_path: Path | None = None
    write_result = None
    mapped = None
    event_status = fetch_result.status
    error_summary = fetch_result.error_summary

    if fetch_result.status == "failed":
        if is_product_full:
            manifest.add_counter("products_failed")
        else:
            manifest.add_counter("roots_failed")
        manifest.record_error(
            phase="fetch",
            message=fetch_result.error_summary or "WB review root fetch failed.",
            source_wb_root_id=work_item.wb_root_id,
            selected_wb_product_ids=work_item.selected_wb_product_ids,
            endpoint=fetch_result.endpoint,
            attempt=fetch_result.attempts,
            action="stopped",
            http_status=fetch_result.http_status,
            exception_type=fetch_result.exception_type,
        )
    else:
        try:
            if config.retain_raw_payloads and fetch_result.payload is not None:
                raw_dir = output_paths["raw_product_feedbacks"] if is_product_full else output_paths["raw_root_feedbacks"]
                raw_file = f"{work_item.wb_product_id or work_item.wb_root_id}.json.gz"
                raw_payload_path = write_raw_root_payload(
                    raw_dir / raw_file,
                    fetch_result.payload,
                )

            if fetch_result.status in {"empty", "skipped"}:
                if is_product_full:
                    manifest.add_counter("products_empty")
                else:
                    manifest.add_counter("roots_empty")
            else:
                mapped = map_feedback_payload(
                    payload=fetch_result.payload or {},
                    selected_products=work_item.selected_products,
                    parser_run_id=manifest.parser_run_id,
                    parsed_at_utc=utc_now_iso(),
                    marketplace=manifest.marketplace,
                    input_products_parser_run_id=input_products_parser_run_id,
                    input_products_jsonl=str(input_products_jsonl),
                    source_wb_root_id=work_item.wb_root_id,
                    review_attribution_mode=(
                        ROOT_VARIANT_FILTERED_ATTRIBUTION_MODE
                        if is_product_full
                        else "root_payload"
                    ),
                )
                manifest.add_counter("reviews_seen", mapped.payload_feedback_rows_seen)
                manifest.add_counter("replies_seen", mapped.replies_seen)
                for issue in mapped.issues:
                    manifest.record_error(
                        phase=issue.phase,
                        message=issue.message,
                        source_wb_root_id=work_item.wb_root_id,
                        selected_wb_product_ids=work_item.selected_wb_product_ids,
                        endpoint=fetch_result.endpoint,
                        action="skipped",
                        details=issue.details,
                    )

                write_result = exporter.write_rows(
                    review_rows=mapped.review_rows,
                    reply_rows=mapped.reply_rows,
                )
                if is_product_full:
                    manifest.add_counter("products_succeeded")
                else:
                    manifest.add_counter("roots_succeeded")
                manifest.add_counter("reviews_written", write_result.reviews_written)
                manifest.add_counter("replies_written", write_result.replies_written)
                manifest.add_counter("review_duplicates", write_result.review_duplicates)
                manifest.add_counter("reply_duplicates", write_result.reply_duplicates)
                manifest.add_counter(
                    "products_with_reviews",
                    len(write_result.product_ids_with_new_reviews),
                )
        except Exception as exception:
            event_status = "failed"
            error_summary = str(exception)
            if is_product_full:
                manifest.add_counter("products_failed")
            else:
                manifest.add_counter("roots_failed")
            manifest.record_error(
                phase="export",
                message="Review root output handling failed.",
                source_wb_root_id=work_item.wb_root_id,
                selected_wb_product_ids=work_item.selected_wb_product_ids,
                endpoint=fetch_result.endpoint,
                action="stopped",
                http_status=fetch_result.http_status,
                exception_type=type(exception).__name__,
                details={"exception": str(exception)},
            )

    coverage = _coverage_row(
        work_item=work_item,
        fetch_result=fetch_result,
        mapped=mapped,
        event_status=event_status,
        error_summary=error_summary,
    )
    append_jsonl(output_paths["review_coverage_jsonl"], coverage)

    append_jsonl(
        output_paths["review_fetch_results_jsonl"],
        {
            "timestamp_utc": utc_now_iso(),
            "parser_run_id": manifest.parser_run_id,
            "source_wb_root_id": work_item.wb_root_id,
            "source_wb_product_id": work_item.wb_product_id,
            "selected_wb_product_ids": work_item.selected_wb_product_ids,
            "endpoint": fetch_result.endpoint,
            "coverage_source": fetch_result.coverage_source,
            "coverage_status": coverage["coverage_status"],
            "marketplace_feedback_count": coverage["marketplace_feedback_count"],
            "fetched_reviews_count": coverage["fetched_reviews_count"],
            "pages_fetched": fetch_result.pages_fetched,
            "attempts": fetch_result.attempts,
            "retries": fetch_result.retries,
            "backoff_seconds_total": fetch_result.backoff_seconds_total,
            "http_status": fetch_result.http_status,
            "status": event_status,
            "elapsed_ms": fetch_result.elapsed_ms,
            "payload_feedback_count_if_available": (
                mapped.payload_feedback_count
                if mapped
                else (fetch_result.payload or {}).get("feedbackCount")
            ),
            "payload_feedback_rows_seen": mapped.payload_feedback_rows_seen if mapped else 0,
            "selected_review_rows_seen": mapped.selected_review_rows_seen if mapped else 0,
            "foreign_review_rows_seen": mapped.foreign_review_rows_seen if mapped else 0,
            "foreign_wb_product_ids": sorted(mapped.foreign_wb_product_ids) if mapped else [],
            "reviews_written": write_result.reviews_written if write_result else 0,
            "replies_written": write_result.replies_written if write_result else 0,
            "review_duplicates": write_result.review_duplicates if write_result else 0,
            "reply_duplicates": write_result.reply_duplicates if write_result else 0,
            "known_reviews_seen": fetch_result.known_reviews_seen,
            "fetched_new_reviews": fetch_result.fetched_new_reviews,
            "fetched_new_replies": fetch_result.fetched_new_replies,
            "sync_coverage_status": fetch_result.sync_coverage_status,
            "raw_payload_retained": raw_payload_path is not None,
            "raw_payload_path": str(raw_payload_path) if raw_payload_path else None,
            "error_summary": error_summary,
        },
    )
    _log_progress(manifest, work_item, event_status, write_result)
    manifest.write()


def _log_progress(
    manifest: ReviewRunManifest,
    work_item: RootWorkItem | ProductWorkItem,
    status: str,
    write_result: Any,
) -> None:
    processed = (
        manifest.counters.get("products_processed", 0)
        if work_item.wb_product_id
        else manifest.counters["roots_processed"]
    )
    selected = (
        manifest.counters.get("products_selected", 0)
        if work_item.wb_product_id
        else manifest.counters["roots_selected"]
    )
    if processed <= 5 or processed % 25 == 0 or status == "failed":
        logger.info(
            "Review handled root={} product={} status={} roots={}/{} products={}/{} reviews_written={} replies_written={}",
            work_item.wb_root_id,
            work_item.wb_product_id,
            status,
            manifest.counters["roots_processed"],
            manifest.counters["roots_selected"],
            manifest.counters.get("products_processed", 0),
            manifest.counters.get("products_selected", selected),
            getattr(write_result, "reviews_written", 0),
            getattr(write_result, "replies_written", 0),
        )


def _coverage_row(
    *,
    work_item: RootWorkItem | ProductWorkItem,
    fetch_result: ReviewFetchResult,
    mapped: Any,
    event_status: str,
    error_summary: str | None,
) -> dict[str, Any]:
    product = next(iter(work_item.selected_products.values()))
    fetched_reviews_count = mapped.selected_review_rows_seen if mapped else 0
    if fetch_result.existing_fetched_reviews_count is not None:
        fetched_reviews_count += fetch_result.existing_fetched_reviews_count
    expected_count = fetch_result.marketplace_feedback_count
    group_marketplace_feedback_count = product.feedback_count
    if fetch_result.coverage_source == ROOT_VARIANT_FILTERED_ATTRIBUTION_MODE:
        expected_count = fetched_reviews_count
    if expected_count is None:
        expected_count = product.feedback_count
    if event_status == "failed":
        coverage_status = _failed_coverage_status(fetch_result.http_status)
    elif expected_count is None:
        coverage_status = "unknown"
    elif expected_count == 0 and fetched_reviews_count == 0:
        coverage_status = "full"
    elif fetched_reviews_count >= expected_count:
        coverage_status = "full"
    else:
        coverage_status = "incomplete"

    return {
        "timestamp_utc": utc_now_iso(),
        "source_wb_root_id": product.wb_root_id,
        "source_wb_product_id": product.wb_product_id,
        "group_marketplace_feedback_count": group_marketplace_feedback_count,
        "marketplace_feedback_count": expected_count,
        "fetched_reviews_count": fetched_reviews_count,
        "coverage_status": coverage_status,
        "coverage_source": fetch_result.coverage_source,
        "pages_fetched": fetch_result.pages_fetched,
        "http_status": fetch_result.http_status,
        "foreign_review_rows_seen": mapped.foreign_review_rows_seen if mapped else 0,
        "foreign_wb_product_ids": sorted(mapped.foreign_wb_product_ids) if mapped else [],
        "last_coverage_error": error_summary,
    }


def _load_review_sync_state(work_item: ProductWorkItem) -> ReviewSyncState | None:
    url = runtime_review_sync_state_url(work_item.wb_product_id)
    if not url:
        return None

    try:
        state = load_runtime_review_sync_state(url)
    except Exception as exception:  # pragma: no cover - defensive runtime fallback
        logger.warning(
            "Review sync state unavailable product={} error={}",
            work_item.wb_product_id,
            str(exception),
        )
        return None

    logger.info(
        "Review sync state product={} knownReviews={} unansweredReviews={} marketplaceFeedbackCount={} fetchedReviews={} coverageStatus={}",
        work_item.wb_product_id,
        len(state.known_review_ids),
        len(state.unanswered_review_ids),
        state.marketplace_feedback_count,
        state.fetched_reviews_count,
        state.coverage_status,
    )
    return state


def _failed_coverage_status(http_status: int | None) -> str:
    if http_status == 429:
        return "rate_limited"
    if http_status in {401, 403, 498}:
        return "blocked"
    return "endpoint_unavailable"


def _final_status(manifest: ReviewRunManifest, interrupted: bool) -> str:
    if interrupted:
        return "interrupted"
    if manifest.counters["errors"] and manifest.counters["roots_processed"] > 0:
        return "partial"
    if manifest.counters["errors"]:
        return "failed"
    return "succeeded"


def run_reviews(
    config: ReviewsParserConfig,
    args: argparse.Namespace,
) -> Path:
    if getattr(args, "output_base_dir", None) is not None:
        config = replace(config, output_base_dir=args.output_base_dir)
        config.validate()

    if getattr(args, "max_concurrent", None) is not None:
        config = replace(config, max_concurrent=args.max_concurrent)
        config.validate()

    resume_manifest = ReviewRunManifest.load(args.resume_run_dir) if args.resume_run_dir else None
    source = _resolve_products_source(args, resume_manifest)
    if config.fetch_mode == "product_full" and source.limit_products is not None:
        logger.info(
            "Ignoring reviews limit_products={} in product_full mode; product batch input already defines scope.",
            source.limit_products,
        )
        source = replace(source, limit_products=None)
    if resume_manifest:
        run_dir = resume_manifest.run_dir
        manifest = resume_manifest
        logger.info("Resuming review run {}", manifest.parser_run_id)
    else:
        parser_run_id = _make_run_id()
        run_dir = config.output_base_dir / "runs" / parser_run_id
        run_dir.mkdir(parents=True, exist_ok=False)
        manifest = ReviewRunManifest.create(
            parser_run_id=parser_run_id,
            run_dir=run_dir,
            config_snapshot=config.safe_snapshot(),
            requested_scope={
                "input_products_parser_run_id": source.input_products_parser_run_id,
                "input_products_jsonl": str(source.products_jsonl),
                "limit_products": source.limit_products,
                "source_subcategory": source.source_subcategory,
                **parser_run_scope_from_env(),
            },
            marketplace=config.marketplace,
            repo_dir=BASE_DIR.parent,
        )

    output_paths = _output_paths(run_dir)
    _ensure_contract_files(output_paths)
    manifest.set_output_files(output_paths)
    logger.add(output_paths["runner_log"], encoding="utf-8")
    logger.info(
        "Review run start id={} products_jsonl={} limit={} subcategory={} concurrency={} raw_retention={}",
        manifest.parser_run_id,
        source.products_jsonl,
        source.limit_products,
        source.source_subcategory,
        config.max_concurrent,
        config.retain_raw_payloads,
    )

    interrupted = False
    try:
        root_items = _load_products(source=source, marketplace=config.marketplace, manifest=manifest)
        work_items = _to_review_work_items(root_items, config)
        if not work_items:
            raise ValueError("No selected review work items were built from products input.")

        completed_root_ids = _completed_root_ids(output_paths["review_fetch_results_jsonl"])
        completed_product_ids = _completed_product_ids(output_paths["review_fetch_results_jsonl"])
        if config.fetch_mode == "product_full" and completed_product_ids:
            before = len(work_items)
            work_items = [
                item for item in work_items
                if not isinstance(item, ProductWorkItem)
                or item.wb_product_id not in completed_product_ids
            ]
            skipped_products = before - len(work_items)
            manifest.add_counter("products_skipped", skipped_products)
            logger.info("Resume skip completed review products: {}", skipped_products)
        elif completed_root_ids:
            before = len(work_items)
            work_items = [
                item for item in work_items
                if not isinstance(item, RootWorkItem)
                or item.wb_root_id not in completed_root_ids
            ]
            skipped_roots = before - len(work_items)
            manifest.add_counter("roots_skipped", skipped_roots)
            logger.info("Resume skip completed review roots: {}", skipped_roots)

        manifest.write()
        if not work_items:
            logger.info("Review run has no pending roots after resume skip.")
            return run_dir

        smoke_result = asyncio.run(_smoke_probe(config=config, work_item=work_items[0]))
        manifest.record_fetch_attempts(
            attempts=smoke_result.attempts,
            retries=smoke_result.retries,
            backoff_seconds=smoke_result.backoff_seconds_total,
        )
        manifest.network_check_result = {
            "status": "ok" if smoke_result.status in {"success", "empty"} else "failed",
            "source_wb_root_id": smoke_result.source_wb_root_id,
            "source_wb_product_id": smoke_result.source_wb_product_id,
            "endpoint": smoke_result.endpoint,
            "http_status": smoke_result.http_status,
            "coverage_source": smoke_result.coverage_source,
            "fetch_status": smoke_result.status,
            "error_summary": smoke_result.error_summary,
        }
        manifest.write()
        logger.info("Review smoke probe result: {}", manifest.network_check_result)
        if manifest.network_check_result["status"] != "ok":
            manifest.record_error(
                phase="smoke",
                message=smoke_result.error_summary or "Review smoke probe failed.",
                source_wb_root_id=smoke_result.source_wb_root_id,
                selected_wb_product_ids=work_items[0].selected_wb_product_ids,
                endpoint=smoke_result.endpoint,
                action="stopped",
                http_status=smoke_result.http_status,
                exception_type=smoke_result.exception_type,
            )
            return run_dir

        if args.smoke_only:
            logger.info("Review smoke-only run completed without canonical extraction.")
            return run_dir

        exporter = ReviewCanonicalExporter(
            reviews_path=output_paths["reviews_jsonl"],
            replies_path=output_paths["review_replies_jsonl"],
        )
        handler = lambda item, result: _handle_result(
            work_item=item,
            fetch_result=result,
            config=config,
            manifest=manifest,
            exporter=exporter,
            output_paths=output_paths,
            input_products_parser_run_id=source.input_products_parser_run_id,
            input_products_jsonl=source.products_jsonl,
        )
        asyncio.run(_run_worklist(config=config, work_items=work_items, handler=handler))
    except KeyboardInterrupt:
        interrupted = True
        logger.warning("Review runner interrupted. Writing partial manifest.")
    except Exception as exception:
        manifest.record_error(
            phase="runner",
            message="Review runner failed.",
            action="stopped",
            exception_type=type(exception).__name__,
            details={"exception": str(exception)},
        )
        logger.exception("Review runner failed")
    finally:
        manifest.finish(_final_status(manifest, interrupted))
        manifest.write()
        logger.info("Review run finished id={} status={} counters={}", manifest.parser_run_id, manifest.status, manifest.counters)

    return run_dir


def _string_id(value: Any) -> str | None:
    if value is None:
        return None
    normalized = str(value).strip()
    return normalized or None


def _int_or_none(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Safe manual WB reviews and review replies runner.")
    parser.add_argument("--config", type=Path, default=BASE_DIR / ".env", help="Path to parser .env config.")
    parser.add_argument("--products-run-dir", type=Path, help="Existing product parser run directory.")
    parser.add_argument("--products-jsonl", type=Path, help="Explicit products.jsonl input path.")
    parser.add_argument("--output-base-dir", type=Path, help="Directory where the review run directory will be created.")
    parser.add_argument("--limit-products", type=int, help="Limit selected products before root dedupe.")
    parser.add_argument("--source-subcategory", help="Optional product source_subcategory filter.")
    parser.add_argument("--max-concurrent", type=int, help="Override PARSER_REVIEWS_MAX_CONCURRENT for this run.")
    parser.add_argument("--resume-run-dir", type=Path, help="Existing WB review run directory to resume.")
    parser.add_argument("--smoke-only", action="store_true", help="Run input and endpoint smoke checks only.")
    parser.add_argument("--fail-fast", action="store_true", help="Stop after a terminal root failure.")
    raw_group = parser.add_mutually_exclusive_group()
    raw_group.add_argument(
        "--retain-raw-payloads",
        dest="retain_raw_payloads",
        action="store_true",
        default=None,
        help="Retain compressed root feedback payloads.",
    )
    raw_group.add_argument(
        "--no-retain-raw-payloads",
        dest="retain_raw_payloads",
        action="store_false",
        help="Disable compressed root feedback payload retention.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    config = ReviewsParserConfig.load(args.config)
    if args.fail_fast:
        config = replace(config, fail_fast=True)
    if args.retain_raw_payloads is not None:
        config = replace(config, retain_raw_payloads=args.retain_raw_payloads)
    run_dir = run_reviews(config, args)
    logger.info("Review run directory: {}", run_dir)


if __name__ == "__main__":
    main()

