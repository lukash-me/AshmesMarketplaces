from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable

import httpx

try:
    from app.browser_sessions import get_token_for_proxy, proxy_profile_dir, proxy_session_cache_path, token_ref
    from app.proxy_mapping import ProxyDefinition
    from app.proxy_transport import httpx_proxy_kwargs, masked_proxy_url, http_proxy_url_from_definition
except ImportError:  # pragma: no cover
    from browser_sessions import get_token_for_proxy, proxy_profile_dir, proxy_session_cache_path, token_ref
    from proxy_mapping import ProxyDefinition
    from proxy_transport import httpx_proxy_kwargs, masked_proxy_url, http_proxy_url_from_definition


class ProxyPreflightError(RuntimeError):
    pass


@dataclass(frozen=True)
class ProxyPreflightItem:
    proxy_key: str
    proxy_type: str
    egress_ip: str
    token_ref: str | None
    profile_dir: Path
    cache_path: Path


@dataclass(frozen=True)
class ProxyPreflightResult:
    items: list[ProxyPreflightItem]


IpProbe = Callable[[ProxyDefinition], str]
TokenProbe = Callable[[ProxyDefinition], str | None]
PathProbe = Callable[[ProxyDefinition], Path]


def default_ip_probe(proxy: ProxyDefinition) -> str:
    proxy_url = http_proxy_url_from_definition(proxy)
    with httpx.Client(timeout=20.0, **httpx_proxy_kwargs(proxy_url)) as client:
        response = client.get("https://api.ipify.org", params={"format": "json"})
        response.raise_for_status()
        payload = response.json()
    ip = str(payload.get("ip") or "").strip()
    if not ip:
        raise ProxyPreflightError(f"Proxy {proxy.key} did not return an egress IP.")
    return ip


def default_token_probe(proxy: ProxyDefinition) -> str | None:
    return get_token_for_proxy(proxy.key, proxy)


def run_proxy_preflight(
    proxies: Iterable[ProxyDefinition],
    *,
    ip_probe: IpProbe = default_ip_probe,
    token_probe: TokenProbe | None = None,
    profile_path: PathProbe = lambda proxy: proxy_profile_dir(proxy.key),
    cache_path: PathProbe = lambda proxy: proxy_session_cache_path(proxy.key),
) -> ProxyPreflightResult:
    items: list[ProxyPreflightItem] = []
    by_ip: dict[str, str] = {}
    by_token_ref: dict[str, str] = {}

    for proxy in proxies:
        if proxy.type == "direct":
            continue
        egress_ip = ip_probe(proxy).strip()
        if egress_ip in by_ip:
            raise ProxyPreflightError(
                f"Proxies {by_ip[egress_ip]} and {proxy.key} use the same egress IP {egress_ip}."
            )
        by_ip[egress_ip] = proxy.key

        token = token_probe(proxy) if token_probe is not None else None
        ref = token_ref(token)
        if token_probe is not None:
            if not token:
                raise ProxyPreflightError(f"Proxy {proxy.key} did not acquire WB token.")
            if ref and ref in by_token_ref:
                raise ProxyPreflightError(
                    f"Proxies {by_token_ref[ref]} and {proxy.key} use the same WB token reference {ref}."
                )
            if ref:
                by_token_ref[ref] = proxy.key

        items.append(
            ProxyPreflightItem(
                proxy_key=proxy.key,
                proxy_type=proxy.type,
                egress_ip=egress_ip,
                token_ref=ref,
                profile_dir=profile_path(proxy),
                cache_path=cache_path(proxy),
            )
        )

    return ProxyPreflightResult(items=items)


def format_proxy_preflight_result(result: ProxyPreflightResult) -> list[dict[str, str | None]]:
    return [
        {
            "proxyKey": item.proxy_key,
            "proxyType": item.proxy_type,
            "egressIp": item.egress_ip,
            "tokenRef": item.token_ref,
            "profileDir": str(item.profile_dir),
            "cachePath": str(item.cache_path),
        }
        for item in result.items
    ]
