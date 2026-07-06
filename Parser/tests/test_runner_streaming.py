from __future__ import annotations

import sys
from pathlib import Path


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from config import ParserConfig  # noqa: E402
from dto import DataPage  # noqa: E402
from app.proxy_mapping import NicheProxyAssignment, ProxyDefinition, ProxyMapping  # noqa: E402
from pipelines.products import runner  # noqa: E402


class _Product:
    def __init__(self, product_id: int) -> None:
        self.id = product_id


class _Items:
    def __init__(self, products: list[_Product]) -> None:
        self.products = products


def _patch_common_streaming(monkeypatch, fetcher_cls, *, categories: list[dict] | None = None) -> None:
    monkeypatch.setattr(runner, "_selected_subcategories", lambda config: categories or [{"name": "Test niche"}])
    monkeypatch.setattr(runner, "_acquire_cookies", lambda *args, **kwargs: None)
    monkeypatch.setattr(runner, "_network_smoke_check", lambda config, manifest, selected: {"status": "ok"})
    monkeypatch.setattr(runner, "WbCatalogFetcher", fetcher_cls)
    monkeypatch.setattr(
        runner.Items,
        "model_validate",
        staticmethod(lambda raw: _Items([_Product(item["id"]) for item in raw["products"]])),
    )
    monkeypatch.setattr(runner, "add_images", lambda products: products)
    monkeypatch.setattr(runner, "add_price_with_wb_wallet", lambda products: products)

    def canonical_row(**kwargs):
        product = kwargs["item"]
        return {
            "marketplace": kwargs["marketplace"],
            "parser_run_id": kwargs["parser_run_id"],
            "wb_product_id": str(product.id),
            "wb_root_id": str(product.id + 1000),
            "source_category": kwargs["source_category"],
            "source_subcategory": kwargs["source_subcategory"],
        }

    monkeypatch.setattr(runner, "product_to_canonical_row", canonical_row)


def _streaming_config(tmp_path: Path, *, product_fetch_mode: str = "direct", acquire_token: bool = False) -> ParserConfig:
    return ParserConfig(
        output_base_dir=tmp_path,
        parent_category="Root",
        subcategory_allowlist=["Test niche"],
        include_xlsx=False,
        include_wb_wallet_prices=False,
        product_fetch_mode=product_fetch_mode,
        acquire_token=acquire_token,
        batch_delay_min_seconds=0.0,
        batch_delay_max_seconds=0.0,
    )


def _proxy_mapping_for_test() -> ProxyMapping:
    proxy = ProxyDefinition(
        key="proxy-1",
        type="http-proxy",
        base_url="http://10.0.0.1:19118",
        credentials={"username": "login", "password": "secret-password"},
    )
    return ProxyMapping(
        default_proxy=ProxyDefinition(key="direct", type="direct"),
        proxies={"proxy-1": proxy},
        assignments=[
            NicheProxyAssignment(
                source_category="Root",
                source_subcategory="Test niche",
                proxy_key="proxy-1",
            )
        ],
    )


def test_product_run_id_uses_proxy_scope_to_avoid_parallel_child_collisions(monkeypatch) -> None:
    monkeypatch.setenv("PARSER_ONLY_PROXY", "proxy-2")

    run_id = runner._make_run_id()

    assert "_proxy_2_" in run_id


def test_explicit_niche_resolution_preserves_configured_subcategory(monkeypatch) -> None:
    selected = [
        {
            "id": 8137,
            "name": "Configured leaf",
            "searchQuery": "menu_v3_8137 dress",
            "sourceCategory": "Root category",
            "sourcePath": "Root category - Configured leaf",
        }
    ]

    class _CategoriesParser:
        def parse(self):
            return [
                {
                    "id": 8137,
                    "name": "WB leaf",
                    "searchQuery": "menu_v3_8137 dress",
                    "sourceCategory": "Root category",
                    "sourceSubcategory": "WB leaf",
                    "sourcePath": "Root category - Parent - WB leaf",
                }
            ]

    monkeypatch.setattr(runner, "CategoriesParser", _CategoriesParser)

    resolved = runner._resolve_explicit_niches_against_wb_menu(selected)

    assert resolved[0]["name"] == "Configured leaf"
    assert resolved[0]["sourceSubcategory"] == "Configured leaf"
    assert resolved[0]["sourcePath"] == "Root category - Parent - WB leaf"


def test_price_split_uses_parser_search_text_instead_of_wb_menu_query(tmp_path, monkeypatch) -> None:
    class _SearchPhraseParser:
        search_phrases: list[str] = []

        def __init__(self, *, search_phrase: str, **_: object) -> None:
            self.search_phrases.append(search_phrase)

        def parse(self) -> list[DataPage]:
            return [DataPage(min_price=10000, max_price=20000, total=100)]

    categories = [
        {
            "name": "Платья и сарафаны",
            "sourceCategory": "Женщинам",
            "sourceSubcategory": "Платья и сарафаны",
            "sourcePath": "Женщинам / Платья и сарафаны",
            "searchQuery": "menu_v3_8137 платье женские",
            "parserSearchText": "Платья и сарафаны",
        }
    ]
    _patch_common_streaming(monkeypatch, _Fetcher, categories=categories)
    monkeypatch.setattr(runner, "SearchPhraseParser", _SearchPhraseParser)

    run_dir = runner.run_parser_streaming(
        _streaming_config(tmp_path, product_fetch_mode="price_split"),
        streaming_batch_size=100,
        batch_handler=lambda batch: runner.ProductDiscoveryBatchResult(status="staged"),
    )

    assert _SearchPhraseParser.search_phrases == ["Платья и сарафаны"]
    assert (run_dir / "products.csv").exists()


def test_transport_preflight_acquires_browser_session_then_checks_filters_endpoint(tmp_path, monkeypatch) -> None:
    _patch_common_streaming(monkeypatch, _Fetcher)
    monkeypatch.setattr(runner, "_load_proxy_mapping", lambda: _proxy_mapping_for_test())

    calls: list[str] = []

    def acquire_cookies(*args, **kwargs):
        calls.append("token")
        return {"x_wbaas_token": "token-1", "session": "cookie-1"}

    class _SearchPhraseParser:
        def __init__(self, **kwargs: object) -> None:
            self.search_phrase = kwargs["search_phrase"]
            assert kwargs["cookies"]["x_wbaas_token"] == "token-1"

        def fetch_data(self) -> dict[str, object]:
            calls.append(f"filters_preflight:{self.search_phrase}")
            return {"metadata": {"name": self.search_phrase}, "data": {"total": 100}}

        def parse(self) -> list[DataPage]:
            calls.append("filters_parse")
            return [DataPage(min_price=10000, max_price=20000, total=100)]

    class _TransportPreflight:
        is_success = True
        is_transport_success = True
        connect_status = 200
        http_status = 0
        tls_established = True

        @staticmethod
        def diagnostic_message() -> str:
            return "WB transport preflight ok: CONNECT=200 TLS=ok"

    monkeypatch.setattr(runner, "_acquire_cookies", acquire_cookies)
    monkeypatch.setattr(runner, "SearchPhraseParser", _SearchPhraseParser)
    monkeypatch.setattr(runner, "run_wb_proxy_preflight", lambda proxy: _TransportPreflight())

    lifecycle_events: list[dict[str, object]] = []
    batches: list[runner.ProductDiscoveryBatch] = []

    runner.run_parser_streaming(
        _streaming_config(tmp_path, product_fetch_mode="price_split", acquire_token=True),
        streaming_batch_size=100,
        batch_handler=lambda batch: batches.append(batch) or runner.ProductDiscoveryBatchResult(status="staged"),
        category_lifecycle_handler=lifecycle_events.append,
    )

    assert calls == ["token", "filters_preflight:Test niche", "filters_parse"]
    assert [len(batch.rows) for batch in batches] == [100, 100, 50]
    assert any(
        event.get("event") == "start" and event.get("phase") == "wb_preflight"
        for event in lifecycle_events
    )
    assert any(
        event.get("event") == "progress" and event.get("phase") == "ranges"
        for event in lifecycle_events
    )


def test_filters_preflight_failure_stops_before_full_price_split(tmp_path, monkeypatch) -> None:
    _patch_common_streaming(monkeypatch, _Fetcher)
    monkeypatch.setattr(runner, "_load_proxy_mapping", lambda: _proxy_mapping_for_test())

    calls: list[str] = []

    class _TransportPreflight:
        is_transport_success = True
        connect_status = 200
        http_status = 0
        tls_established = True

        @staticmethod
        def diagnostic_message() -> str:
            return "WB transport preflight ok: CONNECT=200 TLS=ok"

    class _SearchPhraseParser:
        def __init__(self, **kwargs: object) -> None:
            self.final_error = "WB filters HTTP status 498"
            self.aborted_by_rate_limit = False

        def fetch_data(self) -> None:
            calls.append("filters_preflight")
            return None

        def parse(self) -> list[DataPage]:
            calls.append("filters_parse")
            raise AssertionError("Price split must not run when authenticated WB preflight fails")

    monkeypatch.setattr(runner, "_acquire_cookies", lambda *args, **kwargs: {"x_wbaas_token": "token-1"})
    monkeypatch.setattr(runner, "SearchPhraseParser", _SearchPhraseParser)
    monkeypatch.setattr(runner, "run_wb_proxy_preflight", lambda proxy: _TransportPreflight())

    lifecycle_events: list[dict[str, object]] = []
    batches: list[runner.ProductDiscoveryBatch] = []

    runner.run_parser_streaming(
        _streaming_config(tmp_path, product_fetch_mode="price_split", acquire_token=True),
        streaming_batch_size=100,
        batch_handler=lambda batch: batches.append(batch) or runner.ProductDiscoveryBatchResult(status="staged"),
        category_lifecycle_handler=lifecycle_events.append,
    )

    assert calls == ["filters_preflight"]
    assert batches == []
    assert any(
        event.get("event") == "finish"
        and event.get("status") == "failed"
        and "WB filters preflight failed" in str(event.get("error"))
        and "search.wb.ru" in str(event.get("error"))
        and "www" not in str(event.get("error"))
        for event in lifecycle_events
    )


class _Fetcher:
    events: list[str] = []

    def __init__(self, **_: object) -> None:
        pass

    async def fetch_all(self) -> list[dict[str, object]]:
        raise AssertionError("streaming parser must consume iter_result_batches(), not fetch_all()")

    async def iter_result_batches(self):
        self.events.append("yield:first")
        yield [{"products": [{"id": index} for index in range(1, 101)]}]
        self.events.append("after:first")
        self.events.append("yield:second")
        yield [{"products": [{"id": index} for index in range(101, 251)]}]
        self.events.append("after:second")


def test_run_parser_streaming_flushes_batches_before_full_discovery_finishes(tmp_path, monkeypatch) -> None:
    _Fetcher.events = []
    _patch_common_streaming(monkeypatch, _Fetcher)

    batch_sizes: list[int] = []
    product_lines_during_batches: list[int] = []

    def handle_batch(batch: runner.ProductDiscoveryBatch) -> runner.ProductDiscoveryBatchResult:
        _Fetcher.events.append(f"handler:{len(batch.rows)}")
        batch_sizes.append(len(batch.rows))
        product_lines_during_batches.append(
            len((batch.parent_run_dir / "products.jsonl").read_text(encoding="utf-8").splitlines())
        )
        return runner.ProductDiscoveryBatchResult(status="staged")

    run_dir = runner.run_parser_streaming(
        _streaming_config(tmp_path),
        streaming_batch_size=100,
        batch_handler=handle_batch,
    )

    assert batch_sizes == [100, 100, 50]
    assert product_lines_during_batches == [100, 200, 250]
    assert _Fetcher.events.index("handler:100") < _Fetcher.events.index("after:first")
    assert (run_dir / "products.csv").exists()


class _SmallFetcher:
    calls = 0

    def __init__(self, **_: object) -> None:
        pass

    async def fetch_all(self) -> list[dict[str, object]]:
        raise AssertionError("streaming parser must consume iter_result_batches(), not fetch_all()")

    async def iter_result_batches(self):
        type(self).calls += 1
        offset = (type(self).calls - 1) * 1000
        yield [{"products": [{"id": offset + index} for index in range(1, 61)]}]


def test_run_parser_streaming_flushes_batch_at_category_boundary(tmp_path, monkeypatch) -> None:
    _SmallFetcher.calls = 0
    categories = [{"name": "Niche A"}, {"name": "Niche B"}]
    _patch_common_streaming(monkeypatch, _SmallFetcher, categories=categories)
    monkeypatch.setattr(runner, "_category_source_category", lambda config, selected: f"Root {selected['name'][-1]}")

    batch_scopes: list[tuple[int, set[str]]] = []

    def handle_batch(batch: runner.ProductDiscoveryBatch) -> runner.ProductDiscoveryBatchResult:
        batch_scopes.append((len(batch.rows), {row["source_subcategory"] for row in batch.rows}))
        return runner.ProductDiscoveryBatchResult(status="staged")

    runner.run_parser_streaming(
        _streaming_config(tmp_path),
        streaming_batch_size=100,
        batch_handler=handle_batch,
    )

    assert batch_scopes == [(60, {"Niche A"}), (60, {"Niche B"})]


class _FullSplitParser:
    def __init__(self, **kwargs: object) -> None:
        self.split_progress_recorder = kwargs.get("split_progress_recorder")

    def parse(self):
        if self.split_progress_recorder:
            self.split_progress_recorder(
                processed_ranges=0,
                pending_ranges=2,
                progress_percent=0.0,
                range_checks_count=0,
                final_ranges_count=0,
                empty_ranges_count=0,
                split_ranges_count=0,
            )
            self.split_progress_recorder(
                processed_ranges=2,
                pending_ranges=0,
                progress_percent=99.0,
                range_checks_count=2,
                final_ranges_count=2,
                empty_ranges_count=0,
                split_ranges_count=0,
            )
        return [DataPage(100, 900, 50), DataPage(901, 1900, 1000)]


class _RangeCarryFetcher:
    stop_requested = False

    def __init__(self, *, pages: list[DataPage], **_: object) -> None:
        self.pages = pages

    async def fetch_all(self) -> list[dict[str, object]]:
        raise AssertionError("streaming parser must consume iter_result_batches(), not fetch_all()")

    async def iter_result_batches(self):
        assert [page.total for page in self.pages] == [50, 1000]
        yield [{"products": [{"id": index} for index in range(1, 51)]}]
        yield [{"products": [{"id": index} for index in range(1001, 1101)]}]


class _StopRequestedAfterEnoughBatchesFetcher:
    stop_requested = True

    def __init__(self, **_: object) -> None:
        pass

    async def fetch_all(self) -> list[dict[str, object]]:
        raise AssertionError("streaming parser must consume iter_result_batches(), not fetch_all()")

    async def iter_result_batches(self):
        yield [{"products": [{"id": index} for index in range(1, 101)]}]
        yield [{"products": [{"id": index} for index in range(101, 201)]}]
        yield [{"products": [{"id": index} for index in range(201, 301)]}]


def test_run_parser_streaming_full_split_carries_batch_across_price_ranges(tmp_path, monkeypatch) -> None:
    _patch_common_streaming(monkeypatch, _RangeCarryFetcher)
    monkeypatch.setattr(runner, "SearchPhraseParser", _FullSplitParser)

    batches: list[list[str]] = []

    def handle_batch(batch: runner.ProductDiscoveryBatch) -> runner.ProductDiscoveryBatchResult:
        batches.append([row["wb_product_id"] for row in batch.rows])
        return runner.ProductDiscoveryBatchResult(status="staged")

    runner.run_parser_streaming(
        _streaming_config(tmp_path, product_fetch_mode="price_split"),
        streaming_batch_size=100,
        batch_handler=handle_batch,
    )

    assert len(batches) == 2
    assert batches[0][:3] == ["1", "2", "3"]
    assert batches[0][-3:] == ["1048", "1049", "1050"]
    assert batches[1][0] == "1051"
    assert batches[1][-1] == "1100"


def test_run_parser_streaming_does_not_fail_on_catalog_stop_after_batch_limit(tmp_path, monkeypatch) -> None:
    _patch_common_streaming(monkeypatch, _StopRequestedAfterEnoughBatchesFetcher)
    monkeypatch.setenv("PARSER_MAX_STREAM_BATCHES", "3")

    batches: list[runner.ProductDiscoveryBatch] = []

    def handle_batch(batch: runner.ProductDiscoveryBatch) -> runner.ProductDiscoveryBatchResult:
        batches.append(batch)
        status = "stopped" if len(batches) >= 3 else "staged"
        return runner.ProductDiscoveryBatchResult(status=status)

    run_dir = runner.run_parser_streaming(
        _streaming_config(tmp_path),
        streaming_batch_size=100,
        batch_handler=handle_batch,
    )

    manifest = (run_dir / "manifest.json").read_text(encoding="utf-8")
    assert [len(batch.rows) for batch in batches] == [100, 100, 100]
    assert "WB catalog rate limit while fetching price ranges" not in manifest
    assert '"status": "succeeded"' in manifest


def test_run_parser_streaming_reports_full_split_plan_before_first_batch(tmp_path, monkeypatch) -> None:
    _patch_common_streaming(monkeypatch, _RangeCarryFetcher)
    monkeypatch.setattr(runner, "SearchPhraseParser", _FullSplitParser)
    monkeypatch.setenv("PARSER_MAX_STREAM_BATCHES", "3")

    lifecycle_events: list[dict] = []

    runner.run_parser_streaming(
        _streaming_config(tmp_path, product_fetch_mode="price_split"),
        streaming_batch_size=100,
        batch_handler=lambda batch: runner.ProductDiscoveryBatchResult(status="stopped"),
        category_lifecycle_handler=lambda event: lifecycle_events.append(dict(event)),
    )

    download_events = [
        event for event in lifecycle_events
        if event.get("event") == "progress" and event.get("phase") == "download"
    ]
    assert download_events
    assert download_events[-1]["planned_products_count"] == 300
    assert download_events[-1]["range_progress_percent"] == 100.0


def test_run_parser_streaming_waits_between_full_split_and_catalog_fetch(tmp_path, monkeypatch) -> None:
    events: list[str] = []

    class _Parser(_FullSplitParser):
        def parse(self):
            events.append("full_split")
            return super().parse()

    class _Fetcher(_RangeCarryFetcher):
        def __init__(self, **kwargs: object) -> None:
            events.append("catalog_fetcher_created")
            super().__init__(**kwargs)

    _patch_common_streaming(monkeypatch, _Fetcher)
    monkeypatch.setattr(runner, "SearchPhraseParser", _Parser)
    monkeypatch.setenv("PARSER_PRICE_SPLIT_TO_CATALOG_DELAY_SECONDS", "7")
    monkeypatch.setenv("PARSER_PRICE_SPLIT_TO_CATALOG_JITTER_SECONDS", "0")
    monkeypatch.setattr(runner, "time", type("_Time", (), {"sleep": staticmethod(lambda seconds: events.append(f"sleep:{seconds:g}"))}))

    runner.run_parser_streaming(
        _streaming_config(tmp_path, product_fetch_mode="price_split"),
        streaming_batch_size=100,
        batch_handler=lambda batch: runner.ProductDiscoveryBatchResult(status="stopped"),
    )

    assert events.index("full_split") < events.index("sleep:7")
    assert events.index("sleep:7") < events.index("catalog_fetcher_created")


def test_run_parser_streaming_ignores_removed_price_split_queue_flag(tmp_path, monkeypatch) -> None:
    _patch_common_streaming(monkeypatch, _RangeCarryFetcher)
    monkeypatch.setattr(runner, "SearchPhraseParser", _FullSplitParser)
    monkeypatch.setenv("PARSER_PRICE_SPLIT_QUEUE_ENABLED", "true")
    monkeypatch.setenv("PARSER_BATCH_QUEUE_URL", "http://api.local/api/v1/parser")

    batches: list[runner.ProductDiscoveryBatch] = []

    runner.run_parser_streaming(
        _streaming_config(tmp_path, product_fetch_mode="price_split"),
        streaming_batch_size=100,
        batch_handler=lambda batch: batches.append(batch) or runner.ProductDiscoveryBatchResult(status="stopped"),
    )

    assert batches
    assert not hasattr(runner, "PriceSplitQueueClient")
