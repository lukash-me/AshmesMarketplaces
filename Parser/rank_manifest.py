from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from manifest import utc_now_iso


@dataclass
class RankContextResult:
    rank_context_id: str
    rank_context_type: str
    query: str
    source_category: str | None
    source_subcategory: str | None
    status: str = "pending"
    pages_requested: int = 0
    pages_succeeded: int = 0
    pages_empty: int = 0
    pages_failed: int = 0
    rows_written: int = 0
    max_absolute_position: int | None = None
    duplicate_product_ids: int = 0
    warning_count: int = 0
    error_count: int = 0


@dataclass
class RankRunManifest:
    parser_run_id: str
    run_dir: Path
    config_snapshot: dict[str, Any]
    parser_version: str
    started_at_utc: str = field(default_factory=utc_now_iso)
    finished_at_utc: str | None = None
    status: str = "running"
    run_kind: str = "wb_search_rank"
    marketplace: str = "wildberries"
    source_region_dest: str = ""
    endpoint: str = ""
    requested_scope: dict[str, Any] = field(default_factory=dict)
    output_files: dict[str, str] = field(default_factory=dict)
    page_counts: dict[str, int] = field(default_factory=lambda: {
        "requested": 0,
        "succeeded": 0,
        "empty": 0,
        "failed": 0,
        "stopped": 0,
    })
    row_counts: dict[str, int] = field(default_factory=lambda: {
        "rank_rows_written": 0,
        "duplicate_product_ids_within_context": 0,
    })
    context_results: list[dict[str, Any]] = field(default_factory=list)
    error_counts: dict[str, int] = field(default_factory=dict)
    warning_counts: dict[str, int] = field(default_factory=dict)
    wb_error_codes_observed: dict[str, int] = field(default_factory=dict)
    retry_summary: dict[str, int] = field(default_factory=lambda: {"attempts": 0, "retries": 0})
    backoff_summary: dict[str, Any] = field(default_factory=lambda: {"count": 0, "seconds_total": 0.0})
    token_acquisition_status: dict[str, Any] = field(default_factory=lambda: {"status": "not_attempted"})
    network_check_result: dict[str, Any] = field(default_factory=dict)

    @property
    def manifest_path(self) -> Path:
        return self.run_dir / "manifest.json"

    @property
    def errors_path(self) -> Path:
        return self.run_dir / "errors.jsonl"

    def set_output_files(self, files: dict[str, Path]) -> None:
        self.output_files = {key: str(path) for key, path in files.items()}

    def add_context_result(self, result: RankContextResult) -> None:
        self.context_results.append(result.__dict__.copy())

    def record_attempt(self) -> None:
        self.retry_summary["attempts"] += 1

    def record_retry(self) -> None:
        self.retry_summary["retries"] += 1

    def record_backoff(self, seconds: float) -> None:
        self.backoff_summary["count"] += 1
        self.backoff_summary["seconds_total"] = round(
            float(self.backoff_summary["seconds_total"]) + seconds,
            3,
        )

    def record_warning(self, phase: str) -> None:
        self.warning_counts[phase] = self.warning_counts.get(phase, 0) + 1

    def record_wb_code(self, code: str | int | None) -> None:
        if code is None:
            return
        key = str(code)
        self.wb_error_codes_observed[key] = self.wb_error_codes_observed.get(key, 0) + 1

    def record_error(
        self,
        *,
        phase: str,
        message: str,
        rank_context_id: str | None = None,
        source_category: str | None = None,
        source_subcategory: str | None = None,
        query: str | None = None,
        page: int | None = None,
        http_status: int | None = None,
        wb_code: str | int | None = None,
        attempt: int | None = None,
        action: str | None = None,
        details: dict[str, Any] | None = None,
    ) -> None:
        self.error_counts[phase] = self.error_counts.get(phase, 0) + 1
        self.record_wb_code(wb_code or http_status)

        event = {
            "timestamp_utc": utc_now_iso(),
            "phase": phase,
            "rank_context_id": rank_context_id,
            "source_category": source_category,
            "source_subcategory": source_subcategory,
            "query": query,
            "page": page,
            "http_status": http_status,
            "wb_code": wb_code,
            "attempt": attempt,
            "action": action,
            "message": message,
            "details": details or {},
        }

        with self.errors_path.open("a", encoding="utf-8") as file:
            file.write(json.dumps(event, ensure_ascii=False, default=str) + "\n")

    def finish(self, status: str) -> None:
        self.status = status
        self.finished_at_utc = utc_now_iso()

    def write(self) -> None:
        payload = {
            "schema_version": 1,
            "parser_run_id": self.parser_run_id,
            "run_kind": self.run_kind,
            "started_at_utc": self.started_at_utc,
            "finished_at_utc": self.finished_at_utc,
            "status": self.status,
            "marketplace": self.marketplace,
            "endpoint": self.endpoint,
            "requested_scope": self.requested_scope,
            "source_region_dest": self.source_region_dest,
            "output_files": self.output_files,
            "page_counts": self.page_counts,
            "row_counts": self.row_counts,
            "context_results": self.context_results,
            "error_counts": self.error_counts,
            "warning_counts": self.warning_counts,
            "wb_error_codes_observed": self.wb_error_codes_observed,
            "retry_summary": self.retry_summary,
            "backoff_summary": self.backoff_summary,
            "token_acquisition_status": self.token_acquisition_status,
            "network_check_result": self.network_check_result,
            "parser_version": self.parser_version,
            "config_snapshot": self.config_snapshot,
        }

        self.manifest_path.write_text(
            json.dumps(payload, ensure_ascii=False, indent=2, default=str),
            encoding="utf-8",
        )
