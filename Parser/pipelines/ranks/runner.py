from __future__ import annotations

import argparse
import math
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

PARSER_ROOT = Path(__file__).resolve().parents[2]
if str(PARSER_ROOT) not in sys.path:
    sys.path.insert(0, str(PARSER_ROOT))

try:
    from loguru import logger
except ModuleNotFoundError as exception:
    print(
        "Parser dependencies are missing in the current Python environment.\n"
        f"Missing module: {exception.name}\n\n"
        r"Use: .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\ranks\runner.py --config .\Parser\presets\home_goods_search_rank_demo.json",
        file=sys.stderr,
    )
    raise SystemExit(1) from exception

from config import BASE_DIR
from get_token import get_token
from manifest import get_git_commit, utc_now_iso
from rank_config import RankContextConfig, RankParserConfig, SUPPORTED_RANK_CONTEXT_TYPES
from rank_contracts import (
    WB_SEARCH_ENDPOINT,
    build_page_fetch_event,
    build_rank_rows,
)
from rank_exporters import append_jsonl, append_many_jsonl
from rank_manifest import RankContextResult, RankRunManifest
from run_scope import parser_run_scope_from_env
from wb_rank_fetcher import WbRankFetcher


def _make_run_id() -> str:
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    commit = get_git_commit(BASE_DIR.parent)
    return f"wb_rank_{timestamp}_{commit}"


def _make_manifest(config: RankParserConfig, run_dir: Path, parser_run_id: str) -> RankRunManifest:
    manifest = RankRunManifest(
        parser_run_id=parser_run_id,
        run_dir=run_dir,
        config_snapshot=config.safe_snapshot(),
        parser_version=get_git_commit(BASE_DIR.parent),
        marketplace=config.marketplace,
        source_region_dest=config.source_region_dest,
        endpoint=WB_SEARCH_ENDPOINT,
        requested_scope={
            "contexts": [
                {
                    "rank_context_id": context.id,
                    "rank_context_type": context.type,
                    "source_category": context.source_category,
                    "source_subcategory": context.source_subcategory,
                    "query": context.query,
                    "sort": context.sort,
                    "filters": context.filters,
                    "top_n": config.top_n_for(context),
                    "source_region_dest": config.dest_for(context),
                }
                for context in config.contexts
            ],
            "page_size": config.page_size,
            **parser_run_scope_from_env(),
        },
    )
    manifest.set_output_files(
        {
            "manifest": run_dir / "manifest.json",
            "product_rank_snapshots_jsonl": run_dir / "product_rank_snapshots.jsonl",
            "rank_page_fetches_jsonl": run_dir / "rank_page_fetches.jsonl",
            "errors_jsonl": run_dir / "errors.jsonl",
            "runner_log": run_dir / "runner.log",
        }
    )
    return manifest


def _acquire_token(config: RankParserConfig, manifest: RankRunManifest) -> str | None:
    if config.wb_token_secret:
        manifest.token_acquisition_status = {"status": "provided_by_config"}
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


def _selected_contexts(config: RankParserConfig, context_id: str | None) -> list[RankContextConfig]:
    if not context_id:
        return config.contexts

    selected = [context for context in config.contexts if context.id == context_id]
    if not selected:
        raise ValueError(f"Rank context was not found in preset: {context_id}")
    return selected


def _safe_status(manifest: RankRunManifest, interrupted: bool) -> str:
    if interrupted:
        return "interrupted"
    if manifest.error_counts and manifest.row_counts["rank_rows_written"] > 0:
        return "partial"
    if manifest.error_counts and manifest.row_counts["rank_rows_written"] == 0:
        return "failed"
    return "succeeded"


def _sanitize_context_id(value: str) -> str:
    return re.sub(r"[^A-Za-z0-9_-]+", "_", value).strip("_") or "context"


def run_rank_parser(
    config: RankParserConfig,
    *,
    context_id: str | None = None,
    top_n_override: int | None = None,
    max_pages_override: int | None = None,
    smoke_only: bool = False,
) -> Path:
    parser_run_id = _make_run_id()
    run_dir = config.output_base_dir / "runs" / parser_run_id
    run_dir.mkdir(parents=True, exist_ok=False)

    manifest = _make_manifest(config, run_dir, parser_run_id)
    log_sink_id = logger.add(run_dir / "runner.log", encoding="utf-8")

    rank_rows_path = run_dir / "product_rank_snapshots.jsonl"
    page_fetches_path = run_dir / "rank_page_fetches.jsonl"
    interrupted = False

    try:
        selected_contexts = _selected_contexts(config, context_id)
        token = _acquire_token(config, manifest)
        cookies = {"x_wbaas_token": token} if token else None
        fetcher = WbRankFetcher(
            cookies=cookies,
            timeout=config.timeout_seconds,
            max_retries=config.max_retries,
            request_delay_bounds=(
                config.request_delay_min_seconds,
                config.request_delay_max_seconds,
            ),
            attempt_recorder=manifest.record_attempt,
            retry_recorder=manifest.record_retry,
            backoff_recorder=manifest.record_backoff,
        )

        manifest.write()

        for context in selected_contexts:
            context_result = RankContextResult(
                rank_context_id=context.id,
                rank_context_type=context.type,
                query=context.query,
                source_category=context.source_category,
                source_subcategory=context.source_subcategory,
                status="running",
            )

            if context.type not in SUPPORTED_RANK_CONTEXT_TYPES:
                context_result.status = "failed"
                context_result.error_count += 1
                manifest.record_error(
                    phase="context",
                    rank_context_id=context.id,
                    source_category=context.source_category,
                    source_subcategory=context.source_subcategory,
                    query=context.query,
                    message=f"Rank context type is not implemented in v1: {context.type}",
                    action="skipped",
                )
                manifest.add_context_result(context_result)
                manifest.write()
                if config.fail_fast:
                    break
                continue

            logger.info("Parsing rank context: {}", context.id)
            dest = config.dest_for(context)
            context_top_n = top_n_override or config.top_n_for(context)
            max_pages = max_pages_override or math.ceil(context_top_n / config.page_size)
            if smoke_only:
                max_pages = 1
                context_top_n = config.page_size

            seen_product_ids: set[str] = set()
            context_slug = _sanitize_context_id(context.id)

            for page in range(1, max_pages + 1):
                manifest.page_counts["requested"] += 1
                context_result.pages_requested += 1
                fetch_result = fetcher.fetch_page(context=context, page=page, dest=dest)
                observed_at_utc = utc_now_iso()
                products_count = len(fetch_result.products)
                page_event = build_page_fetch_event(
                    parser_run_id=parser_run_id,
                    observed_at_utc=observed_at_utc,
                    context=context,
                    source_region_dest=dest,
                    request_fingerprint_value=fetch_result.request_fingerprint,
                    page=page,
                    status=fetch_result.status,
                    product_count=products_count,
                    response_total_value=fetch_result.response_total,
                    retry_count=fetch_result.retry_count,
                    http_status=fetch_result.http_status,
                    wb_code=fetch_result.wb_code,
                    message=fetch_result.message,
                )
                append_jsonl(page_fetches_path, page_event)

                if fetch_result.status == "failed":
                    manifest.page_counts["failed"] += 1
                    context_result.pages_failed += 1
                    context_result.error_count += 1
                    manifest.record_error(
                        phase="rank_fetch",
                        rank_context_id=context.id,
                        source_category=context.source_category,
                        source_subcategory=context.source_subcategory,
                        query=context.query,
                        page=page,
                        http_status=fetch_result.http_status,
                        wb_code=fetch_result.wb_code,
                        action="stopped",
                        message=fetch_result.message or "Rank page fetch failed.",
                    )
                    break

                if fetch_result.status == "empty":
                    manifest.page_counts["empty"] += 1
                    context_result.pages_empty += 1
                    break

                manifest.page_counts["succeeded"] += 1
                context_result.pages_succeeded += 1

                if products_count < config.page_size and page < max_pages:
                    manifest.record_warning("irregular_rank_page")
                    context_result.warning_count += 1

                if smoke_only:
                    manifest.network_check_result[context_slug] = {
                        "status": fetch_result.status,
                        "page": page,
                        "product_count": products_count,
                        "response_total": fetch_result.response_total,
                    }
                    break

                remaining_slots = context_top_n - context_result.rows_written
                rows = build_rank_rows(
                    products=fetch_result.products,
                    parser_run_id=parser_run_id,
                    observed_at_utc=observed_at_utc,
                    marketplace=config.marketplace,
                    context=context,
                    source_region_dest=dest,
                    request_fingerprint_value=fetch_result.request_fingerprint,
                    page=page,
                    page_size=config.page_size,
                    response_total_value=fetch_result.response_total,
                    remaining_slots=remaining_slots,
                )

                if not smoke_only:
                    append_many_jsonl(rank_rows_path, rows)

                for row in rows:
                    product_id = row["wb_product_id"]
                    if product_id in seen_product_ids:
                        context_result.duplicate_product_ids += 1
                        manifest.row_counts["duplicate_product_ids_within_context"] += 1
                    seen_product_ids.add(product_id)
                    context_result.max_absolute_position = row["absolute_position"]

                context_result.rows_written += len(rows)
                manifest.row_counts["rank_rows_written"] += len(rows)

                if context_result.rows_written >= context_top_n:
                    break

                if products_count < config.page_size:
                    break

            if context_result.status == "running":
                if context_result.rows_written or (smoke_only and context_result.pages_succeeded):
                    context_result.status = "succeeded"
                elif context_result.pages_failed:
                    context_result.status = "failed"
                else:
                    context_result.status = "partial"

            manifest.add_context_result(context_result)
            manifest.write()
    except KeyboardInterrupt:
        interrupted = True
        logger.warning("Rank parser interrupted. Writing partial manifest.")
    except Exception as exception:
        manifest.record_error(
            phase="runner",
            message="Rank runner failed.",
            action="stopped",
            details={"exception": str(exception)},
        )
        logger.exception("Rank runner failed")
    finally:
        manifest.finish(_safe_status(manifest, interrupted))
        manifest.write()
        logger.remove(log_sink_id)

    return run_dir


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Manual WB search-rank snapshot runner.")
    parser.add_argument(
        "--config",
        type=Path,
        default=BASE_DIR / "presets" / "home_goods_search_rank_demo.json",
        help="Rank preset JSON path.",
    )
    parser.add_argument("--context-id", help="Run only one configured rank context.")
    parser.add_argument("--top-n", type=int, help="Override top_n for this run.")
    parser.add_argument("--max-pages", type=int, help="Override max pages for this run.")
    parser.add_argument("--smoke-only", action="store_true", help="Fetch one page per context without writing rank rows.")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    config = RankParserConfig.load(args.config)
    run_dir = run_rank_parser(
        config,
        context_id=args.context_id,
        top_n_override=args.top_n,
        max_pages_override=args.max_pages,
        smoke_only=args.smoke_only,
    )
    logger.info("Rank run directory: {}", run_dir)


if __name__ == "__main__":
    main()

