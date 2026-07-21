from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from logistics_exporters import append_jsonl
from manifest import get_git_commit, utc_now_iso


def _default_counters() -> dict[str, int]:
    return {
        "products_requested": 0,
        "products_attempted": 0,
        "products_succeeded": 0,
        "products_empty": 0,
        "products_failed_transient": 0,
        "products_failed_permanent": 0,
        "products_skipped": 0,
        "detail_rows_written": 0,
        "fetch_result_rows_written": 0,
        "error_rows_written": 0,
        "network_attempts": 0,
        "network_retries": 0,
        "retry_queue_attempts": 0,
    }


@dataclass
class ProductDetailsRunManifest:
    parser_run_id: str
    run_dir: Path
    config_snapshot: dict[str, Any]
    requested_scope: dict[str, Any]
    parser_version: str
    marketplace: str
    source_request_family: str
    input_products_run_id: str | None
    input_products_file: str
    started_at_utc: str = field(default_factory=utc_now_iso)
    finished_at_utc: str | None = None
    status: str = "running"
    output_files: dict[str, str] = field(default_factory=dict)
    counters: dict[str, int] = field(default_factory=_default_counters)
    error_counts: dict[str, int] = field(default_factory=dict)
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
        source_request_family: str,
        input_products_run_id: str | None,
        input_products_file: str,
        repo_dir: Path,
    ) -> "ProductDetailsRunManifest":
        return cls(
            parser_run_id=parser_run_id,
            run_dir=run_dir,
            config_snapshot=config_snapshot,
            requested_scope=requested_scope,
            parser_version=get_git_commit(repo_dir),
            marketplace=marketplace,
            source_request_family=source_request_family,
            input_products_run_id=input_products_run_id,
            input_products_file=input_products_file,
        )

    @classmethod
    def load(cls, run_dir: Path) -> "ProductDetailsRunManifest":
        payload = json.loads((run_dir / "manifest.json").read_text(encoding="utf-8"))
        return cls(
            parser_run_id=payload["parser_run_id"],
            run_dir=run_dir,
            config_snapshot=payload.get("config_snapshot", {}),
            requested_scope=payload.get("requested_scope", {}),
            parser_version=payload.get("parser_version", "unknown"),
            marketplace=payload.get("marketplace", "wb"),
            source_request_family=payload.get("source_request_family", "basket_card_info"),
            input_products_run_id=payload.get("input_products_run_id"),
            input_products_file=payload.get("input_products_file", ""),
            started_at_utc=payload.get("started_at_utc") or utc_now_iso(),
            finished_at_utc=payload.get("finished_at_utc"),
            status="running",
            output_files=payload.get("output_files", {}),
            counters={**_default_counters(), **payload.get("counters", {})},
            error_counts=payload.get("error_counts", {}),
            resume_count=int(payload.get("resume_count", 0)) + 1,
        )

    def set_output_files(self, files: dict[str, Path]) -> None:
        self.output_files = {key: str(path) for key, path in files.items()}

    def add_counter(self, name: str, value: int = 1) -> None:
        self.counters[name] = int(self.counters.get(name, 0)) + int(value)

    def set_counter(self, name: str, value: int) -> None:
        self.counters[name] = int(value)

    def record_error(
        self,
        *,
        wb_product_id: str | None,
        request_fingerprint: str | None,
        error_type: str,
        message: str,
        http_status: int | None = None,
        retry_count: int = 0,
        is_transient: bool = False,
    ) -> None:
        self.error_counts[error_type] = self.error_counts.get(error_type, 0) + 1
        self.add_counter("error_rows_written")
        append_jsonl(
            self.errors_path,
            {
                "schema_version": 1,
                "parser_run_id": self.parser_run_id,
                "observed_at_utc": utc_now_iso(),
                "wb_product_id": wb_product_id,
                "request_fingerprint": request_fingerprint,
                "error_type": error_type,
                "message": message,
                "http_status": http_status,
                "retry_count": retry_count,
                "is_transient": is_transient,
            },
        )

    def finish(self, status: str) -> None:
        self.status = status
        self.finished_at_utc = utc_now_iso()

    def write(self) -> None:
        requested = int(self.counters.get("products_requested", 0) or 0)
        attempted = int(self.counters.get("products_attempted", 0) or 0)
        succeeded = int(self.counters.get("products_succeeded", 0) or 0)
        empty = int(self.counters.get("products_empty", 0) or 0)
        failed = int(self.counters.get("products_failed_transient", 0) or 0) + int(
            self.counters.get("products_failed_permanent", 0) or 0
        )
        self.manifest_path.write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "parser_run_id": self.parser_run_id,
                    "run_kind": "product_details",
                    "marketplace": self.marketplace,
                    "started_at_utc": self.started_at_utc,
                    "finished_at_utc": self.finished_at_utc,
                    "status": self.status,
                    "source_request_family": self.source_request_family,
                    "input_products_run_id": self.input_products_run_id,
                    "input_products_file": self.input_products_file,
                    **self.counters,
                    "requested_scope": self.requested_scope,
                    "coverage": {
                        "products_requested": requested,
                        "products_attempted": attempted,
                        "products_succeeded": succeeded,
                        "products_empty": empty,
                        "products_failed": failed,
                        "attempted_percent": round((attempted / requested) * 100, 2) if requested else 0,
                        "succeeded_percent": round((succeeded / requested) * 100, 2) if requested else 0,
                        "completed_percent": round(((succeeded + empty) / requested) * 100, 2) if requested else 0,
                    },
                    "output_files": self.output_files,
                    "counters": self.counters,
                    "error_counts": self.error_counts,
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
