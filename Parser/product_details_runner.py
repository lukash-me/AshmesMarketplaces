from __future__ import annotations

import argparse
import concurrent.futures
import json
import random
import sys
import threading
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

try:
    from loguru import logger
except ModuleNotFoundError as exception:
    print(
        "Parser dependencies are missing in the current Python environment.\n"
        f"Missing module: {exception.name}\n\n"
        "Use the project parser virtual environment:\n"
        r"  .\Parser\.venv\Scripts\python.exe .\Parser\product_details_runner.py --help",
        file=sys.stderr,
    )
    raise SystemExit(1) from exception

from config import BASE_DIR
from logistics_exporters import append_jsonl, iter_jsonl
from manifest import get_git_commit, utc_now_iso
from product_details_contracts import (
    SOURCE_REQUEST_FAMILY,
    ProductDetailInput,
    map_fetch_result,
    map_product_detail_payload,
)
from product_details_manifest import ProductDetailsRunManifest
from wb_product_details_client import ProductDetailFetchResult, WbProductDetailsClient


DETAIL_FIELDS = {
    "schema_version",
    "parser_run_id",
    "marketplace",
    "parsed_at_utc",
    "input_products_parser_run_id",
    "input_products_jsonl",
    "source_request_family",
    "source_endpoint",
    "request_fingerprint",
    "source_category",
    "source_subcategory",
    "source_query",
    "source_region_dest",
    "wb_product_id",
    "wb_root_id",
    "description",
    "characteristics",
    "grouped_options",
    "media_count",
    "status",
    "raw_detail_fields",
}


class AdaptiveDelay:
    def __init__(self, *, initial_ms: int, enabled: bool) -> None:
        self.enabled = enabled
        self.min_ms = max(0, int(initial_ms))
        self.max_ms = max(self.min_ms, self.min_ms * 8, 2_000)
        self.current_ms = max(0, int(initial_ms))
        self.success_streak = 0
        self.lock = threading.Lock()

    def sleep(self) -> None:
        with self.lock:
            current_ms = self.current_ms
        if current_ms <= 0:
            return
        jitter = random.uniform(0.75, 1.25)
        time.sleep((current_ms / 1000.0) * jitter)

    def record(self, result: ProductDetailFetchResult) -> None:
        if not self.enabled:
            return
        with self.lock:
            if result.status == "failed" and result.is_transient:
                self.current_ms = min(self.max_ms, max(self.current_ms * 2, self.min_ms + 250))
                self.success_streak = 0
                return
            if result.status in {"succeeded", "empty"}:
                self.success_streak += 1
                if self.success_streak >= 50 and self.current_ms > self.min_ms:
                    self.current_ms = max(self.min_ms, int(self.current_ms * 0.8))
                    self.success_streak = 0

    def snapshot(self) -> dict[str, int | bool]:
        with self.lock:
            return {
                "enabled": self.enabled,
                "min_delay_ms": self.min_ms,
                "current_delay_ms": self.current_ms,
                "max_delay_ms": self.max_ms,
            }


def _make_run_id() -> str:
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    commit = get_git_commit(BASE_DIR.parent)
    return f"wb_product_details_{timestamp}_{commit}"


def _resolve_products_source(args: argparse.Namespace) -> tuple[Path, Path | None]:
    if args.products_jsonl:
        return args.products_jsonl, args.products_jsonl.parent
    if args.products_run_dir:
        return args.products_run_dir / "products.jsonl", args.products_run_dir
    raise ValueError("Provide --products-run-dir or --products-jsonl.")


def _products_run_id(products_run_dir: Path | None) -> str | None:
    if products_run_dir is None:
        return None
    manifest_path = products_run_dir / "manifest.json"
    if manifest_path.exists():
        try:
            value = json.loads(manifest_path.read_text(encoding="utf-8")).get("parser_run_id")
            return str(value) if value else None
        except (OSError, ValueError):
            pass
    return products_run_dir.name


def _run_dir_for(args: argparse.Namespace) -> tuple[Path, bool]:
    if args.resume and args.output_dir and (args.output_dir / "manifest.json").exists():
        return args.output_dir, True
    output_base_dir = args.output_dir or BASE_DIR / "output"
    return output_base_dir / "runs" / _make_run_id(), False


def _output_paths(run_dir: Path) -> dict[str, Path]:
    return {
        "manifest": run_dir / "manifest.json",
        "product_details": run_dir / "product_details.jsonl",
        "product_detail_fetch_results": run_dir / "product_detail_fetch_results.jsonl",
        "errors": run_dir / "errors.jsonl",
        "runner_log": run_dir / "runner.log",
    }


def _load_products(
    *,
    products_jsonl: Path,
    product_ids: list[str] | None,
    limit: int | None,
) -> tuple[int, list[ProductDetailInput]]:
    if not products_jsonl.exists():
        raise FileNotFoundError(f"Products JSONL was not found: {products_jsonl}")

    allowed = {str(item) for item in product_ids} if product_ids else None
    rows_total = 0
    selected: list[ProductDetailInput] = []
    seen: set[str] = set()

    with products_jsonl.open(encoding="utf-8") as file:
        for line in file:
            if not line.strip():
                continue
            rows_total += 1
            row = json.loads(line)
            wb_product_id = _string_or_none(row.get("wb_product_id"))
            if not wb_product_id or wb_product_id in seen:
                continue
            if allowed is not None and wb_product_id not in allowed:
                continue
            selected.append(
                ProductDetailInput(
                    wb_product_id=wb_product_id,
                    wb_root_id=_string_or_none(row.get("wb_root_id")),
                    source_category=_string_or_none(row.get("source_category")),
                    source_subcategory=_string_or_none(row.get("source_subcategory")),
                    source_query=_string_or_none(row.get("source_query")),
                    source_region_dest=_string_or_none(row.get("source_region_dest")),
                )
            )
            seen.add(wb_product_id)
            if limit is not None and len(selected) >= limit:
                break

    return rows_total, selected


def _completed_product_ids(fetch_results_path: Path) -> set[str]:
    completed: set[str] = set()
    for row in iter_jsonl(fetch_results_path) or []:
        if row.get("status") in {"succeeded", "empty"}:
            wb_product_id = row.get("wb_product_id")
            if wb_product_id:
                completed.add(str(wb_product_id))
    return completed


def _client_for_thread(
    *,
    client_factory: Callable[[], WbProductDetailsClient] | None,
    thread_local: threading.local,
    timeout_sec: int,
    retries: int,
    delay_ms: int,
    throttle: AdaptiveDelay,
) -> WbProductDetailsClient:
    if not hasattr(thread_local, "client"):
        thread_local.client = client_factory() if client_factory else WbProductDetailsClient(
            timeout_sec=timeout_sec,
            retries=retries,
            delay_ms=delay_ms,
            delay_provider=throttle.sleep,
        )
    return thread_local.client


def _fetch_product_with_retry_queue(
    *,
    product: ProductDetailInput,
    args: argparse.Namespace,
    client_factory: Callable[[], WbProductDetailsClient] | None,
    thread_local: threading.local,
    throttle: AdaptiveDelay,
) -> tuple[ProductDetailInput, ProductDetailFetchResult, int]:
    client = _client_for_thread(
        client_factory=client_factory,
        thread_local=thread_local,
        timeout_sec=args.timeout_sec,
        retries=args.retries,
        delay_ms=args.delay_ms,
        throttle=throttle,
    )
    result = client.fetch_product(wb_product_id=product.wb_product_id)
    throttle.record(result)
    total_attempts = result.attempts
    total_retries = result.retries
    retry_queue_attempts = 0
    max_retry_queue_passes = max(0, int(getattr(args, "retry_queue_passes", 0) or 0))
    while result.status == "failed" and result.is_transient and retry_queue_attempts < max_retry_queue_passes:
        retry_queue_attempts += 1
        result = client.fetch_product(wb_product_id=product.wb_product_id)
        throttle.record(result)
        total_attempts += result.attempts
        total_retries += result.retries
    result.attempts = total_attempts
    result.retries = total_retries
    return product, result, retry_queue_attempts


def _iter_fetch_results(
    *,
    products: list[ProductDetailInput],
    args: argparse.Namespace,
    client_factory: Callable[[], WbProductDetailsClient] | None,
    throttle: AdaptiveDelay,
) -> Any:
    max_workers = max(1, int(getattr(args, "max_concurrent", 1) or 1))
    if max_workers == 1:
        thread_local = threading.local()
        for product in products:
            yield _fetch_product_with_retry_queue(
                product=product,
                args=args,
                client_factory=client_factory,
                thread_local=thread_local,
                throttle=throttle,
            )
        return

    thread_local = threading.local()
    with concurrent.futures.ThreadPoolExecutor(max_workers=max_workers) as executor:
        iterator = iter(products)
        pending: set[concurrent.futures.Future] = set()
        max_pending = max_workers * 2

        def submit_next() -> bool:
            try:
                product = next(iterator)
            except StopIteration:
                return False
            pending.add(executor.submit(
                _fetch_product_with_retry_queue,
                product=product,
                args=args,
                client_factory=client_factory,
                thread_local=thread_local,
                throttle=throttle,
            ))
            return True

        for _ in range(max_pending):
            if not submit_next():
                break

        while pending:
            done, pending = concurrent.futures.wait(
                pending,
                return_when=concurrent.futures.FIRST_COMPLETED,
            )
            for future in done:
                yield future.result()
                submit_next()


def run_product_details(
    *,
    args: argparse.Namespace,
    client_factory: Callable[[], WbProductDetailsClient] | None = None,
) -> Path:
    products_jsonl, products_run_dir = _resolve_products_source(args)
    run_dir, is_resume_run = _run_dir_for(args)
    run_dir.mkdir(parents=True, exist_ok=is_resume_run)
    paths = _output_paths(run_dir)
    logger.remove()
    log_handler_id = logger.add(paths["runner_log"], encoding="utf-8")

    input_products_run_id = _products_run_id(products_run_dir)
    if is_resume_run:
        manifest = ProductDetailsRunManifest.load(run_dir)
    else:
        manifest = ProductDetailsRunManifest.create(
            parser_run_id=run_dir.name,
            run_dir=run_dir,
            config_snapshot={
                "marketplace": args.marketplace,
                "limit": args.limit,
                "delay_ms": args.delay_ms,
                "timeout_sec": args.timeout_sec,
                "retries": args.retries,
                "dry_run": args.dry_run,
                "resume": args.resume,
                "product_ids": args.product_id or [],
            },
            requested_scope={
                "products_jsonl": str(products_jsonl.resolve()),
                "product_ids": args.product_id or [],
                "limit": args.limit,
                "source_request_family": SOURCE_REQUEST_FAMILY,
            },
            marketplace=args.marketplace,
            source_request_family=SOURCE_REQUEST_FAMILY,
            input_products_run_id=input_products_run_id,
            input_products_file=str(products_jsonl.resolve()),
            repo_dir=BASE_DIR.parent,
        )
        manifest.set_output_files(paths)

    for path in paths.values():
        if path.name != "manifest.json" and not path.exists():
            path.touch()
    manifest.write()

    rows_total, products = _load_products(
        products_jsonl=products_jsonl,
        product_ids=args.product_id,
        limit=args.limit,
    )
    completed = _completed_product_ids(paths["product_detail_fetch_results"]) if args.resume else set()
    work_items = [product for product in products if product.wb_product_id not in completed]

    manifest.set_counter("products_requested", len(work_items))
    manifest.set_counter("products_skipped", len(products) - len(work_items))
    manifest.requested_scope["products_total"] = rows_total
    manifest.write()
    logger.info(
        "Product details run start id={} products_jsonl={} selected={} skipped={} dry_run={}",
        manifest.parser_run_id,
        products_jsonl,
        len(work_items),
        len(products) - len(work_items),
        args.dry_run,
    )

    if args.dry_run:
        manifest.finish(_final_status(manifest, dry_run=True))
        manifest.write()
        logger.info("Dry-run completed without network calls.")
        print(f"Product details run directory: {run_dir}")
        logger.remove(log_handler_id)
        return run_dir

    throttle = AdaptiveDelay(initial_ms=args.delay_ms, enabled=bool(args.adaptive_delay))
    manifest.requested_scope["max_concurrent"] = max(1, int(args.max_concurrent or 1))
    manifest.requested_scope["retry_queue_passes"] = max(0, int(args.retry_queue_passes or 0))
    manifest.requested_scope["checkpoint_interval"] = max(1, int(args.checkpoint_interval or 25))
    manifest.requested_scope["adaptive_delay"] = throttle.snapshot()
    manifest.write()

    checkpoint_interval = max(1, int(args.checkpoint_interval or 25))
    for product, result, retry_queue_attempts in _iter_fetch_results(
        products=work_items,
        args=args,
        client_factory=client_factory,
        throttle=throttle,
    ):
        parsed_at_utc = utc_now_iso()
        try:
            manifest.add_counter("products_attempted")
            manifest.add_counter("network_attempts", result.attempts)
            manifest.add_counter("network_retries", result.retries)
            manifest.add_counter("retry_queue_attempts", retry_queue_attempts)

            fetch_status = result.status
            fetch_row = map_fetch_result(
                product_input=product,
                parser_run_id=manifest.parser_run_id,
                parsed_at_utc=parsed_at_utc,
                source_endpoint=result.endpoint,
                request_fingerprint_value=result.request_fingerprint,
                status=fetch_status,
                attempts=result.attempts,
                retries=result.retries,
                http_status=result.http_status,
                message=result.message,
                is_transient=result.is_transient,
            )
            append_jsonl(paths["product_detail_fetch_results"], fetch_row)
            manifest.add_counter("fetch_result_rows_written")

            if result.status == "empty":
                manifest.add_counter("products_empty")
                continue

            if result.status != "succeeded" or result.payload is None:
                failure_counter = "products_failed_transient" if result.is_transient else "products_failed_permanent"
                manifest.add_counter(failure_counter)
                manifest.record_error(
                    wb_product_id=product.wb_product_id,
                    request_fingerprint=result.request_fingerprint,
                    error_type="request_failed",
                    message=result.message or "WB product details request failed.",
                    http_status=result.http_status,
                    retry_count=result.retries,
                    is_transient=result.is_transient,
                )
                continue

            row = map_product_detail_payload(
                payload=result.payload,
                product_input=product,
                parser_run_id=manifest.parser_run_id,
                marketplace=args.marketplace,
                parsed_at_utc=parsed_at_utc,
                source_endpoint=result.endpoint,
                request_fingerprint_value=result.request_fingerprint,
                input_products_parser_run_id=input_products_run_id,
                input_products_jsonl=str(products_jsonl.resolve()),
                status="succeeded",
            )
            _validate_fields(row)
            append_jsonl(paths["product_details"], row)
            manifest.add_counter("detail_rows_written")
            manifest.add_counter("products_succeeded")
        except Exception as exception:
            manifest.add_counter("products_failed_permanent")
            manifest.record_error(
                wb_product_id=product.wb_product_id,
                request_fingerprint=result.request_fingerprint,
                error_type=type(exception).__name__,
                message=str(exception),
                http_status=None,
                retry_count=0,
                is_transient=False,
            )
        finally:
            processed = int(manifest.counters["products_attempted"])
            if processed <= 5 or processed % checkpoint_interval == 0 or processed == len(work_items):
                manifest.requested_scope["adaptive_delay"] = throttle.snapshot()
                manifest.write()
                logger.info(
                    "Product details progress products={}/{} succeeded={} empty={} failed_transient={} failed_permanent={} delay={}ms",
                    processed,
                    len(work_items),
                    manifest.counters["products_succeeded"],
                    manifest.counters["products_empty"],
                    manifest.counters["products_failed_transient"],
                    manifest.counters["products_failed_permanent"],
                    throttle.snapshot()["current_delay_ms"],
                )

    manifest.finish(_final_status(manifest, dry_run=False))
    manifest.write()
    logger.info("Product details run finished id={} status={}", manifest.parser_run_id, manifest.status)
    print(f"Product details run directory: {run_dir}")
    logger.remove(log_handler_id)
    return run_dir


def _final_status(manifest: ProductDetailsRunManifest, *, dry_run: bool) -> str:
    if dry_run:
        return "succeeded"
    succeeded = int(manifest.counters.get("products_succeeded", 0) or 0)
    empty = int(manifest.counters.get("products_empty", 0) or 0)
    failed = int(manifest.counters.get("products_failed_transient", 0) or 0) + int(
        manifest.counters.get("products_failed_permanent", 0) or 0
    )
    if failed and (succeeded or empty):
        return "partial"
    if failed and not succeeded and not empty:
        return "failed"
    if not int(manifest.counters.get("products_requested", 0) or 0):
        return "failed"
    return "succeeded"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="WB product description/characteristics artifact runner.")
    parser.add_argument("--products-run-dir", type=Path, help="Existing product parser run directory.")
    parser.add_argument("--products-jsonl", type=Path, help="Explicit products.jsonl input path.")
    parser.add_argument("--limit", type=_positive_int, help="Maximum number of unique products to request.")
    parser.add_argument("--output-dir", type=Path, help="Output base directory, or existing run dir with --resume.")
    parser.add_argument("--delay-ms", type=_non_negative_int, default=300)
    parser.add_argument("--timeout-sec", type=_positive_int, default=10)
    parser.add_argument("--retries", type=_non_negative_int, default=2)
    parser.add_argument("--max-concurrent", type=_positive_int, default=2)
    parser.add_argument("--checkpoint-interval", type=_positive_int, default=100)
    parser.add_argument("--retry-queue-passes", type=_non_negative_int, default=1)
    parser.add_argument("--adaptive-delay", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--marketplace", default="wb", choices=["wb"])
    parser.add_argument("--product-id", action="append", help="Restrict to one WB product id. Can be repeated.")
    return parser.parse_args()


def _validate_fields(row: dict[str, Any]) -> None:
    missing = sorted(DETAIL_FIELDS - set(row.keys()))
    if missing:
        raise ValueError(f"Product detail row is missing fields: {', '.join(missing)}")


def _positive_int(value: str) -> int:
    parsed = int(value)
    if parsed < 1:
        raise argparse.ArgumentTypeError("Value must be positive.")
    return parsed


def _non_negative_int(value: str) -> int:
    parsed = int(value)
    if parsed < 0:
        raise argparse.ArgumentTypeError("Value must be zero or positive.")
    return parsed


def _string_or_none(value: object) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def main() -> None:
    args = parse_args()
    run_product_details(args=args)


if __name__ == "__main__":
    main()
