from __future__ import annotations

import sys
import json
from pathlib import Path
from types import SimpleNamespace

PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

import SearchPhraseParser as parser_module
from SearchPhraseParser import SearchPhraseParser


PRICE_FILTER_NAME = "\u0426\u0435\u043d\u0430"


class NoopRateLimiter:
    def __init__(self) -> None:
        self.proxy_keys: list[str] = []
        self.deferred: list[tuple[str | None, float]] = []

    def wait(self, proxy_key: str) -> None:
        self.proxy_keys.append(proxy_key)

    def defer(self, proxy_key: str | None, seconds: float) -> None:
        self.deferred.append((proxy_key, seconds))


def _filters_payload(*, total: int = 10, min_price: int = 100, max_price: int = 1000) -> dict:
    return {
        "data": {
            "total": total,
            "filters": [{"name": PRICE_FILTER_NAME, "minPriceU": min_price, "maxPriceU": max_price}],
        }
    }


def test_search_phrase_parser_retries_429_and_continues_after_success(monkeypatch) -> None:
    calls = 0
    invalidated: list[tuple[str, str | None]] = []

    def fake_get(*args, **kwargs):
        nonlocal calls
        calls += 1
        if calls < 3:
            return SimpleNamespace(status_code=429)
        return SimpleNamespace(status_code=200, json=lambda: _filters_payload())

    monkeypatch.setattr(parser_module.requests, "get", fake_get)
    monkeypatch.setattr(
        parser_module,
        "invalidate_proxy_session",
        lambda proxy_key, *, reason=None: invalidated.append((proxy_key, reason)),
    )

    limiter = NoopRateLimiter()
    parser = SearchPhraseParser(
        search_phrase="test",
        proxy_key="proxy-1",
        rate_limiter=limiter,
        max_retries=2,
        max_retryable_statuses=8,
    )

    result = parser.fetch_data()

    assert result is not None
    assert parser.aborted_by_rate_limit is False
    assert parser.consecutive_retryable_statuses == 0
    assert calls == 3
    assert limiter.proxy_keys == ["proxy-1", "proxy-1", "proxy-1"]
    assert [item[0] for item in limiter.deferred] == ["proxy-1", "proxy-1"]
    assert invalidated == []


def test_search_phrase_parser_enters_cooldown_after_soft_rate_limit_budget(monkeypatch) -> None:
    calls = 0
    invalidated: list[tuple[str, str | None]] = []

    def fake_get(*args, **kwargs):
        nonlocal calls
        calls += 1
        if calls <= 3:
            return SimpleNamespace(status_code=429)
        return SimpleNamespace(status_code=200, json=lambda: _filters_payload())

    monkeypatch.setattr(parser_module.requests, "get", fake_get)
    monkeypatch.setattr(
        parser_module,
        "invalidate_proxy_session",
        lambda proxy_key, *, reason=None: invalidated.append((proxy_key, reason)),
    )

    limiter = NoopRateLimiter()
    parser = SearchPhraseParser(
        search_phrase="test",
        proxy_key="proxy-1",
        rate_limiter=limiter,
        max_retries=2,
        max_retryable_statuses=3,
        max_total_retryable_attempts=6,
    )
    parser.filter_rate_limit_cooldown_seconds = 120.0

    result = parser.fetch_data()

    assert result is not None
    assert parser.aborted_by_rate_limit is False
    assert calls == 4
    assert [item[0] for item in limiter.deferred] == ["proxy-1", "proxy-1", "proxy-1"]
    assert limiter.deferred[-1][1] == 120.0
    assert invalidated == []


def test_search_phrase_parser_stops_after_hard_rate_limit_budget(monkeypatch) -> None:
    calls = 0
    invalidated: list[tuple[str, str | None]] = []

    def fake_get(*args, **kwargs):
        nonlocal calls
        calls += 1
        return SimpleNamespace(status_code=429)

    monkeypatch.setattr(parser_module.requests, "get", fake_get)
    monkeypatch.setattr(
        parser_module,
        "invalidate_proxy_session",
        lambda proxy_key, *, reason=None: invalidated.append((proxy_key, reason)),
    )

    limiter = NoopRateLimiter()
    parser = SearchPhraseParser(
        search_phrase="test",
        proxy_key="proxy-1",
        rate_limiter=limiter,
        max_retries=2,
        max_retryable_statuses=3,
        max_total_retryable_attempts=5,
    )

    result = parser.fetch_data()

    assert result is None
    assert parser.aborted_by_rate_limit is True
    assert parser.final_error is not None
    assert "last_status=429" in parser.final_error
    assert calls == 5
    assert [item[0] for item in limiter.deferred] == ["proxy-1", "proxy-1", "proxy-1", "proxy-1"]
    assert invalidated == []


def test_search_phrase_parser_invalidates_session_on_auth_status_and_retries_once(monkeypatch) -> None:
    invalidated: list[tuple[str, str | None]] = []
    calls = 0

    def fake_get(*args, **kwargs):
        nonlocal calls
        calls += 1
        return SimpleNamespace(status_code=403)

    monkeypatch.setattr(parser_module.requests, "get", fake_get)
    monkeypatch.setattr(
        parser_module,
        "invalidate_proxy_session",
        lambda proxy_key, *, reason=None: invalidated.append((proxy_key, reason)),
    )

    parser = SearchPhraseParser(
        search_phrase="test",
        proxy_key="proxy-1",
        rate_limiter=NoopRateLimiter(),
        max_retries=2,
        max_retryable_statuses=8,
    )

    result = parser.fetch_data()

    assert result is None
    assert calls == 2
    assert invalidated == [
        ("proxy-1", "WB filters HTTP status 403"),
        ("proxy-1", "WB filters HTTP status 403"),
    ]


def test_search_phrase_parser_invalidates_session_on_498_without_stopping_early(monkeypatch) -> None:
    calls = 0
    invalidated: list[tuple[str, str | None]] = []

    def fake_get(*args, **kwargs):
        nonlocal calls
        calls += 1
        if calls == 1:
            return SimpleNamespace(status_code=498)
        return SimpleNamespace(status_code=200, json=lambda: _filters_payload())

    monkeypatch.setattr(parser_module.requests, "get", fake_get)
    monkeypatch.setattr(
        parser_module,
        "invalidate_proxy_session",
        lambda proxy_key, *, reason=None: invalidated.append((proxy_key, reason)),
    )

    parser = SearchPhraseParser(
        search_phrase="test",
        proxy_key="proxy-1",
        rate_limiter=NoopRateLimiter(),
        max_retries=2,
        max_retryable_statuses=5,
    )

    result = parser.fetch_data()

    assert result is not None
    assert parser.aborted_by_rate_limit is False
    assert calls == 2
    assert invalidated == [("proxy-1", "WB filters HTTP status 498")]


def test_search_phrase_parser_sends_wbaas_token_header(monkeypatch) -> None:
    observed_headers: dict[str, str] = {}

    def fake_get(*args, **kwargs):
        observed_headers.update(kwargs.get("headers") or {})
        return SimpleNamespace(status_code=200, json=lambda: _filters_payload())

    monkeypatch.setattr(parser_module.requests, "get", fake_get)

    parser = SearchPhraseParser(
        search_phrase="test",
        cookies={"x_wbaas_token": "token-1"},
        proxy_key="proxy-1",
        rate_limiter=NoopRateLimiter(),
    )

    result = parser.fetch_data()

    assert result is not None
    assert observed_headers["x_wbaas_token"] == "token-1"


def test_search_phrase_parser_accepts_normal_price_filter_name() -> None:
    page = SearchPhraseParser(search_phrase="test").get_price_range(_filters_payload(total=42, max_price=900))

    assert page is not None
    assert page.min_price == 100
    assert page.max_price == 900
    assert page.total == 42


def test_search_phrase_parser_returns_empty_range_with_fallback_bounds() -> None:
    page = SearchPhraseParser(search_phrase="test").get_price_range(
        {
            "data": {
                "total": 0,
                "filters": [],
            }
        },
        fallback_min_price=100,
        fallback_max_price=900,
    )

    assert page is not None
    assert page.min_price == 100
    assert page.max_price == 900
    assert page.total == 0


def test_search_phrase_parser_rejects_empty_range_without_fallback_bounds() -> None:
    page = SearchPhraseParser(search_phrase="test").get_price_range(
        {
            "data": {
                "total": 0,
                "filters": [],
            }
        }
    )

    assert page is None


def test_search_phrase_parser_first_filters_failure_keeps_split_counters_zero(monkeypatch) -> None:
    parser = SearchPhraseParser(search_phrase="test", rate_limiter=NoopRateLimiter())
    monkeypatch.setattr(parser, "fetch_data", lambda add_params=None: None)

    assert parser.parse() is None
    assert parser.range_checks_count == 0
    assert parser.final_ranges_count == 0
    assert parser.empty_ranges_count == 0
    assert parser.split_ranges_count == 0


def test_search_phrase_parser_counts_empty_price_range(monkeypatch) -> None:
    parser = SearchPhraseParser(search_phrase="test", rate_limiter=NoopRateLimiter())
    monkeypatch.setattr(
        parser,
        "fetch_data",
        lambda add_params=None: {"data": {"total": 0, "filters": []}},
    )

    assert parser.split_price_range(100, 200) == []
    assert parser.range_checks_count == 1
    assert parser.final_ranges_count == 0
    assert parser.empty_ranges_count == 1
    assert parser.split_ranges_count == 0


def test_search_phrase_parser_counts_final_price_range(monkeypatch) -> None:
    parser = SearchPhraseParser(search_phrase="test", rate_limiter=NoopRateLimiter())
    parser.min_step = 0
    monkeypatch.setattr(
        parser,
        "fetch_data",
        lambda add_params=None: _filters_payload(total=42, min_price=100, max_price=200),
    )

    ranges = parser.split_price_range(100, 200)

    assert len(ranges) == 1
    assert parser.range_checks_count == 1
    assert parser.final_ranges_count == 1
    assert parser.empty_ranges_count == 0
    assert parser.split_ranges_count == 0


def test_search_phrase_parser_counts_split_price_range(monkeypatch) -> None:
    parser = SearchPhraseParser(search_phrase="test", rate_limiter=NoopRateLimiter())
    parser.min_step = 0
    parser.max_count_of_good = 50
    parser.max_split_depth = 0
    monkeypatch.setattr(
        parser,
        "fetch_data",
        lambda add_params=None: _filters_payload(total=100, min_price=100, max_price=200),
    )

    assert parser.split_price_range(100, 200) == []
    assert parser.range_checks_count == 1
    assert parser.final_ranges_count == 0
    assert parser.empty_ranges_count == 0
    assert parser.split_ranges_count == 1


def test_search_phrase_parser_uses_bounded_total_when_full_range_is_undercounted(monkeypatch) -> None:
    parser = SearchPhraseParser(search_phrase="test", rate_limiter=NoopRateLimiter())
    parser.max_count_of_good = 1_000_000
    calls: list[dict | None] = []

    def fake_fetch_data(add_params=None):
        calls.append(add_params)
        if add_params == {"priceU": "100;999"}:
            return _filters_payload(total=655_730, min_price=100, max_price=1000)
        return _filters_payload(total=220_000, min_price=100, max_price=1000)

    monkeypatch.setattr(parser, "fetch_data", fake_fetch_data)

    ranges = parser.parse()

    assert calls[:2] == [None, {"priceU": "100;999"}]
    assert parser.anti_full_range_detected is True
    assert parser.unbounded_total == 220_000
    assert parser.bounded_total == 655_730
    assert parser.effective_min_price_u == 100
    assert parser.effective_max_price_u == 999
    assert ranges == [parser_module.DataPage(100, 999, 655_730)]


def test_search_phrase_parser_keeps_unbounded_total_when_bounded_is_close(monkeypatch) -> None:
    parser = SearchPhraseParser(search_phrase="test", rate_limiter=NoopRateLimiter())
    parser.max_count_of_good = 1_000_000

    def fake_fetch_data(add_params=None):
        if add_params == {"priceU": "100;999"}:
            return _filters_payload(total=230_000, min_price=100, max_price=1000)
        return _filters_payload(total=220_000, min_price=100, max_price=1000)

    monkeypatch.setattr(parser, "fetch_data", fake_fetch_data)

    ranges = parser.parse()

    assert parser.anti_full_range_detected is False
    assert parser.unbounded_total == 220_000
    assert parser.bounded_total == 230_000
    assert parser.effective_min_price_u == 100
    assert parser.effective_max_price_u == 1000
    assert ranges == [parser_module.DataPage(100, 1000, 220_000)]


def test_search_phrase_parser_writes_structured_range_events(monkeypatch) -> None:
    messages: list[str] = []
    parser = SearchPhraseParser(
        search_phrase="Платья и сарафаны",
        proxy_key="proxy-1",
        source_category="Женщинам",
        source_subcategory="Платья и сарафаны",
        rate_limiter=NoopRateLimiter(),
    )
    monkeypatch.setattr(parser_module.logger, "info", lambda message, *args, **kwargs: messages.append(message.format(*args)))

    parser._record_analyzed_range(parser_module.DataPage(min_price=100, max_price=200, total=0))

    event_line = next(item for item in messages if item.startswith("PARSER_EVENT "))
    payload = json.loads(event_line.removeprefix("PARSER_EVENT "))
    assert payload["event"] == "range_empty"
    assert payload["proxyKey"] == "proxy-1"
    assert payload["rangeChecksCount"] == 1
    assert payload["emptyRangesCount"] == 1
