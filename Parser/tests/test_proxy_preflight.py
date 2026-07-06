from __future__ import annotations

import sys
from pathlib import Path

import pytest

PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from app.proxy_mapping import ProxyDefinition
from app.proxy_preflight import ProxyPreflightError, run_proxy_preflight


def test_proxy_preflight_rejects_duplicate_egress_ip() -> None:
    proxies = [
        ProxyDefinition(key="proxy-1", type="http-proxy", base_url="http://127.0.0.1:8001"),
        ProxyDefinition(key="proxy-2", type="http-proxy", base_url="http://127.0.0.1:8002"),
    ]

    with pytest.raises(ProxyPreflightError, match="same egress IP"):
        run_proxy_preflight(
            proxies,
            ip_probe=lambda proxy: "203.0.113.10",
            token_probe=lambda proxy: f"token-{proxy.key}",
            profile_path=lambda proxy: Path(f"/profiles/{proxy.key}"),
            cache_path=lambda proxy: Path(f"/sessions/{proxy.key}.json"),
        )


def test_proxy_preflight_accepts_distinct_proxy_identities() -> None:
    proxies = [
        ProxyDefinition(key="proxy-1", type="http-proxy", base_url="http://127.0.0.1:8001"),
        ProxyDefinition(key="proxy-2", type="http-proxy", base_url="http://127.0.0.1:8002"),
    ]

    result = run_proxy_preflight(
        proxies,
        ip_probe=lambda proxy: f"203.0.113.{proxy.key[-1]}",
        token_probe=lambda proxy: f"token-{proxy.key}",
        profile_path=lambda proxy: Path(f"/profiles/{proxy.key}"),
        cache_path=lambda proxy: Path(f"/sessions/{proxy.key}.json"),
    )

    assert [item.proxy_key for item in result.items] == ["proxy-1", "proxy-2"]
    assert [item.egress_ip for item in result.items] == ["203.0.113.1", "203.0.113.2"]
    assert result.items[0].token_ref != result.items[1].token_ref


def test_proxy_preflight_does_not_require_token_probe_by_default() -> None:
    proxies = [
        ProxyDefinition(key="proxy-1", type="http-proxy", base_url="http://127.0.0.1:8001"),
    ]

    result = run_proxy_preflight(
        proxies,
        ip_probe=lambda proxy: "203.0.113.1",
        profile_path=lambda proxy: Path(f"/profiles/{proxy.key}"),
        cache_path=lambda proxy: Path(f"/sessions/{proxy.key}.json"),
    )

    assert result.items[0].token_ref is None
