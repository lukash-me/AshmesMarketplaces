from __future__ import annotations

import os
from urllib.parse import quote, urlsplit, urlunsplit

try:
    from app.proxy_mapping import ProxyDefinition
except ImportError:  # pragma: no cover - direct script execution from Parser/app
    from proxy_mapping import ProxyDefinition


def http_proxy_url_from_definition(proxy: ProxyDefinition | None) -> str | None:
    if proxy is None or proxy.type != "http-proxy" or not proxy.base_url:
        return None

    credentials = proxy.credentials or {}
    login = str(credentials.get("login") or credentials.get("username") or "").strip()
    password = str(credentials.get("password") or "").strip()
    if not login or not password:
        return proxy.base_url

    parts = urlsplit(proxy.base_url)
    host = parts.hostname or ""
    if not host:
        return proxy.base_url

    netloc = f"{quote(login, safe='')}:{quote(password, safe='')}@{host}"
    if parts.port:
        netloc = f"{netloc}:{parts.port}"
    return urlunsplit((parts.scheme or "http", netloc, parts.path, parts.query, parts.fragment))


def masked_proxy_url(proxy_url: str | None) -> str:
    if not proxy_url:
        return ""

    parts = urlsplit(proxy_url)
    if not parts.username and not parts.password:
        return proxy_url

    host = parts.hostname or ""
    netloc = f"***:***@{host}"
    if parts.port:
        netloc = f"{netloc}:{parts.port}"
    return urlunsplit((parts.scheme, netloc, parts.path, parts.query, parts.fragment))


def current_http_proxy_url() -> str | None:
    value = os.environ.get("PARSER_HTTP_PROXY_URL")
    return value.strip() if value and value.strip() else None


def _require_proxy_for_marketplace() -> bool:
    value = os.environ.get("PARSER_REQUIRE_PROXY_FOR_MARKETPLACE", "")
    return value.strip().lower() in {"1", "true", "yes", "y", "on"}


def _resolved_proxy_url(proxy_url: str | None, *, caller: str) -> str | None:
    url = proxy_url or current_http_proxy_url()
    if not url and _require_proxy_for_marketplace():
        raise RuntimeError(f"Marketplace proxy is required for {caller}, but no proxy URL is configured.")
    return url


def requests_proxy_kwargs(proxy_url: str | None = None) -> dict:
    url = _resolved_proxy_url(proxy_url, caller="requests")
    if not url:
        return {}
    return {"proxies": {"http": url, "https": url}}


def httpx_proxy_kwargs(proxy_url: str | None = None) -> dict:
    url = _resolved_proxy_url(proxy_url, caller="httpx")
    if not url:
        return {}
    return {"proxy": url}
