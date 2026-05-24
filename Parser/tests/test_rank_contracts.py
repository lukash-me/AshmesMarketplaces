from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

import rank_runner  # noqa: E402
from rank_config import (  # noqa: E402
    KNOWN_RANK_CONTEXT_TYPES,
    RankContextConfig,
    RankParserConfig,
)
from rank_contracts import (  # noqa: E402
    build_rank_rows,
    build_search_params,
    request_fingerprint,
)
from wb_rank_fetcher import RankPageFetchResult  # noqa: E402


class RankContractTests(unittest.TestCase):
    def test_home_goods_search_rank_preset_loads_three_contexts(self) -> None:
        config = RankParserConfig.load(PARSER_DIR / "presets" / "home_goods_search_rank_demo.json")

        self.assertEqual(config.top_n, 1000)
        self.assertEqual([context.id for context in config.contexts], [
            "home_storage_organizers",
            "home_bath_mats",
            "home_wall_sconces",
        ])
        self.assertEqual({context.type for context in config.contexts}, {"search_query"})

    def test_home_goods_search_rank_preset_queries_match_subcategories(self) -> None:
        config = RankParserConfig.load(PARSER_DIR / "presets" / "home_goods_search_rank_demo.json")

        expected = {
            "home_storage_organizers": "Органайзеры для хранения вещей",
            "home_bath_mats": "Коврики для ванной",
            "home_wall_sconces": "Светильники бра",
        }

        for context in config.contexts:
            with self.subTest(context=context.id):
                self.assertEqual(context.query.strip(), context.source_subcategory.strip())
                self.assertEqual(context.query, expected[context.id])

        wall_sconces = next(context for context in config.contexts if context.id == "home_wall_sconces")
        self.assertNotEqual(wall_sconces.query, "бра настенное")

    def test_home_goods_search_rank_preset_sends_subcategory_as_search_query(self) -> None:
        config = RankParserConfig.load(PARSER_DIR / "presets" / "home_goods_search_rank_demo.json")

        for context in config.contexts:
            with self.subTest(context=context.id):
                params = build_search_params(
                    context=context,
                    page=1,
                    dest=config.dest_for(context),
                )

                self.assertEqual(params["query"], context.source_subcategory)
                self.assertEqual(params["query"], context.query)

    def test_builds_ordered_rows_with_absolute_positions(self) -> None:
        context = RankContextConfig(
            id="storage",
            type="search_query",
            query="Органайзеры для хранения вещей",
            source_category="Товары для дома",
            source_subcategory="Органайзеры для хранения вещей",
        )

        rows = build_rank_rows(
            products=[{"id": 101, "root": 1001}, {"id": 102, "root": 1002}],
            parser_run_id="wb_rank_test",
            observed_at_utc="2026-05-23T00:00:00Z",
            marketplace="wildberries",
            context=context,
            source_region_dest="123",
            request_fingerprint_value="fingerprint",
            page=2,
            page_size=100,
            response_total_value=1000,
            remaining_slots=10,
        )

        self.assertEqual([row["wb_product_id"] for row in rows], ["101", "102"])
        self.assertEqual([row["position_on_page"] for row in rows], [1, 2])
        self.assertEqual([row["absolute_position"] for row in rows], [101, 102])

    def test_top_n_cutoff_does_not_emit_extra_rows(self) -> None:
        context = RankContextConfig(id="bath", type="search_query", query="Коврики для ванной")

        rows = build_rank_rows(
            products=[{"id": 1}, {"id": 2}, {"id": 3}],
            parser_run_id="wb_rank_test",
            observed_at_utc="2026-05-23T00:00:00Z",
            marketplace="wildberries",
            context=context,
            source_region_dest="123",
            request_fingerprint_value="fingerprint",
            page=1,
            page_size=100,
            response_total_value=None,
            remaining_slots=2,
        )

        self.assertEqual([row["wb_product_id"] for row in rows], ["1", "2"])

    def test_search_params_have_no_price_split(self) -> None:
        context = RankContextConfig(
            id="lighting",
            type="search_query",
            query="Светильники бра",
            sort="popular",
            filters={"xsubject": 130194},
        )

        params = build_search_params(context=context, page=1, dest="12354108")

        self.assertEqual(params["query"], "Светильники бра")
        self.assertEqual(params["resultset"], "catalog")
        self.assertEqual(params["sort"], "popular")
        self.assertNotIn("priceU", params)

    def test_request_fingerprint_is_stable(self) -> None:
        first = request_fingerprint(endpoint="endpoint", params={"b": "2", "a": "1"})
        second = request_fingerprint(endpoint="endpoint", params={"a": "1", "b": "2"})

        self.assertEqual(first, second)

    def test_category_result_context_is_known_but_not_supported_in_v1_runner(self) -> None:
        self.assertIn("category_result", KNOWN_RANK_CONTEXT_TYPES)

        with tempfile.TemporaryDirectory() as temp_dir:
            config = RankParserConfig(
                output_base_dir=Path(temp_dir),
                acquire_token=False,
                request_delay_min_seconds=0,
                request_delay_max_seconds=0,
                contexts=[
                    RankContextConfig(
                        id="future_category",
                        type="category_result",
                        query="menu_v3_261 коврики в ванную",
                    )
                ],
            )
            run_dir = rank_runner.run_rank_parser(config)
            manifest = json.loads((run_dir / "manifest.json").read_text(encoding="utf-8"))

        self.assertEqual(manifest["status"], "failed")
        self.assertEqual(manifest["context_results"][0]["status"], "failed")
        self.assertFalse((run_dir / "product_rank_snapshots.jsonl").exists())


class FakeRankFetcher:
    pages: dict[int, RankPageFetchResult] = {}

    def __init__(self, **_: object) -> None:
        pass

    def fetch_page(self, *, context: RankContextConfig, page: int, dest: str) -> RankPageFetchResult:
        return self.pages[page]


class RankRunnerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.original_fetcher = rank_runner.WbRankFetcher
        self.original_make_run_id = rank_runner._make_run_id
        rank_runner.WbRankFetcher = FakeRankFetcher  # type: ignore[assignment]
        rank_runner._make_run_id = lambda: "wb_rank_test"  # type: ignore[assignment]

    def tearDown(self) -> None:
        rank_runner.WbRankFetcher = self.original_fetcher  # type: ignore[assignment]
        rank_runner._make_run_id = self.original_make_run_id  # type: ignore[assignment]

    def test_runner_writes_rank_rows_page_audit_and_duplicate_count(self) -> None:
        context = RankContextConfig(
            id="storage",
            type="search_query",
            query="Органайзеры для хранения вещей",
            source_category="Товары для дома",
            source_subcategory="Органайзеры для хранения вещей",
        )
        FakeRankFetcher.pages = {
            1: RankPageFetchResult(
                status="succeeded",
                params={"page": "1"},
                request_fingerprint="page-1",
                page=1,
                products=[{"id": 1}, {"id": 2}],
                response_total=4,
            ),
            2: RankPageFetchResult(
                status="succeeded",
                params={"page": "2"},
                request_fingerprint="page-2",
                page=2,
                products=[{"id": 2}, {"id": 3}],
                response_total=4,
            ),
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            config = RankParserConfig(
                output_base_dir=Path(temp_dir),
                acquire_token=False,
                page_size=2,
                top_n=4,
                request_delay_min_seconds=0,
                request_delay_max_seconds=0,
                contexts=[context],
            )
            run_dir = rank_runner.run_rank_parser(config)
            rows = [
                json.loads(line)
                for line in (run_dir / "product_rank_snapshots.jsonl").read_text(encoding="utf-8").splitlines()
            ]
            page_events = [
                json.loads(line)
                for line in (run_dir / "rank_page_fetches.jsonl").read_text(encoding="utf-8").splitlines()
            ]
            manifest = json.loads((run_dir / "manifest.json").read_text(encoding="utf-8"))

        self.assertEqual([row["absolute_position"] for row in rows], [1, 2, 3, 4])
        self.assertEqual(len(page_events), 2)
        self.assertEqual({row["query"] for row in rows}, {"Органайзеры для хранения вещей"})
        self.assertEqual({row["source_category"] for row in rows}, {"Товары для дома"})
        self.assertEqual({row["source_subcategory"] for row in rows}, {"Органайзеры для хранения вещей"})
        self.assertEqual({event["query"] for event in page_events}, {"Органайзеры для хранения вещей"})
        self.assertEqual({event["source_category"] for event in page_events}, {"Товары для дома"})
        self.assertEqual({event["source_subcategory"] for event in page_events}, {"Органайзеры для хранения вещей"})
        self.assertEqual(manifest["requested_scope"]["contexts"][0]["query"], "Органайзеры для хранения вещей")
        self.assertEqual(manifest["requested_scope"]["contexts"][0]["source_category"], "Товары для дома")
        self.assertEqual(
            manifest["requested_scope"]["contexts"][0]["source_subcategory"],
            "Органайзеры для хранения вещей",
        )
        self.assertEqual(manifest["context_results"][0]["query"], "Органайзеры для хранения вещей")
        self.assertEqual(manifest["context_results"][0]["source_category"], "Товары для дома")
        self.assertEqual(manifest["context_results"][0]["source_subcategory"], "Органайзеры для хранения вещей")
        self.assertEqual(manifest["status"], "succeeded")
        self.assertEqual(manifest["row_counts"]["rank_rows_written"], 4)
        self.assertEqual(manifest["row_counts"]["duplicate_product_ids_within_context"], 1)

    def test_failed_page_writes_gap_event_without_fake_rank_rows(self) -> None:
        context = RankContextConfig(id="bath", type="search_query", query="коврик")
        FakeRankFetcher.pages = {
            1: RankPageFetchResult(
                status="failed",
                params={"page": "1"},
                request_fingerprint="page-1",
                page=1,
                products=[],
                http_status=500,
                message="failed",
            )
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            config = RankParserConfig(
                output_base_dir=Path(temp_dir),
                acquire_token=False,
                page_size=100,
                top_n=100,
                request_delay_min_seconds=0,
                request_delay_max_seconds=0,
                contexts=[context],
            )
            run_dir = rank_runner.run_rank_parser(config)
            page_events = [
                json.loads(line)
                for line in (run_dir / "rank_page_fetches.jsonl").read_text(encoding="utf-8").splitlines()
            ]
            manifest = json.loads((run_dir / "manifest.json").read_text(encoding="utf-8"))

        self.assertFalse((run_dir / "product_rank_snapshots.jsonl").exists())
        self.assertEqual(page_events[0]["status"], "failed")
        self.assertEqual(manifest["status"], "failed")
        self.assertEqual(manifest["page_counts"]["failed"], 1)


if __name__ == "__main__":
    unittest.main()
