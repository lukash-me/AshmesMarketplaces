from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

import SearchPhraseParser as parser_module
from SearchPhraseParser import SearchPhraseParser


class NoopRateLimiter:
    def __init__(self) -> None:
        self.proxy_keys: list[str] = []

    def wait(self, proxy_key: str) -> None:
        self.proxy_keys.append(proxy_key)


def test_search_phrase_parser_stops_after_repeated_rate_limit(monkeypatch) -> None:
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
        max_retryable_statuses=1,
    )

    result = parser.parse()

    assert result == []
    assert parser.aborted_by_rate_limit is True
    assert calls == 1
    assert limiter.proxy_keys == ["proxy-1"]
    assert invalidated == []


def test_search_phrase_parser_invalidates_session_on_auth_or_antibot_status(monkeypatch) -> None:
    invalidated: list[tuple[str, str | None]] = []

    def fake_get(*args, **kwargs):
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
        max_retryable_statuses=1,
    )

    result = parser.fetch_data()

    assert result is None
    assert invalidated == [("proxy-1", "WB filters HTTP status 403")]


def test_search_phrase_parser_marks_final_rate_limit_even_below_status_threshold(monkeypatch) -> None:
    calls = 0

    def fake_get(*args, **kwargs):
        nonlocal calls
        calls += 1
        return SimpleNamespace(status_code=429)

    monkeypatch.setattr(parser_module.requests, "get", fake_get)
    monkeypatch.setattr(parser_module, "invalidate_proxy_session", lambda *args, **kwargs: None)

    parser = SearchPhraseParser(
        search_phrase="test",
        proxy_key="proxy-1",
        rate_limiter=NoopRateLimiter(),
        max_retries=2,
        max_retryable_statuses=5,
    )

    result = parser.fetch_data()

    assert result is None
    assert parser.aborted_by_rate_limit is True
    assert calls == 3


def test_search_phrase_parser_sends_wbaas_token_header(monkeypatch) -> None:
    observed_headers: dict[str, str] = {}

    def fake_get(*args, **kwargs):
        observed_headers.update(kwargs.get("headers") or {})
        return SimpleNamespace(
            status_code=200,
            json=lambda: {
                "data": {
                    "total": 10,
                    "filters": [{"name": "Цена", "minPriceU": 100, "maxPriceU": 1000}],
                }
            },
        )

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
    page = SearchPhraseParser(search_phrase="test").get_price_range(
        {
            "data": {
                "total": 42,
                "filters": [{"name": "Цена", "minPriceU": 100, "maxPriceU": 900}],
            }
        }
    )

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
