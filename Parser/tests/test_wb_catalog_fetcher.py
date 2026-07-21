from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from WbCatalogFetcher import WbCatalogFetcher  # noqa: E402
from dto import DataPage  # noqa: E402


class NoopAsyncLimiter:
    def __init__(self) -> None:
        self.waited: list[str] = []
        self.deferred: list[tuple[str, float]] = []

    async def wait(self, proxy_key: str) -> None:
        self.waited.append(proxy_key)

    def defer(self, proxy_key: str, seconds: float) -> None:
        self.deferred.append((proxy_key, seconds))


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


def test_catalog_fetcher_cools_down_and_retries_same_task_after_rate_limit(monkeypatch) -> None:
    limiter = NoopAsyncLimiter()
    responses = [
        SimpleNamespace(status_code=429),
        SimpleNamespace(status_code=429),
        SimpleNamespace(status_code=200, json=lambda: {"products": [{"id": 10}]}),
    ]

    class FakeClient:
        async def get(self, *args, **kwargs):
            return responses.pop(0)

    async def fake_sleep(seconds: float) -> None:
        return None

    monkeypatch.setattr("WbCatalogFetcher.asyncio.sleep", fake_sleep)

    fetcher = WbCatalogFetcher(
        pages=[],
        search_phrase="test",
        cookies={},
        proxy_key="proxy-1",
        rate_limiter=limiter,
        max_retries=10,
        catalog_limit_cooldown_signals=2,
        catalog_rate_limit_cooldown_seconds=120.0,
        max_total_retryable_attempts=10,
    )

    import asyncio

    result = asyncio.run(fetcher._fetch_one(FakeClient(), {"page": 1, "min_price": 100, "max_price": 200}))

    assert result is not None
    assert result["products"] == [{"id": 10}]
    assert result["__parser_page"] == 1
    assert result["__parser_start_offset"] == 0
    assert limiter.deferred == [("proxy-1", 120.0)]
    assert fetcher.catalog_limit_signals == 2
    assert fetcher.catalog_cooldowns_count == 1
    assert fetcher.catalog_recovered_after_cooldown is True
    assert fetcher.stop_requested is False


def test_catalog_fetcher_hard_fails_after_total_retryable_attempt_budget(monkeypatch) -> None:
    limiter = NoopAsyncLimiter()

    class FakeClient:
        async def get(self, *args, **kwargs):
            return SimpleNamespace(status_code=429)

    async def fake_sleep(seconds: float) -> None:
        return None

    monkeypatch.setattr("WbCatalogFetcher.asyncio.sleep", fake_sleep)

    fetcher = WbCatalogFetcher(
        pages=[],
        search_phrase="test",
        cookies={},
        proxy_key="proxy-1",
        rate_limiter=limiter,
        max_retries=10,
        catalog_limit_cooldown_signals=2,
        catalog_rate_limit_cooldown_seconds=120.0,
        max_total_retryable_attempts=3,
    )

    import asyncio

    result = asyncio.run(fetcher._fetch_one(FakeClient(), {"page": 1, "min_price": 100, "max_price": 200}))

    assert result is None
    assert fetcher.stop_requested is True
    assert fetcher.catalog_limit_signals == 3
    assert fetcher.catalog_cooldowns_count == 1


def test_catalog_fetcher_uses_small_initial_group_for_large_scope(monkeypatch) -> None:
    pages = [DataPage(min_price=index * 100, max_price=index * 100 + 99, total=100) for index in range(130)]
    fetcher = WbCatalogFetcher(
        pages=pages,
        search_phrase="test",
        cookies={},
        price_split_enabled=True,
        batch_size=10,
        batch_delay_bounds=(0.0, 0.0),
        request_delay_bounds=(0.0, 0.0),
    )
    observed_batch_sizes: list[int] = []

    async def fake_fetch_one(client, task):
        return {"products": [{"id": task["page"]}]}

    monkeypatch.setattr(fetcher, "_fetch_one", fake_fetch_one)

    async def collect() -> None:
        async for result_batch in fetcher.iter_result_batches():
            observed_batch_sizes.append(len(result_batch))
            break

    import asyncio

    asyncio.run(collect())

    assert observed_batch_sizes == [2]
