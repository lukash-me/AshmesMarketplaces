from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
import sys
from types import SimpleNamespace

PARSER_ROOT = Path(__file__).resolve().parents[1]
if str(PARSER_ROOT) not in sys.path:
    sys.path.insert(0, str(PARSER_ROOT))

from app.durable_outbox import DurableBatchOutbox, OutboxStatus


class DurableBatchOutboxTests(unittest.TestCase):
    def test_enqueue_batch_persists_metadata_and_payload(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            outbox = DurableBatchOutbox(root_dir=Path(temp), parser_instance_id="parser-a")

            batch = outbox.enqueue_batch(
                external_batch_id="batch-1",
                source_category="Товары для дома",
                source_subcategory="Коврики для ванной",
                proxy_key="local",
                batch_kind="complete_card_batch",
                payload={"rows": [{"wbProductId": 1}]},
            )

            self.assertEqual(batch.status, OutboxStatus.SEND_PENDING)
            self.assertTrue(batch.payload_path.exists())
            self.assertTrue(batch.content_hash.startswith("sha256:"))
            saved_payload = json.loads(batch.payload_path.read_text(encoding="utf-8"))
            self.assertEqual(saved_payload["rows"][0]["wbProductId"], 1)

    def test_reopened_outbox_finds_unsent_batch_after_restart(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            first = DurableBatchOutbox(root_dir=root, parser_instance_id="parser-a")
            first.enqueue_batch(
                external_batch_id="batch-1",
                source_category="Товары для дома",
                source_subcategory="Коврики для ванной",
                proxy_key="local",
                batch_kind="complete_card_batch",
                payload={"rows": [1]},
            )

            second = DurableBatchOutbox(root_dir=root, parser_instance_id="parser-a")
            pending = second.list_send_pending()

            self.assertEqual(len(pending), 1)
            self.assertEqual(pending[0].external_batch_id, "batch-1")
            self.assertEqual(pending[0].status, OutboxStatus.SEND_PENDING)

    def test_duplicate_enqueue_returns_existing_row_without_rewriting_payload(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            outbox = DurableBatchOutbox(root_dir=Path(temp), parser_instance_id="parser-a")
            first = outbox.enqueue_batch(
                external_batch_id="batch-1",
                source_category="Товары для дома",
                source_subcategory="Коврики для ванной",
                proxy_key="local",
                batch_kind="complete_card_batch",
                payload={"rows": [1]},
            )
            second = outbox.enqueue_batch(
                external_batch_id="batch-1",
                source_category="Товары для дома",
                source_subcategory="Коврики для ванной",
                proxy_key="local",
                batch_kind="complete_card_batch",
                payload={"rows": [2]},
            )

            self.assertEqual(first.local_batch_id, second.local_batch_id)
            self.assertEqual(len(outbox.list_active()), 1)
            self.assertEqual(json.loads(second.payload_path.read_text(encoding="utf-8"))["rows"], [1])

    def test_send_failure_keeps_payload_and_marks_retryable(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            outbox = DurableBatchOutbox(
                root_dir=Path(temp),
                parser_instance_id="parser-a",
                server_base_url="http://api.local/api/v1/parser",
                post_json=lambda _url, _payload: (_ for _ in ()).throw(RuntimeError("network down")),
            )
            batch = outbox.enqueue_batch(
                external_batch_id="batch-1",
                source_category="Товары для дома",
                source_subcategory="Коврики для ванной",
                proxy_key="local",
                batch_kind="complete_card_batch",
                payload={"rows": [1]},
            )

            outbox.send_pending_once()
            updated = outbox.get_batch(batch.local_batch_id)

            self.assertIsNotNone(updated)
            self.assertEqual(updated.status, OutboxStatus.FAILED_RETRYABLE)
            self.assertEqual(updated.send_attempts, 1)
            self.assertTrue(updated.payload_path.exists())
            self.assertIn("network down", updated.error or "")

    def test_accepted_send_does_not_delete_payload(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            outbox = DurableBatchOutbox(
                root_dir=Path(temp),
                parser_instance_id="parser-a",
                server_base_url="http://api.local/api/v1/parser",
                post_json=lambda _url, _payload: SimpleNamespace(status_code=202, json=lambda: {"status": "accepted"}),
            )
            batch = outbox.enqueue_batch(
                external_batch_id="batch-1",
                source_category="Товары для дома",
                source_subcategory="Коврики для ванной",
                proxy_key="local",
                batch_kind="complete_card_batch",
                payload={"rows": [1]},
            )

            outbox.send_pending_once()
            updated = outbox.get_batch(batch.local_batch_id)

            self.assertIsNotNone(updated)
            self.assertEqual(updated.status, OutboxStatus.SENT)
            self.assertEqual(updated.server_status, "accepted")
            self.assertTrue(updated.payload_path.exists())

    def test_send_includes_parser_cycle_and_proxy_run_metadata(self) -> None:
        captured: dict[str, object] = {}

        def post_json(_url: str, payload: dict[str, object]) -> SimpleNamespace:
            captured.update(payload)
            return SimpleNamespace(status_code=202, json=lambda: {"status": "accepted"})

        with tempfile.TemporaryDirectory() as temp:
            outbox = DurableBatchOutbox(
                root_dir=Path(temp),
                parser_instance_id="parser-a",
                server_base_url="http://api.local/api/v1/parser",
                post_json=post_json,
            )
            outbox.enqueue_batch(
                external_batch_id="batch-1",
                source_category="category",
                source_subcategory="niche",
                proxy_key="proxy-1",
                batch_kind="complete_card_batch",
                payload={"rows": [1]},
                parser_cycle_id="cycle-1",
                external_proxy_run_id="cycle-1:proxy-1:category:niche",
            )

            outbox.send_pending_once()

        self.assertEqual(captured["parserCycleId"], "cycle-1")
        self.assertEqual(captured["externalProxyRunId"], "cycle-1:proxy-1:category:niche")

    def test_completed_poll_deletes_payload_but_keeps_journal_row(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            outbox = DurableBatchOutbox(
                root_dir=Path(temp),
                parser_instance_id="parser-a",
                server_base_url="http://api.local/api/v1/parser",
                get_json=lambda _url: SimpleNamespace(status_code=200, json=lambda: {"status": "completed"}),
            )
            batch = outbox.enqueue_batch(
                external_batch_id="batch-1",
                source_category="Товары для дома",
                source_subcategory="Коврики для ванной",
                proxy_key="local",
                batch_kind="complete_card_batch",
                payload={"rows": [1]},
            )
            outbox.record_send_accepted(batch.local_batch_id, server_status="queued")

            outbox.poll_active_once()
            updated = outbox.get_batch(batch.local_batch_id)

            self.assertIsNotNone(updated)
            self.assertEqual(updated.status, OutboxStatus.COMPLETED)
            self.assertFalse(updated.payload_path.exists())
            self.assertEqual(len(outbox.list_active()), 0)

    def test_cleanup_completed_payloads_removes_only_completed_payloads(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            outbox = DurableBatchOutbox(root_dir=Path(temp), parser_instance_id="parser-a")
            completed = outbox.enqueue_batch(
                external_batch_id="batch-completed",
                source_category="Товары для дома",
                source_subcategory="Коврики для ванной",
                proxy_key="local",
                batch_kind="complete_card_batch",
                payload={"rows": [1]},
            )
            pending = outbox.enqueue_batch(
                external_batch_id="batch-pending",
                source_category="Товары для дома",
                source_subcategory="Коврики для ванной",
                proxy_key="local",
                batch_kind="complete_card_batch",
                payload={"rows": [2]},
            )
            outbox.record_poll_result(completed.local_batch_id, server_status="completed")
            completed.payload_path.write_text("{}", encoding="utf-8")

            cleaned = outbox.cleanup_completed_payloads()

            self.assertEqual(cleaned, 1)
            self.assertFalse(completed.payload_path.exists())
            self.assertTrue(pending.payload_path.exists())
            self.assertEqual(outbox.count_by_status()[OutboxStatus.COMPLETED.value], 1)

    def test_proxy_run_lifecycle_uses_server_queue_base_url(self) -> None:
        calls: list[tuple[str, str, dict[str, object]]] = []

        def post_json(url: str, payload: dict[str, object]) -> SimpleNamespace:
            calls.append(("POST", url, payload))
            return SimpleNamespace(status_code=202, json=lambda: {"status": "running"})

        def patch_json(url: str, payload: dict[str, object]) -> SimpleNamespace:
            calls.append(("PATCH", url, payload))
            return SimpleNamespace(status_code=200, json=lambda: {"status": "running"})

        with tempfile.TemporaryDirectory() as temp:
            outbox = DurableBatchOutbox(
                root_dir=Path(temp),
                parser_instance_id="parser-a",
                server_base_url="http://api/api/v1/parser",
                post_json=post_json,
                patch_json=patch_json,
            )

            outbox.start_proxy_run(
                external_proxy_run_id="market_refresh:proxy-1:Товары:Платья и сарафаны",
                proxy_key="proxy-1",
                source_category="category",
                source_subcategory="niche",
                planned_products_count=100,
                downloaded_products_count=0,
            )
            outbox.update_proxy_run_progress(
                external_proxy_run_id="market_refresh:proxy-1:Товары:Платья и сарафаны",
                planned_products_count=100,
                downloaded_products_count=10,
            )
            outbox.finish_proxy_run(
                external_proxy_run_id="market_refresh:proxy-1:Товары:Платья и сарафаны",
                status="completed",
                planned_products_count=100,
                downloaded_products_count=10,
            )

        self.assertEqual([call[0] for call in calls], ["POST", "PATCH", "POST"])
        self.assertTrue(calls[0][1].endswith("/proxy-runs/start"))
        self.assertIn(
            "/proxy-runs/market_refresh%3Aproxy-1%3A%D0%A2%D0%BE%D0%B2%D0%B0%D1%80%D1%8B%3A%D0%9F%D0%BB%D0%B0%D1%82%D1%8C%D1%8F%20%D0%B8%20%D1%81%D0%B0%D1%80%D0%B0%D1%84%D0%B0%D0%BD%D1%8B/progress",
            calls[1][1],
        )
        self.assertIn(
            "/proxy-runs/market_refresh%3Aproxy-1%3A%D0%A2%D0%BE%D0%B2%D0%B0%D1%80%D1%8B%3A%D0%9F%D0%BB%D0%B0%D1%82%D1%8C%D1%8F%20%D0%B8%20%D1%81%D0%B0%D1%80%D0%B0%D1%84%D0%B0%D0%BD%D1%8B/finish",
            calls[2][1],
        )
        self.assertEqual(calls[0][2]["parserInstanceId"], "parser-a")


if __name__ == "__main__":
    unittest.main()
