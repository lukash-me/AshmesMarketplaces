from __future__ import annotations

import sys
from pathlib import Path

import pytest


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from app.proxy_transport import httpx_proxy_kwargs, requests_proxy_kwargs  # noqa: E402


def test_marketplace_proxy_requirement_rejects_direct_requests(monkeypatch) -> None:
    monkeypatch.setenv("PARSER_REQUIRE_PROXY_FOR_MARKETPLACE", "true")
    monkeypatch.delenv("PARSER_HTTP_PROXY_URL", raising=False)

    with pytest.raises(RuntimeError, match="Marketplace proxy is required"):
        requests_proxy_kwargs()

    with pytest.raises(RuntimeError, match="Marketplace proxy is required"):
        httpx_proxy_kwargs()


def test_marketplace_proxy_requirement_accepts_explicit_proxy(monkeypatch) -> None:
    monkeypatch.setenv("PARSER_REQUIRE_PROXY_FOR_MARKETPLACE", "true")
    monkeypatch.delenv("PARSER_HTTP_PROXY_URL", raising=False)

    assert requests_proxy_kwargs("http://proxy.local:8080") == {
        "proxies": {
            "http": "http://proxy.local:8080",
            "https": "http://proxy.local:8080",
        }
    }
    assert httpx_proxy_kwargs("http://proxy.local:8080") == {"proxy": "http://proxy.local:8080"}
