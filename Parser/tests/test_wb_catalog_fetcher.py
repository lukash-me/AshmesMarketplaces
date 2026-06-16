from __future__ import annotations

import sys
from pathlib import Path


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from WbCatalogFetcher import WbCatalogFetcher  # noqa: E402


def test_iter_result_batches_yields_after_each_internal_request_batch(monkeypatch) -> None:
    fetcher = WbCatalogFetcher(
        pages=[],
        search_phrase="test",
        cookies={},
        price_split_enabled=False,
        max_catalog_pages=3,
        batch_size=2,
        batch_delay_bounds=(0.0, 0.0),
        request_delay_bounds=(0.0, 0.0),
    )
    events: list[str] = []

    async def fake_fetch_one(client, task):
        events.append(f"fetch:{task['page']}")
        return {"products": [{"id": task["page"]}]}

    monkeypatch.setattr(fetcher, "_fetch_one", fake_fetch_one)

    async def collect() -> list[list[dict]]:
        batches: list[list[dict]] = []
        async for result_batch in fetcher.iter_result_batches():
            events.append(f"yield:{len(result_batch)}")
            batches.append(result_batch)
        return batches

    import asyncio

    batches = asyncio.run(collect())

    assert [[row["products"][0]["id"] for row in batch] for batch in batches] == [[1, 2], [3]]
    assert events == ["fetch:1", "fetch:2", "yield:2", "fetch:3", "yield:1"]


def test_fetch_all_preserves_flattened_results_through_streaming_generator(monkeypatch) -> None:
    fetcher = WbCatalogFetcher(
        pages=[],
        search_phrase="test",
        cookies={},
        price_split_enabled=False,
        max_catalog_pages=3,
        batch_size=2,
        batch_delay_bounds=(0.0, 0.0),
        request_delay_bounds=(0.0, 0.0),
    )

    async def fake_fetch_one(client, task):
        return {"products": [{"id": task["page"]}]}

    monkeypatch.setattr(fetcher, "_fetch_one", fake_fetch_one)

    import asyncio

    results = asyncio.run(fetcher.fetch_all())

    assert [row["products"][0]["id"] for row in results] == [1, 2, 3]
