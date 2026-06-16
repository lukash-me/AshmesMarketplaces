from __future__ import annotations

import argparse
import concurrent.futures
import json
import random
import re
import sys
import threading
import time
from dataclasses import dataclass
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
        r"  .\Parser\.venv\Scripts\python.exe .\Parser\logistics_runner.py --help",
        file=sys.stderr,
    )
    raise SystemExit(1) from exception

from config import BASE_DIR
from logistics_contracts import (
    SOURCE_REQUEST_FAMILY,
    WB_CARD_DETAIL_ENDPOINT,
    LogisticsProductInput,
    build_card_detail_params,
    map_card_detail_payload,
    request_fingerprint,
)
from logistics_exporters import (
    LOGISTICS_SNAPSHOT_FIELDS,
    WAREHOUSE_AVAILABILITY_FIELDS,
    append_jsonl,
    iter_jsonl,
    validate_fields,
)
from logistics_manifest import LogisticsRunManifest
from manifest import get_git_commit, utc_now_iso
from run_scope import parser_run_scope_from_env
from wb_logistics_client import LogisticsFetchResult, WbLogisticsClient


@dataclass(frozen=True)
class DeliveryDestination:
    key: str
    name: str
    dest: str
    profile_key: str | None = None
    profile_version: str | None = None
    city: str | None = None
    label: str | None = None
    address: str | None = None
    latitude: float | None = None
    longitude: float | None = None


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

    def record(self, result: LogisticsFetchResult) -> None:
        if not self.enabled:
            return
        with self.lock:
            if result.status == "failed" and result.is_transient:
                self.current_ms = min(self.max_ms, max(self.current_ms * 2, self.min_ms + 250))
                self.success_streak = 0
                return
            if result.status == "succeeded":
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
    return f"wb_logistics_{timestamp}_{commit}"


def _resolve_products_source(args: argparse.Namespace) -> tuple[Path, Path | None]:
    if args.products_jsonl:
        products_jsonl = args.products_jsonl
        return products_jsonl, products_jsonl.parent

    if args.products_run_dir:
        return args.products_run_dir / "products.jsonl", args.products_run_dir

    raise ValueError("Provide --products-run-dir or --products-jsonl.")


def _products_run_id(products_run_dir: Path | None) -> str | None:
    if products_run_dir is None:
        return None

    manifest_path = products_run_dir / "manifest.json"
    if manifest_path.exists():
        try:
            payload = json.loads(manifest_path.read_text(encoding="utf-8"))
            value = payload.get("parser_run_id")
            if value:
                return str(value)
        except (OSError, ValueError):
            pass

    return products_run_dir.name


def _run_dir_for(args: argparse.Namespace) -> tuple[Path, bool]:
    if args.resume and args.output_dir and (args.output_dir / "manifest.json").exists():
        return args.output_dir, True

    output_base_dir = args.output_dir or BASE_DIR / "output"
    run_dir = output_base_dir / "runs" / _make_run_id()
    return run_dir, False


def _output_paths(run_dir: Path) -> dict[str, Path]:
    return {
        "manifest": run_dir / "manifest.json",
        "logistics_snapshots": run_dir / "logistics_snapshots.jsonl",
        "warehouse_availability": run_dir / "warehouse_availability.jsonl",
        "errors": run_dir / "errors.jsonl",
        "runner_log": run_dir / "runner.log",
    }


def _load_products(
    *,
    products_jsonl: Path,
    product_ids: list[str] | None,
    limit: int | None,
    dest: str,
) -> tuple[int, list[LogisticsProductInput]]:
    if not products_jsonl.exists():
        raise FileNotFoundError(f"Products JSONL was not found: {products_jsonl}")

    allowed = {str(item) for item in product_ids} if product_ids else None
    rows_total = 0
    selected: list[LogisticsProductInput] = []
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
                LogisticsProductInput(
                    wb_product_id=wb_product_id,
                    wb_root_id=_string_or_none(row.get("wb_root_id")),
                    seller_id=row.get("seller_id"),
                    seller_name=_string_or_none(row.get("seller_name")),
                    source_category=_string_or_none(row.get("source_category")),
                    source_subcategory=_string_or_none(row.get("source_subcategory")),
                    source_query=_string_or_none(row.get("source_query")),
                    source_region_dest=_string_or_none(row.get("source_region_dest")) or dest,
                )
            )
            seen.add(wb_product_id)
            if limit is not None and len(selected) >= limit:
                break

    return rows_total, selected


def _destination_key(wb_product_id: str, dest: str) -> str:
    return f"{wb_product_id}|{dest}"


def _completed_product_destination_keys(snapshot_path: Path) -> set[str]:
    completed: set[str] = set()
    for row in iter_jsonl(snapshot_path) or []:
        wb_product_id = row.get("wb_product_id")
        source_region_dest = row.get("source_region_dest")
        if wb_product_id and source_region_dest:
            completed.add(_destination_key(str(wb_product_id), str(source_region_dest)))
    return completed


def _load_delivery_profile(config_path: Path, profile_key: str) -> tuple[str | None, list[DeliveryDestination]]:
    payload = json.loads(config_path.read_text(encoding="utf-8"))
    profiles = payload.get("profiles") if isinstance(payload, dict) else None
    if not isinstance(profiles, dict) or profile_key not in profiles:
        available = ", ".join(sorted(str(key) for key in (profiles or {}).keys()))
        raise ValueError(f"Delivery profile '{profile_key}' was not found in {config_path}. Available: {available}")

    profile = profiles[profile_key]
    if not isinstance(profile, dict):
        raise ValueError(f"Delivery profile '{profile_key}' must be an object.")
    version = str(profile.get("version") or payload.get("schema_version") or "1")
    destinations = profile.get("destinations")
    if not isinstance(destinations, list) or not destinations:
        raise ValueError(f"Delivery profile '{profile_key}' must include non-empty destinations.")

    result: list[DeliveryDestination] = []
    for index, item in enumerate(destinations, start=1):
        if not isinstance(item, dict):
            raise ValueError(f"Delivery profile '{profile_key}' destination #{index} must be an object.")
        key = _string_or_none(item.get("key")) or f"destination_{index}"
        name = _string_or_none(item.get("name")) or key
        dest = _string_or_none(item.get("dest"))
        if not dest:
            raise ValueError(f"Delivery profile '{profile_key}' destination '{key}' has no dest.")
        result.append(DeliveryDestination(
            key=key,
            name=name,
            dest=dest,
            profile_key=profile_key,
            profile_version=version,
            city=_string_or_none(item.get("city")) or name,
            label=_string_or_none(item.get("label")),
            address=_string_or_none(item.get("address")),
            latitude=_float_or_none(item.get("latitude")),
            longitude=_float_or_none(item.get("longitude")),
        ))

    return version, result


def _parse_delivery_destination(value: str) -> DeliveryDestination:
    parts = [part.strip() for part in value.split("|")]
    if len(parts) == 1:
        dest = _string_or_none(parts[0])
        if not dest:
            raise ValueError("--delivery-destination must include dest.")
        return DeliveryDestination(key=dest, name=dest, dest=dest, profile_key="manual", profile_version="1")
    if len(parts) != 3:
        raise ValueError("--delivery-destination format must be 'key|name|dest' or 'dest'.")
    key, name, dest = parts
    if not key or not name or not dest:
        raise ValueError("--delivery-destination format must be 'key|name|dest' with non-empty values.")
    return DeliveryDestination(key=key, name=name, dest=dest, profile_key="manual", profile_version="1", city=name)


def _resolve_delivery_destinations(args: argparse.Namespace) -> tuple[str, list[DeliveryDestination]]:
    manual_values = list(getattr(args, "delivery_destination", None) or [])
    if manual_values:
        destinations = [_parse_delivery_destination(value) for value in manual_values]
        return "manual", destinations

    profile_key = _string_or_none(getattr(args, "delivery_profile", None))
    if profile_key:
        profile_config = Path(getattr(args, "delivery_profile_config", None) or BASE_DIR / "presets" / "delivery_profiles.json")
        _, destinations = _load_delivery_profile(profile_config, profile_key)
        return profile_key, destinations

    dest = str(getattr(args, "dest", None) or "12354108")
    return "single_dest", [DeliveryDestination(key="default", name="Default destination", dest=dest)]


def _final_status(manifest: LogisticsRunManifest, *, dry_run: bool) -> str:
    if dry_run:
        return "succeeded"
    if manifest.counters["products_failed"] and manifest.counters["products_succeeded"]:
        return "partial"
    if manifest.counters["products_failed"] and not manifest.counters["products_succeeded"]:
        return "failed"
    if not manifest.counters["products_requested"]:
        return "failed"
    return "succeeded"


def _client_for_thread(
    *,
    client_factory: Callable[[], WbLogisticsClient] | None,
    thread_local: threading.local,
    timeout_sec: int,
    retries: int,
    delay_ms: int,
    throttle: AdaptiveDelay,
) -> WbLogisticsClient:
    if not hasattr(thread_local, "client"):
        thread_local.client = client_factory() if client_factory else WbLogisticsClient(
            timeout_sec=timeout_sec,
            retries=retries,
            delay_ms=delay_ms,
            delay_provider=throttle.sleep,
        )
    return thread_local.client


def _fetch_product_with_retry_queue(
    *,
    product: LogisticsProductInput,
    destination: DeliveryDestination,
    args: argparse.Namespace,
    client_factory: Callable[[], WbLogisticsClient] | None,
    thread_local: threading.local,
    throttle: AdaptiveDelay,
) -> tuple[LogisticsProductInput, DeliveryDestination, LogisticsFetchResult, int]:
    product_dest = destination.dest or product.source_region_dest or ""
    client = _client_for_thread(
        client_factory=client_factory,
        thread_local=thread_local,
        timeout_sec=args.timeout_sec,
        retries=args.retries,
        delay_ms=args.delay_ms,
        throttle=throttle,
    )
    result = client.fetch_product(wb_product_id=product.wb_product_id, dest=product_dest)
    throttle.record(result)
    total_attempts = result.attempts
    total_retries = result.retries
    retry_queue_attempts = 0
    max_retry_queue_passes = max(0, int(getattr(args, "retry_queue_passes", 0) or 0))
    while result.status == "failed" and result.is_transient and retry_queue_attempts < max_retry_queue_passes:
        retry_queue_attempts += 1
        result = client.fetch_product(wb_product_id=product.wb_product_id, dest=product_dest)
        throttle.record(result)
        total_attempts += result.attempts
        total_retries += result.retries
    result.attempts = total_attempts
    result.retries = total_retries
    return product, destination, result, retry_queue_attempts


def _iter_fetch_results(
    *,
    work_items: list[tuple[LogisticsProductInput, DeliveryDestination]],
    args: argparse.Namespace,
    client_factory: Callable[[], WbLogisticsClient] | None,
    throttle: AdaptiveDelay,
) -> Any:
    max_workers = max(1, int(getattr(args, "max_concurrent", 1) or 1))
    if max_workers == 1:
        thread_local = threading.local()
        for product, destination in work_items:
            yield _fetch_product_with_retry_queue(
                product=product,
                destination=destination,
                args=args,
                client_factory=client_factory,
                thread_local=thread_local,
                throttle=throttle,
            )
        return

    thread_local = threading.local()
    with concurrent.futures.ThreadPoolExecutor(max_workers=max_workers) as executor:
        iterator = iter(work_items)
        pending: set[concurrent.futures.Future] = set()
        max_pending = max_workers * 2

        def submit_next() -> bool:
            try:
                product, destination = next(iterator)
            except StopIteration:
                return False
            pending.add(executor.submit(
                _fetch_product_with_retry_queue,
                product=product,
                destination=destination,
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


def run_logistics(
    *,
    args: argparse.Namespace,
    client_factory: Callable[[], WbLogisticsClient] | None = None,
) -> Path:
    products_jsonl, products_run_dir = _resolve_products_source(args)
    run_dir, is_resume_run = _run_dir_for(args)
    run_dir.mkdir(parents=True, exist_ok=is_resume_run)
    paths = _output_paths(run_dir)
    logger.remove()
    log_handler_id = logger.add(paths["runner_log"], encoding="utf-8")

    if is_resume_run:
        manifest = LogisticsRunManifest.load(run_dir)
        profile_key, delivery_destinations = _resolve_delivery_destinations(args)
        manifest.source_region_dest = ",".join(destination.dest for destination in delivery_destinations)
    else:
        profile_key, delivery_destinations = _resolve_delivery_destinations(args)
        manifest = LogisticsRunManifest.create(
            parser_run_id=run_dir.name,
            run_dir=run_dir,
            config_snapshot={
                "marketplace": args.marketplace,
                "dest": args.dest,
                "delivery_profile": getattr(args, "delivery_profile", None),
                "delivery_destinations": [destination.__dict__ for destination in delivery_destinations],
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
                **parser_run_scope_from_env(),
            },
            marketplace=args.marketplace,
            source_request_family=SOURCE_REQUEST_FAMILY,
            source_endpoint=WB_CARD_DETAIL_ENDPOINT,
            source_region_dest=",".join(destination.dest for destination in delivery_destinations),
            input_products_run_id=_products_run_id(products_run_dir),
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
        dest=args.dest,
    )
    completed = _completed_product_destination_keys(paths["logistics_snapshots"]) if args.resume else set()
    all_work_items = [
        (product, destination)
        for product in products
        for destination in delivery_destinations
    ]
    work_items = [
        (product, destination)
        for product, destination in all_work_items
        if _destination_key(product.wb_product_id, destination.dest) not in completed
    ]

    manifest.set_counter("products_requested", len(work_items))
    manifest.set_counter("products_skipped", len(all_work_items) - len(work_items))
    manifest.requested_scope["products_total"] = rows_total
    manifest.requested_scope["unique_products_selected"] = len(products)
    manifest.requested_scope["delivery_profile_key"] = profile_key
    manifest.requested_scope["delivery_destinations"] = [destination.__dict__ for destination in delivery_destinations]
    manifest.write()
    logger.info(
        "Logistics run start id={} products_jsonl={} selected={} skipped={} dry_run={}",
        manifest.parser_run_id,
        products_jsonl,
        len(work_items),
        len(all_work_items) - len(work_items),
        args.dry_run,
    )

    if args.dry_run:
        manifest.finish(_final_status(manifest, dry_run=True))
        manifest.write()
        logger.info("Dry-run completed without network calls.")
        print(f"Logistics run directory: {run_dir}")
        logger.remove(log_handler_id)
        return run_dir

    throttle = AdaptiveDelay(
        initial_ms=args.delay_ms,
        enabled=bool(getattr(args, "adaptive_delay", False)),
    )
    manifest.requested_scope["max_concurrent"] = max(1, int(getattr(args, "max_concurrent", 1) or 1))
    manifest.requested_scope["retry_queue_passes"] = max(0, int(getattr(args, "retry_queue_passes", 0) or 0))
    manifest.requested_scope["checkpoint_interval"] = max(1, int(getattr(args, "checkpoint_interval", 25) or 25))
    manifest.requested_scope["adaptive_delay"] = throttle.snapshot()
    manifest.write()

    checkpoint_interval = max(1, int(getattr(args, "checkpoint_interval", 25) or 25))
    for product, destination, result, retry_queue_attempts in _iter_fetch_results(
        work_items=work_items,
        args=args,
        client_factory=client_factory,
        throttle=throttle,
    ):
        dest = destination.dest
        params = build_card_detail_params(wb_product_id=product.wb_product_id, dest=dest)
        fingerprint = request_fingerprint(endpoint=WB_CARD_DETAIL_ENDPOINT, params=params)
        try:
            manifest.add_counter("network_attempts", result.attempts)
            manifest.add_counter("network_retries", result.retries)
            manifest.add_counter("retry_queue_attempts", retry_queue_attempts)
            if result.status != "succeeded" or result.payload is None:
                manifest.add_counter("products_failed")
                manifest.record_error(
                    wb_product_id=product.wb_product_id,
                    source_region_dest=dest,
                    request_fingerprint=result.request_fingerprint,
                    error_type="request_failed",
                    message=result.message or "WB logistics request failed.",
                    http_status=result.http_status,
                    retry_count=result.retries,
                    is_transient=result.is_transient,
                )
                continue

            snapshots, warehouse_rows = map_card_detail_payload(
                payload=result.payload,
                product_input=product,
                parser_run_id=manifest.parser_run_id,
                marketplace=args.marketplace,
                observed_at_utc=utc_now_iso(),
                source_endpoint=WB_CARD_DETAIL_ENDPOINT,
                request_fingerprint_value=result.request_fingerprint,
                source_region_dest=dest,
                delivery_profile_key=destination.profile_key,
                delivery_destination_name=destination.name,
                delivery_profile_version=destination.profile_version,
                delivery_destination_city=destination.city,
                delivery_destination_label=destination.label,
                delivery_destination_address=destination.address,
                delivery_destination_latitude=destination.latitude,
                delivery_destination_longitude=destination.longitude,
                visible_delivery_status=result.visible_delivery.status if result.visible_delivery else "empty",
                visible_delivery_label=result.visible_delivery.label if result.visible_delivery else None,
                visible_delivery_date=result.visible_delivery.date if result.visible_delivery else None,
                visible_delivery_source=result.visible_delivery.source if result.visible_delivery else None,
                visible_delivery_observed_at_utc=(
                    result.visible_delivery.observed_at_utc if result.visible_delivery else None
                ),
                visible_delivery_raw_payload=result.visible_delivery.raw_payload if result.visible_delivery else None,
            )
            if not snapshots:
                manifest.add_counter("products_failed")
                manifest.record_error(
                    wb_product_id=product.wb_product_id,
                    source_region_dest=dest,
                    request_fingerprint=fingerprint,
                    error_type="product_missing",
                    message="WB logistics payload did not include the requested product.",
                    http_status=result.http_status,
                    retry_count=result.retries,
                    is_transient=False,
                )
                continue

            for row in snapshots:
                validate_fields(row, LOGISTICS_SNAPSHOT_FIELDS, "logistics snapshot")
                append_jsonl(paths["logistics_snapshots"], row)
                manifest.add_counter("snapshot_rows_written")

            for row in warehouse_rows:
                validate_fields(row, WAREHOUSE_AVAILABILITY_FIELDS, "warehouse availability")
                append_jsonl(paths["warehouse_availability"], row)
                manifest.add_counter("warehouse_rows_written")

            manifest.add_counter("products_succeeded")
        except Exception as exception:
            manifest.add_counter("products_failed")
            manifest.record_error(
                wb_product_id=product.wb_product_id,
                source_region_dest=dest,
                request_fingerprint=fingerprint,
                error_type=type(exception).__name__,
                message=str(exception),
                http_status=None,
                retry_count=0,
                is_transient=False,
            )
        finally:
            processed = manifest.counters["products_succeeded"] + manifest.counters["products_failed"]
            if processed <= 5 or processed % checkpoint_interval == 0 or processed == len(work_items):
                manifest.requested_scope["adaptive_delay"] = throttle.snapshot()
                manifest.write()
                logger.info(
                    "Logistics progress products={}/{} succeeded={} failed={} delay={}ms",
                    processed,
                    len(work_items),
                    manifest.counters["products_succeeded"],
                    manifest.counters["products_failed"],
                    throttle.snapshot()["current_delay_ms"],
                )

    manifest.finish(_final_status(manifest, dry_run=False))
    manifest.write()
    logger.info("Logistics run finished id={} status={}", manifest.parser_run_id, manifest.status)
    print(f"Logistics run directory: {run_dir}")
    logger.remove(log_handler_id)
    return run_dir


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Safe manual WB logistics artifact runner.")
    parser.add_argument("--products-run-dir", type=Path, help="Existing product parser run directory.")
    parser.add_argument("--products-jsonl", type=Path, help="Explicit products.jsonl input path.")
    parser.add_argument("--dest", default="12354108", help="WB destination id.")
    parser.add_argument(
        "--delivery-profile",
        help="Named delivery destination profile from --delivery-profile-config. Keeps --dest as fallback when omitted.",
    )
    parser.add_argument(
        "--delivery-profile-config",
        type=Path,
        default=BASE_DIR / "presets" / "delivery_profiles.json",
        help="JSON file with delivery destination profiles.",
    )
    parser.add_argument(
        "--delivery-destination",
        action="append",
        help="Manual destination in format 'key|name|dest' or just 'dest'. Can be repeated.",
    )
    parser.add_argument("--limit", type=_positive_int, help="Maximum number of unique products to request.")
    parser.add_argument("--output-dir", type=Path, help="Output base directory, or existing run dir with --resume.")
    parser.add_argument("--delay-ms", type=_non_negative_int, default=500)
    parser.add_argument("--timeout-sec", type=_positive_int, default=10)
    parser.add_argument("--retries", type=_non_negative_int, default=2)
    parser.add_argument("--max-concurrent", type=_positive_int, default=1)
    parser.add_argument("--checkpoint-interval", type=_positive_int, default=25)
    parser.add_argument("--retry-queue-passes", type=_non_negative_int, default=1)
    parser.add_argument("--adaptive-delay", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--marketplace", default="wb", choices=["wb"])
    parser.add_argument("--product-id", action="append", help="Restrict to one WB product id. Can be repeated.")
    return parser.parse_args()


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


def _float_or_none(value: object) -> float | None:
    if value is None:
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def main() -> None:
    args = parse_args()
    run_logistics(args=args)


if __name__ == "__main__":
    main()
