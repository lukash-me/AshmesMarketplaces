from __future__ import annotations

import json
import sqlite3
import uuid
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime, timezone
from enum import StrEnum
from hashlib import sha256
from pathlib import Path
from typing import Any, Callable
from urllib import request
from urllib.parse import quote


class OutboxStatus(StrEnum):
    CREATED = "created"
    SEND_PENDING = "send_pending"
    SENT = "sent"
    SERVER_PROCESSING = "server_processing"
    COMPLETED = "completed"
    FAILED_RETRYABLE = "failed_retryable"
    FAILED_FINAL = "failed_final"


ACTIVE_STATUSES = {
    OutboxStatus.CREATED,
    OutboxStatus.SEND_PENDING,
    OutboxStatus.SENT,
    OutboxStatus.SERVER_PROCESSING,
    OutboxStatus.FAILED_RETRYABLE,
}


@dataclass(frozen=True)
class OutboxBatch:
    local_batch_id: str
    parser_instance_id: str
    external_batch_id: str
    source_category: str
    source_subcategory: str
    proxy_key: str
    parser_cycle_id: str | None
    external_proxy_run_id: str | None
    batch_kind: str
    content_hash: str
    payload_path: Path
    status: OutboxStatus
    send_attempts: int
    last_send_at_utc: str | None
    last_poll_at_utc: str | None
    server_status: str | None
    error: str | None
    created_at_utc: str
    updated_at_utc: str


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def content_hash_for_payload(payload: Any) -> str:
    return "sha256:" + sha256(canonical_json_bytes(payload)).hexdigest()


def _default_post_json(url: str, payload: dict[str, Any]) -> Any:
    body = canonical_json_bytes(payload)
    http_request = request.Request(
        url,
        data=body,
        method="POST",
        headers={"Content-Type": "application/json; charset=utf-8"},
    )
    with request.urlopen(http_request, timeout=30) as response:
        raw = response.read().decode("utf-8")
        return _UrllibJsonResponse(response.status, raw)


def _default_patch_json(url: str, payload: dict[str, Any]) -> Any:
    body = canonical_json_bytes(payload)
    http_request = request.Request(
        url,
        data=body,
        method="PATCH",
        headers={"Content-Type": "application/json; charset=utf-8"},
    )
    with request.urlopen(http_request, timeout=30) as response:
        raw = response.read().decode("utf-8")
        return _UrllibJsonResponse(response.status, raw)


def _default_get_json(url: str) -> Any:
    http_request = request.Request(url, method="GET")
    with request.urlopen(http_request, timeout=30) as response:
        raw = response.read().decode("utf-8")
        return _UrllibJsonResponse(response.status, raw)


class _UrllibJsonResponse:
    def __init__(self, status_code: int, raw: str) -> None:
        self.status_code = status_code
        self._raw = raw

    def json(self) -> Any:
        return json.loads(self._raw) if self._raw else {}


class DurableBatchOutbox:
    def __init__(
        self,
        *,
        root_dir: Path,
        parser_instance_id: str,
        server_base_url: str | None = None,
        post_json: Callable[[str, dict[str, Any]], Any] | None = None,
        patch_json: Callable[[str, dict[str, Any]], Any] | None = None,
        get_json: Callable[[str], Any] | None = None,
    ) -> None:
        self.root_dir = Path(root_dir)
        self.payload_dir = self.root_dir / "payloads"
        self.db_path = self.root_dir / "outbox.sqlite3"
        self.parser_instance_id = parser_instance_id
        self.server_base_url = server_base_url.rstrip("/") if server_base_url else None
        self.post_json = post_json or _default_post_json
        self.patch_json = patch_json or _default_patch_json
        self.get_json = get_json or _default_get_json

        self.payload_dir.mkdir(parents=True, exist_ok=True)
        self._init_db()

    @contextmanager
    def _connect(self) -> Any:
        connection = sqlite3.connect(self.db_path)
        connection.row_factory = sqlite3.Row
        try:
            yield connection
            connection.commit()
        finally:
            connection.close()

    def _init_db(self) -> None:
        self.root_dir.mkdir(parents=True, exist_ok=True)
        with self._connect() as connection:
            connection.execute(
                """
                CREATE TABLE IF NOT EXISTS parser_outbox_batches (
                    local_batch_id TEXT PRIMARY KEY,
                    parser_instance_id TEXT NOT NULL,
                    external_batch_id TEXT NOT NULL,
                    source_category TEXT NOT NULL,
                    source_subcategory TEXT NOT NULL,
                    proxy_key TEXT NOT NULL,
                    parser_cycle_id TEXT NULL,
                    external_proxy_run_id TEXT NULL,
                    batch_kind TEXT NOT NULL,
                    content_hash TEXT NOT NULL,
                    payload_path TEXT NOT NULL,
                    status TEXT NOT NULL,
                    send_attempts INTEGER NOT NULL DEFAULT 0,
                    last_send_at_utc TEXT NULL,
                    last_poll_at_utc TEXT NULL,
                    server_status TEXT NULL,
                    error TEXT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                )
                """
            )
            self._ensure_column(connection, "parser_outbox_batches", "parser_cycle_id", "TEXT NULL")
            self._ensure_column(connection, "parser_outbox_batches", "external_proxy_run_id", "TEXT NULL")
            connection.execute(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS ux_parser_outbox_external_batch
                ON parser_outbox_batches (parser_instance_id, external_batch_id)
                """
            )

    def enqueue_batch(
        self,
        *,
        external_batch_id: str,
        source_category: str,
        source_subcategory: str,
        proxy_key: str,
        parser_cycle_id: str | None = None,
        external_proxy_run_id: str | None = None,
        batch_kind: str,
        payload: Any,
    ) -> OutboxBatch:
        existing = self.get_by_external_batch_id(external_batch_id)
        if existing is not None:
            return existing

        local_batch_id = str(uuid.uuid4())
        payload_path = self.payload_dir / f"{local_batch_id}.json"
        content_hash = content_hash_for_payload(payload)
        payload_path.write_text(
            json.dumps(payload, ensure_ascii=False, sort_keys=True, indent=2),
            encoding="utf-8",
        )
        now = utc_now_iso()
        with self._connect() as connection:
            connection.execute(
                """
                INSERT INTO parser_outbox_batches (
                    local_batch_id,
                    parser_instance_id,
                    external_batch_id,
                    source_category,
                    source_subcategory,
                    proxy_key,
                    parser_cycle_id,
                    external_proxy_run_id,
                    batch_kind,
                    content_hash,
                    payload_path,
                    status,
                    send_attempts,
                    created_at_utc,
                    updated_at_utc
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, ?, ?)
                """,
                (
                    local_batch_id,
                    self.parser_instance_id,
                    external_batch_id,
                    source_category,
                    source_subcategory,
                    proxy_key,
                    parser_cycle_id,
                    external_proxy_run_id,
                    batch_kind,
                    content_hash,
                    str(payload_path),
                    OutboxStatus.SEND_PENDING.value,
                    now,
                    now,
                ),
            )
        batch = self.get_batch(local_batch_id)
        if batch is None:
            raise RuntimeError("Outbox batch was not persisted.")
        return batch

    def list_send_pending(self) -> list[OutboxBatch]:
        placeholders = ", ".join("?" for _ in (OutboxStatus.SEND_PENDING, OutboxStatus.FAILED_RETRYABLE))
        return self._query_batches(
            f"""
            SELECT * FROM parser_outbox_batches
            WHERE parser_instance_id = ?
              AND status IN ({placeholders})
            ORDER BY created_at_utc, local_batch_id
            """,
            (
                self.parser_instance_id,
                OutboxStatus.SEND_PENDING.value,
                OutboxStatus.FAILED_RETRYABLE.value,
            ),
        )

    def list_active(self) -> list[OutboxBatch]:
        placeholders = ", ".join("?" for _ in ACTIVE_STATUSES)
        return self._query_batches(
            f"""
            SELECT * FROM parser_outbox_batches
            WHERE parser_instance_id = ?
              AND status IN ({placeholders})
            ORDER BY created_at_utc, local_batch_id
            """,
            (self.parser_instance_id, *(status.value for status in ACTIVE_STATUSES)),
        )

    def count_by_status(self) -> dict[str, int]:
        with self._connect() as connection:
            rows = connection.execute(
                """
                SELECT status, COUNT(*) AS count
                FROM parser_outbox_batches
                WHERE parser_instance_id = ?
                GROUP BY status
                """,
                (self.parser_instance_id,),
            ).fetchall()
        return {str(row["status"]): int(row["count"]) for row in rows}

    def cleanup_completed_payloads(self) -> int:
        cleaned = 0
        completed = self._query_batches(
            """
            SELECT * FROM parser_outbox_batches
            WHERE parser_instance_id = ? AND status = ?
            ORDER BY updated_at_utc, local_batch_id
            """,
            (self.parser_instance_id, OutboxStatus.COMPLETED.value),
        )
        for batch in completed:
            if batch.payload_path.exists():
                batch.payload_path.unlink(missing_ok=True)
                cleaned += 1
        return cleaned

    def list_pollable(self) -> list[OutboxBatch]:
        return self._query_batches(
            """
            SELECT * FROM parser_outbox_batches
            WHERE parser_instance_id = ?
              AND status IN (?, ?)
            ORDER BY created_at_utc, local_batch_id
            """,
            (
                self.parser_instance_id,
                OutboxStatus.SENT.value,
                OutboxStatus.SERVER_PROCESSING.value,
            ),
        )

    def get_batch(self, local_batch_id: str) -> OutboxBatch | None:
        batches = self._query_batches(
            "SELECT * FROM parser_outbox_batches WHERE local_batch_id = ?",
            (local_batch_id,),
        )
        return batches[0] if batches else None

    def get_by_external_batch_id(self, external_batch_id: str) -> OutboxBatch | None:
        batches = self._query_batches(
            """
            SELECT * FROM parser_outbox_batches
            WHERE parser_instance_id = ? AND external_batch_id = ?
            """,
            (self.parser_instance_id, external_batch_id),
        )
        return batches[0] if batches else None

    def send_pending_once(self, *, limit: int | None = None) -> int:
        if not self.server_base_url:
            return 0

        sent_count = 0
        for batch in self.list_send_pending()[:limit]:
            try:
                payload = json.loads(batch.payload_path.read_text(encoding="utf-8"))
                request_payload = {
                    "parserInstanceId": batch.parser_instance_id,
                    "externalBatchId": batch.external_batch_id,
                    "sourceCategory": batch.source_category,
                    "sourceSubcategory": batch.source_subcategory,
                    "proxyKey": batch.proxy_key,
                    "parserCycleId": batch.parser_cycle_id,
                    "externalProxyRunId": batch.external_proxy_run_id,
                    "batchKind": batch.batch_kind,
                    "contentHash": batch.content_hash,
                    "payload": payload,
                }
                response = self.post_json(f"{self.server_base_url}/batches", request_payload)
                if int(getattr(response, "status_code", 0)) >= 400:
                    raise RuntimeError(f"Server returned status {getattr(response, 'status_code', '?')}")
                response_payload = response.json() if callable(getattr(response, "json", None)) else {}
                server_status = str(response_payload.get("status") or "accepted")
                self.record_send_accepted(batch.local_batch_id, server_status=server_status)
                sent_count += 1
            except Exception as exception:
                self.record_send_failure(batch.local_batch_id, error=str(exception))
        return sent_count

    def poll_active_once(self, *, limit: int | None = None) -> int:
        if not self.server_base_url:
            return 0

        polled_count = 0
        for batch in self.list_pollable()[:limit]:
            try:
                response = self.get_json(f"{self.server_base_url}/batches/{batch.external_batch_id}/status")
                if int(getattr(response, "status_code", 0)) >= 400:
                    raise RuntimeError(f"Server returned status {getattr(response, 'status_code', '?')}")
                response_payload = response.json() if callable(getattr(response, "json", None)) else {}
                server_status = str(response_payload.get("status") or "queued")
                self.record_poll_result(batch.local_batch_id, server_status=server_status)
                polled_count += 1
            except Exception as exception:
                self.record_poll_failure(batch.local_batch_id, error=str(exception))
        return polled_count

    def start_proxy_run(
        self,
        *,
        external_proxy_run_id: str,
        parser_cycle_id: str | None = None,
        cycle_kind: str | None = None,
        proxy_key: str,
        source_category: str,
        source_subcategory: str,
        planned_products_count: int,
        downloaded_products_count: int,
        egress_ip: str | None = None,
        token_ref: str | None = None,
        session_status: str | None = None,
        phase: str | None = None,
        planned_ranges_count: int | None = None,
        completed_ranges_count: int | None = None,
        range_progress_percent: float | None = None,
        range_checks_count: int | None = None,
        final_ranges_count: int | None = None,
        empty_ranges_count: int | None = None,
        split_ranges_count: int | None = None,
    ) -> None:
        if not self.server_base_url:
            return

        payload = {
            "parserInstanceId": self.parser_instance_id,
            "externalProxyRunId": external_proxy_run_id,
            "parserCycleId": parser_cycle_id,
            "cycleKind": cycle_kind,
            "proxyKey": proxy_key,
            "sourceCategory": source_category,
            "sourceSubcategory": source_subcategory,
            "plannedProductsCount": max(0, int(planned_products_count)),
            "downloadedProductsCount": max(0, int(downloaded_products_count)),
            "egressIp": egress_ip,
            "tokenRef": token_ref,
            "sessionStatus": session_status,
            "phase": phase,
            "plannedRangesCount": planned_ranges_count,
            "completedRangesCount": completed_ranges_count,
            "rangeProgressPercent": range_progress_percent,
            "rangeChecksCount": range_checks_count,
            "finalRangesCount": final_ranges_count,
            "emptyRangesCount": empty_ranges_count,
            "splitRangesCount": split_ranges_count,
        }
        response = self.post_json(f"{self.server_base_url}/proxy-runs/start", payload)
        if int(getattr(response, "status_code", 0)) >= 400:
            raise RuntimeError(f"Server returned status {getattr(response, 'status_code', '?')}")

    def update_proxy_run_progress(
        self,
        *,
        external_proxy_run_id: str,
        planned_products_count: int,
        downloaded_products_count: int,
        phase: str | None = None,
        planned_ranges_count: int | None = None,
        completed_ranges_count: int | None = None,
        range_progress_percent: float | None = None,
        range_checks_count: int | None = None,
        final_ranges_count: int | None = None,
        empty_ranges_count: int | None = None,
        split_ranges_count: int | None = None,
    ) -> None:
        if not self.server_base_url:
            return

        payload = {
            "parserInstanceId": self.parser_instance_id,
            "plannedProductsCount": max(0, int(planned_products_count)),
            "downloadedProductsCount": max(0, int(downloaded_products_count)),
            "phase": phase,
            "plannedRangesCount": planned_ranges_count,
            "completedRangesCount": completed_ranges_count,
            "rangeProgressPercent": range_progress_percent,
            "rangeChecksCount": range_checks_count,
            "finalRangesCount": final_ranges_count,
            "emptyRangesCount": empty_ranges_count,
            "splitRangesCount": split_ranges_count,
        }
        encoded_run_id = quote(external_proxy_run_id, safe="")
        response = self.patch_json(
            f"{self.server_base_url}/proxy-runs/{encoded_run_id}/progress",
            payload,
        )
        if int(getattr(response, "status_code", 0)) >= 400:
            raise RuntimeError(f"Server returned status {getattr(response, 'status_code', '?')}")

    def finish_proxy_run(
        self,
        *,
        external_proxy_run_id: str,
        status: str,
        planned_products_count: int,
        downloaded_products_count: int,
        error: str | None = None,
    ) -> None:
        if not self.server_base_url:
            return

        payload = {
            "parserInstanceId": self.parser_instance_id,
            "status": status,
            "plannedProductsCount": max(0, int(planned_products_count)),
            "downloadedProductsCount": max(0, int(downloaded_products_count)),
            "error": error,
        }
        encoded_run_id = quote(external_proxy_run_id, safe="")
        response = self.post_json(
            f"{self.server_base_url}/proxy-runs/{encoded_run_id}/finish",
            payload,
        )
        if int(getattr(response, "status_code", 0)) >= 400:
            raise RuntimeError(f"Server returned status {getattr(response, 'status_code', '?')}")

    def record_send_accepted(self, local_batch_id: str, *, server_status: str) -> None:
        status = self._status_from_server_status(server_status, after_send=True)
        self._update_batch(
            local_batch_id,
            status=status,
            server_status=server_status,
            error=None,
            increment_send_attempts=True,
            last_send_at_utc=utc_now_iso(),
        )

    def record_send_failure(self, local_batch_id: str, *, error: str) -> None:
        self._update_batch(
            local_batch_id,
            status=OutboxStatus.FAILED_RETRYABLE,
            error=error,
            increment_send_attempts=True,
            last_send_at_utc=utc_now_iso(),
        )

    def record_poll_result(self, local_batch_id: str, *, server_status: str) -> None:
        status = self._status_from_server_status(server_status, after_send=False)
        batch = self.get_batch(local_batch_id)
        self._update_batch(
            local_batch_id,
            status=status,
            server_status=server_status,
            error=None,
            last_poll_at_utc=utc_now_iso(),
        )
        if status == OutboxStatus.COMPLETED and batch is not None:
            batch.payload_path.unlink(missing_ok=True)

    def record_poll_failure(self, local_batch_id: str, *, error: str) -> None:
        self._update_batch(
            local_batch_id,
            error=error,
            last_poll_at_utc=utc_now_iso(),
        )

    def _status_from_server_status(self, server_status: str, *, after_send: bool) -> OutboxStatus:
        normalized = (server_status or "").strip().lower()
        if normalized == "completed":
            return OutboxStatus.COMPLETED
        if normalized == "failed_final":
            return OutboxStatus.FAILED_FINAL
        if normalized == "failed_retryable":
            return OutboxStatus.FAILED_RETRYABLE
        if normalized == "processing":
            return OutboxStatus.SERVER_PROCESSING
        return OutboxStatus.SENT if after_send else OutboxStatus.SERVER_PROCESSING

    def _update_batch(
        self,
        local_batch_id: str,
        *,
        status: OutboxStatus | None = None,
        server_status: str | None = None,
        error: str | None = None,
        increment_send_attempts: bool = False,
        last_send_at_utc: str | None = None,
        last_poll_at_utc: str | None = None,
    ) -> None:
        assignments = ["updated_at_utc = ?"]
        values: list[Any] = [utc_now_iso()]
        if status is not None:
            assignments.append("status = ?")
            values.append(status.value)
        if server_status is not None:
            assignments.append("server_status = ?")
            values.append(server_status)
        if error is not None:
            assignments.append("error = ?")
            values.append(error)
        else:
            assignments.append("error = NULL")
        if increment_send_attempts:
            assignments.append("send_attempts = send_attempts + 1")
        if last_send_at_utc is not None:
            assignments.append("last_send_at_utc = ?")
            values.append(last_send_at_utc)
        if last_poll_at_utc is not None:
            assignments.append("last_poll_at_utc = ?")
            values.append(last_poll_at_utc)
        values.append(local_batch_id)
        with self._connect() as connection:
            connection.execute(
                f"UPDATE parser_outbox_batches SET {', '.join(assignments)} WHERE local_batch_id = ?",
                tuple(values),
            )

    def _query_batches(self, sql: str, params: tuple[Any, ...]) -> list[OutboxBatch]:
        with self._connect() as connection:
            rows = connection.execute(sql, params).fetchall()
        return [self._row_to_batch(row) for row in rows]

    def _row_to_batch(self, row: sqlite3.Row) -> OutboxBatch:
        return OutboxBatch(
            local_batch_id=str(row["local_batch_id"]),
            parser_instance_id=str(row["parser_instance_id"]),
            external_batch_id=str(row["external_batch_id"]),
            source_category=str(row["source_category"]),
            source_subcategory=str(row["source_subcategory"]),
            proxy_key=str(row["proxy_key"]),
            parser_cycle_id=row["parser_cycle_id"],
            external_proxy_run_id=row["external_proxy_run_id"],
            batch_kind=str(row["batch_kind"]),
            content_hash=str(row["content_hash"]),
            payload_path=Path(str(row["payload_path"])),
            status=OutboxStatus(str(row["status"])),
            send_attempts=int(row["send_attempts"]),
            last_send_at_utc=row["last_send_at_utc"],
            last_poll_at_utc=row["last_poll_at_utc"],
            server_status=row["server_status"],
            error=row["error"],
            created_at_utc=str(row["created_at_utc"]),
            updated_at_utc=str(row["updated_at_utc"]),
        )

    @staticmethod
    def _ensure_column(connection: sqlite3.Connection, table_name: str, column_name: str, definition: str) -> None:
        columns = {str(row["name"]) for row in connection.execute(f"PRAGMA table_info({table_name})").fetchall()}
        if column_name not in columns:
            connection.execute(f"ALTER TABLE {table_name} ADD COLUMN {column_name} {definition}")
