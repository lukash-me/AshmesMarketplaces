from __future__ import annotations

import sys
from pathlib import Path


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

import runner  # noqa: E402
from config import ParserConfig  # noqa: E402


class _Product:
    def __init__(self, product_id: int) -> None:
        self.id = product_id


class _Items:
    def __init__(self, products: list[_Product]) -> None:
        self.products = products


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
    monkeypatch.setattr(runner, "_selected_subcategories", lambda config: [{"name": "Test niche"}])
    monkeypatch.setattr(runner, "_acquire_token", lambda config, manifest: None)
    monkeypatch.setattr(runner, "_network_smoke_check", lambda config, manifest, selected, cookies: {"status": "ok"})
    monkeypatch.setattr(runner, "WbCatalogFetcher", _Fetcher)
    monkeypatch.setattr(runner.Items, "model_validate", staticmethod(lambda raw: _Items([_Product(item["id"]) for item in raw["products"]])))
    monkeypatch.setattr(runner, "add_images", lambda products: products)
    monkeypatch.setattr(runner, "add_price_with_wb_wallet", lambda products: products)

    def canonical_row(**kwargs):
        product = kwargs["item"]
        return {
            "marketplace": kwargs["marketplace"],
            "parser_run_id": kwargs["parser_run_id"],
            "wb_product_id": str(product.id),
            "wb_root_id": str(product.id + 1000),
            "source_subcategory": kwargs["source_subcategory"],
        }

    monkeypatch.setattr(runner, "product_to_canonical_row", canonical_row)

    config = ParserConfig(
        output_base_dir=tmp_path,
        parent_category="Root",
        subcategory_allowlist=["Test niche"],
        include_xlsx=False,
        include_wb_wallet_prices=False,
        product_fetch_mode="direct",
        batch_delay_min_seconds=0.0,
        batch_delay_max_seconds=0.0,
    )
    batch_sizes: list[int] = []
    csv_existed_during_batches: list[bool] = []
    product_lines_during_batches: list[int] = []

    def handle_batch(batch: runner.ProductDiscoveryBatch) -> runner.ProductDiscoveryBatchResult:
        _Fetcher.events.append(f"handler:{len(batch.rows)}")
        batch_sizes.append(len(batch.rows))
        csv_existed_during_batches.append((batch.parent_run_dir / "products.csv").exists())
        product_lines_during_batches.append(
            len((batch.parent_run_dir / "products.jsonl").read_text(encoding="utf-8").splitlines())
        )
        return runner.ProductDiscoveryBatchResult(status="staged")

    run_dir = runner.run_parser_streaming(
        config,
        streaming_batch_size=100,
        batch_handler=handle_batch,
    )

    assert batch_sizes == [100, 100, 50]
    assert csv_existed_during_batches == [False, False, False]
    assert product_lines_during_batches == [100, 200, 250]
    assert _Fetcher.events.index("handler:100") < _Fetcher.events.index("after:first")
    assert (run_dir / "products.csv").exists()
