from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from manifest import get_git_commit, utc_now_iso
from review_exporters import append_jsonl


def _default_counters() -> dict[str, int]:
    return {
        "products_total": 0,
        "products_selected": 0,
        "products_processed": 0,
        "products_with_reviews": 0,
        "products_skipped": 0,
        "roots_selected": 0,
        "roots_processed": 0,
        "roots_succeeded": 0,
        "roots_empty": 0,
        "roots_failed": 0,
        "roots_skipped": 0,
        "reviews_seen": 0,
        "reviews_written": 0,
        "review_duplicates": 0,
        "replies_seen": 0,
        "replies_written": 0,
        "reply_duplicates": 0,
        "errors": 0,
        "retries": 0,
    }


@dataclass
class ReviewRunManifest:
    parser_run_id: str
    run_dir: Path
    config_snapshot: dict[str, Any]
    requested_scope: dict[str, Any]
    parser_version: str
    started_at_utc: str = field(default_factory=utc_now_iso)
    finished_at_utc: str | None = None
    status: str = "running"
    marketplace: str = "wildberries"
    output_files: dict[str, str] = field(default_factory=dict)
    counters: dict[str, int] = field(default_factory=_default_counters)
    error_counts: dict[str, int] = field(default_factory=dict)
    retry_summary: dict[str, Any] = field(default_factory=lambda: {
        "attempts": 0,
        "retries": 0,
        "backoff_seconds_total": 0.0,
    })
    network_check_result: dict[str, Any] = field(default_factory=dict)
    resume_count: int = 0

    @property
    def manifest_path(self) -> Path:
        return self.run_dir / "manifest.json"

    @property
    def errors_path(self) -> Path:
        return self.run_dir / "errors.jsonl"

    @classmethod
    def create(
        cls,
        *,
        parser_run_id: str,
        run_dir: Path,
        config_snapshot: dict[str, Any],
        requested_scope: dict[str, Any],
        marketplace: str,
        repo_dir: Path,
    ) -> "ReviewRunManifest":
        return cls(
            parser_run_id=parser_run_id,
            run_dir=run_dir,
            config_snapshot=config_snapshot,
            requested_scope=requested_scope,
            parser_version=get_git_commit(repo_dir),
            marketplace=marketplace,
        )

    @classmethod
    def load(cls, run_dir: Path) -> "ReviewRunManifest":
        payload = json.loads((run_dir / "manifest.json").read_text(encoding="utf-8"))
        manifest = cls(
            parser_run_id=payload["parser_run_id"],
            run_dir=run_dir,
            config_snapshot=payload.get("config_snapshot", {}),
            requested_scope=payload.get("requested_scope", {}),
            parser_version=payload.get("parser_version", "unknown"),
            started_at_utc=payload.get("started_at_utc") or utc_now_iso(),
            finished_at_utc=payload.get("finished_at_utc"),
            status="running",
            marketplace=payload.get("marketplace", "wildberries"),
            output_files=payload.get("output_files", {}),
            counters={**_default_counters(), **payload.get("counters", {})},
            error_counts=payload.get("error_counts", {}),
            retry_summary={
                "attempts": 0,
                "retries": 0,
                "backoff_seconds_total": 0.0,
                **payload.get("retry_summary", {}),
            },
            network_check_result=payload.get("network_check_result", {}),
            resume_count=int(payload.get("resume_count", 0)) + 1,
        )
        return manifest

    def set_output_files(self, files: dict[str, Path]) -> None:
        self.output_files = {key: str(path) for key, path in files.items()}

    def set_counter(self, name: str, value: int) -> None:
        self.counters[name] = int(value)

    def add_counter(self, name: str, value: int = 1) -> None:
        self.counters[name] = int(self.counters.get(name, 0)) + int(value)

    def record_fetch_attempts(self, *, attempts: int, retries: int, backoff_seconds: float) -> None:
        self.retry_summary["attempts"] += attempts
        self.retry_summary["retries"] += retries
        self.retry_summary["backoff_seconds_total"] = round(
            float(self.retry_summary["backoff_seconds_total"]) + backoff_seconds,
            3,
        )
        self.add_counter("retries", retries)

    def record_error(
        self,
        *,
        phase: str,
        message: str,
        source_wb_root_id: str | None = None,
        selected_wb_product_ids: list[str] | None = None,
        endpoint: str | None = None,
        attempt: int | None = None,
        action: str | None = None,
        http_status: int | None = None,
        wb_code_if_present: str | int | None = None,
        exception_type: str | None = None,
        details: dict[str, Any] | None = None,
    ) -> None:
        self.error_counts[phase] = self.error_counts.get(phase, 0) + 1
        self.add_counter("errors")
        append_jsonl(
            self.errors_path,
            {
                "timestamp_utc": utc_now_iso(),
                "phase": phase,
                "source_wb_root_id": source_wb_root_id,
                "selected_wb_product_ids": selected_wb_product_ids or [],
                "endpoint": endpoint,
                "attempt": attempt,
                "action": action,
                "http_status": http_status,
                "wb_code_if_present": wb_code_if_present,
                "exception_type": exception_type,
                "message": message,
                "details": details or {},
            },
        )

    def finish(self, status: str) -> None:
        self.status = status
        self.finished_at_utc = utc_now_iso()

    def write(self) -> None:
        self.manifest_path.write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "parser_run_id": self.parser_run_id,
                    "started_at_utc": self.started_at_utc,
                    "finished_at_utc": self.finished_at_utc,
                    "status": self.status,
                    "marketplace": self.marketplace,
                    "requested_scope": self.requested_scope,
                    "output_files": self.output_files,
                    "counters": self.counters,
                    "error_counts": self.error_counts,
                    "retry_summary": self.retry_summary,
                    "network_check_result": self.network_check_result,
                    "parser_version": self.parser_version,
                    "config_snapshot": self.config_snapshot,
                    "resume_count": self.resume_count,
                },
                ensure_ascii=False,
                indent=2,
                default=str,
            ),
            encoding="utf-8",
        )
