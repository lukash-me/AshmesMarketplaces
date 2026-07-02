from __future__ import annotations

import argparse
import json
import os
import sys
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

PARSER_ROOT = Path(__file__).resolve().parents[1]
if str(PARSER_ROOT) not in sys.path:
    sys.path.insert(0, str(PARSER_ROOT))

from app.durable_outbox import DurableBatchOutbox, utc_now_iso
from app.market_refresh_runner import PipelineConfig, _resolve_staging_preflight, run_pipeline
from config import BASE_DIR


@dataclass(frozen=True)
class CycleOutboxSummary:
    enabled: bool
    send_attempts: int = 0
    poll_attempts: int = 0
    cleaned_payloads: int = 0
    active_before: int = 0
    active_after: int = 0
    status_counts_before: dict[str, int] | None = None
    status_counts_after: dict[str, int] | None = None
    last_error: str | None = None


def _make_outbox(*, output_base_dir: Path, parser_instance_id: str) -> DurableBatchOutbox | None:
    server_base_url = os.environ.get("PARSER_BATCH_QUEUE_URL")
    if not server_base_url:
        return None

    outbox_dir = Path(os.environ.get("PARSER_OUTBOX_DIR") or output_base_dir / "outbox")
    instance_id = os.environ.get("PARSER_INSTANCE_ID") or parser_instance_id
    return DurableBatchOutbox(
        root_dir=outbox_dir,
        parser_instance_id=instance_id,
        server_base_url=server_base_url,
    )


def _flush_outbox(outbox: DurableBatchOutbox | None, *, passes: int, limit: int | None) -> CycleOutboxSummary:
    if outbox is None:
        return CycleOutboxSummary(enabled=False)

    send_attempts = 0
    poll_attempts = 0
    last_error = None
    status_counts_before = outbox.count_by_status()
    active_before = len(outbox.list_active())
    for _ in range(max(1, passes)):
        try:
            send_attempts += outbox.send_pending_once(limit=limit)
            poll_attempts += outbox.poll_active_once(limit=limit)
        except Exception as exception:
            last_error = str(exception)
    cleaned = outbox.cleanup_completed_payloads()
    status_counts_after = outbox.count_by_status()
    active_after = len(outbox.list_active())
    return CycleOutboxSummary(
        enabled=True,
        send_attempts=send_attempts,
        poll_attempts=poll_attempts,
        cleaned_payloads=cleaned,
        active_before=active_before,
        active_after=active_after,
        status_counts_before=status_counts_before,
        status_counts_after=status_counts_after,
        last_error=last_error,
    )


def _write_cycle_report(
    *,
    config: PipelineConfig,
    report_dir: Path,
    started_at_utc: str,
    pipeline_run_dir: Path | None,
    preflight_outbox: CycleOutboxSummary,
    final_outbox: CycleOutboxSummary,
    status: str,
    error: str | None,
) -> Path:
    report_dir.mkdir(parents=True, exist_ok=True)
    report_path = report_dir / f"parser_cycle_{started_at_utc.replace(':', '').replace('-', '').replace('.', '')}.json"
    payload = {
        "schemaVersion": 1,
        "startedAtUtc": started_at_utc,
        "finishedAtUtc": utc_now_iso(),
        "status": status,
        "configPath": str(config.path),
        "pipelineRunDir": str(pipeline_run_dir) if pipeline_run_dir else None,
        "preflightOutbox": asdict(preflight_outbox),
        "finalOutbox": asdict(final_outbox),
        "error": error,
    }
    report_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return report_path


def _pipeline_status(pipeline_run_dir: Path | None) -> str | None:
    if pipeline_run_dir is None:
        return None
    manifest_path = pipeline_run_dir / "pipeline_manifest.json"
    if not manifest_path.exists():
        return None
    payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    status = payload.get("status")
    return str(status) if status is not None else None


def run_cycle(
    *,
    config_path: Path,
    mode: str,
    stage_to_db: bool,
    connection_string: str | None,
    smoke_max_batches: int | None = None,
    only_proxy: str | None = None,
    only_subcategory: str | None = None,
    outbox_flush_passes: int = 3,
    outbox_flush_limit: int | None = None,
    skip_rank: bool = False,
    skip_products: bool = False,
    skip_logistics: bool = False,
    skip_reviews: bool = False,
    skip_product_details: bool = False,
    dry_run: bool = False,
    test_run: bool = False,
    test_label: str | None = None,
    smoke_source_subcategory: str | None = None,
) -> Path:
    started_at_utc = utc_now_iso()
    config = PipelineConfig.load(config_path)
    mode_config = config.modes.get(mode)
    if mode_config is None:
        raise ValueError(f"Mode '{mode}' is not defined in {config_path}.")

    mode_product_env = {
        str(key): str(value)
        for key, value in (((mode_config.get("product") or {}).get("env") or {}).items())
        if value is not None
    }
    previous_mode_product_env = {key: os.environ.get(key) for key in mode_product_env}
    for key, value in mode_product_env.items():
        os.environ[key] = value

    batching = dict(mode_config.get("batching") or {})
    parser_instance_id = str(
        os.environ.get("PARSER_INSTANCE_ID")
        or batching.get("worker_id")
        or os.environ.get("PARSER_WORKER_ID")
        or "local-worker"
    )
    outbox = _make_outbox(output_base_dir=config.output_base_dir, parser_instance_id=parser_instance_id)
    preflight_outbox = _flush_outbox(outbox, passes=outbox_flush_passes, limit=outbox_flush_limit)

    previous_max_batches = os.environ.get("PARSER_MAX_STREAM_BATCHES")
    previous_subcategory_allowlist = os.environ.get("PARSER_SUBCATEGORY_ALLOWLIST")
    previous_smoke_direct_discovery = os.environ.get("PARSER_SMOKE_DIRECT_DISCOVERY")
    if smoke_max_batches is not None and smoke_max_batches > 0:
        os.environ["PARSER_MAX_STREAM_BATCHES"] = str(smoke_max_batches)
    scoped_subcategory = (only_subcategory or smoke_source_subcategory or "").strip()
    previous_only_proxy = os.environ.get("PARSER_ONLY_PROXY")
    if scoped_subcategory:
        os.environ["PARSER_SUBCATEGORY_ALLOWLIST"] = scoped_subcategory
    if only_proxy and only_proxy.strip():
        os.environ["PARSER_ONLY_PROXY"] = only_proxy.strip()

    pipeline_run_dir: Path | None = None
    final_outbox = CycleOutboxSummary(enabled=bool(outbox))
    status = "succeeded"
    error = None
    try:
        staging = _resolve_staging_preflight(
            stage_to_db_arg=stage_to_db,
            connection_string_arg=connection_string,
        )
        pipeline_run_dir = run_pipeline(
            config=config,
            mode=mode,
            skip_rank=skip_rank,
            skip_products=skip_products,
            skip_logistics=skip_logistics,
            skip_reviews=skip_reviews,
            skip_product_details=skip_product_details,
            dry_run=dry_run,
            stage_to_db=staging.requested,
            connection_string=staging.connection_string,
            connection_string_source=staging.connection_string_source,
            test_run=test_run,
            test_label=test_label,
            batch_outbox=outbox,
        )
        pipeline_status = _pipeline_status(pipeline_run_dir)
        if pipeline_status:
            status = pipeline_status
        if pipeline_status in {"failed", "interrupted"}:
            error = f"Pipeline finished with status {pipeline_status}."
            raise RuntimeError(error)
    except Exception as exception:
        status = "failed"
        error = str(exception)
        raise
    finally:
        for key, value in previous_mode_product_env.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value
        if previous_max_batches is None:
            os.environ.pop("PARSER_MAX_STREAM_BATCHES", None)
        else:
            os.environ["PARSER_MAX_STREAM_BATCHES"] = previous_max_batches
        if previous_subcategory_allowlist is None:
            os.environ.pop("PARSER_SUBCATEGORY_ALLOWLIST", None)
        else:
            os.environ["PARSER_SUBCATEGORY_ALLOWLIST"] = previous_subcategory_allowlist
        if previous_only_proxy is None:
            os.environ.pop("PARSER_ONLY_PROXY", None)
        else:
            os.environ["PARSER_ONLY_PROXY"] = previous_only_proxy
        if previous_smoke_direct_discovery is None:
            os.environ.pop("PARSER_SMOKE_DIRECT_DISCOVERY", None)
        else:
            os.environ["PARSER_SMOKE_DIRECT_DISCOVERY"] = previous_smoke_direct_discovery
        final_outbox = _flush_outbox(outbox, passes=outbox_flush_passes, limit=outbox_flush_limit)
        report_path = _write_cycle_report(
            config=config,
            report_dir=config.output_base_dir / "cycle_reports",
            started_at_utc=started_at_utc,
            pipeline_run_dir=pipeline_run_dir,
            preflight_outbox=preflight_outbox,
            final_outbox=final_outbox,
            status=status,
            error=error,
        )
        print(f"[parser-cycle] report={report_path}")

    if final_outbox.enabled and final_outbox.active_after > 0:
        print(
            "[parser-cycle] safe stop: "
            f"active_outbox_batches={final_outbox.active_after} status_counts={final_outbox.status_counts_after}"
        )
    return pipeline_run_dir or report_path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run one durable parser cycle and exit.")
    parser.add_argument(
        "--config",
        type=Path,
        default=BASE_DIR / "presets" / "production" / "market_refresh_selected_niches_batched.prod.json",
        help="Pipeline preset JSON path.",
    )
    parser.add_argument("--mode", default="batched_full_enrichment")
    parser.add_argument("--stage-to-db", action="store_true")
    parser.add_argument("--connection-string")
    parser.add_argument("--smoke-max-batches", type=int, help="Limit cycle to N streaming batches.")
    parser.add_argument("--smoke-source-subcategory", help="Limit cycle to one source subcategory allowlist value.")
    parser.add_argument("--only-subcategory", help="Limit cycle to one source subcategory allowlist value.")
    parser.add_argument("--only-proxy", help="Assert that the selected niche resolves to this proxy key.")
    parser.add_argument("--outbox-flush-passes", type=int, default=3)
    parser.add_argument("--outbox-flush-limit", type=int)
    parser.add_argument("--skip-rank", action="store_true")
    parser.add_argument("--skip-products", action="store_true")
    parser.add_argument("--skip-logistics", action="store_true")
    parser.add_argument("--skip-reviews", action="store_true")
    parser.add_argument("--skip-product-details", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--test-run", action="store_true")
    parser.add_argument("--test-label")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    try:
        run_cycle(
            config_path=args.config,
            mode=args.mode,
            stage_to_db=args.stage_to_db,
            connection_string=args.connection_string,
            smoke_max_batches=args.smoke_max_batches,
            only_proxy=args.only_proxy,
            only_subcategory=args.only_subcategory,
            smoke_source_subcategory=args.smoke_source_subcategory,
            outbox_flush_passes=args.outbox_flush_passes,
            outbox_flush_limit=args.outbox_flush_limit,
            skip_rank=args.skip_rank,
            skip_products=args.skip_products,
            skip_logistics=args.skip_logistics,
            skip_reviews=args.skip_reviews,
            skip_product_details=args.skip_product_details,
            dry_run=args.dry_run,
            test_run=args.test_run,
            test_label=args.test_label,
        )
    except Exception as exception:
        print(f"[parser-cycle] failed: {exception}", file=sys.stderr)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
