from __future__ import annotations

import os
from urllib.parse import urlencode, urljoin

import requests

try:
    from app.proxy_mapping import ProxyMapping
except ImportError:  # pragma: no cover - direct script execution from Parser/app
    from proxy_mapping import ProxyMapping


def runtime_proxy_assignments_url(
    batch_queue_url: str | None = None,
    *,
    parser_instance_id: str | None = None,
) -> str | None:
    explicit = (os.environ.get("PARSER_RUNTIME_PROXY_ASSIGNMENTS_URL") or "").strip()
    if explicit:
        base_url = explicit
        if parser_instance_id and "parserInstanceId=" not in base_url:
            separator = "&" if "?" in base_url else "?"
            return f"{base_url}{separator}{urlencode({'parserInstanceId': parser_instance_id})}"
        return base_url

    base = (batch_queue_url or os.environ.get("PARSER_BATCH_QUEUE_URL") or "").strip()
    if not base:
        return None

    normalized = base.rstrip("/") + "/"
    if normalized.endswith("/api/v1/parser/"):
        base_url = urljoin(normalized, "runtime/proxy-assignments")
    else:
        base_url = urljoin(normalized, "api/v1/parser/runtime/proxy-assignments")

    if parser_instance_id:
        return f"{base_url}?{urlencode({'parserInstanceId': parser_instance_id})}"
    return base_url


def load_runtime_proxy_mapping(url: str, *, api_key: str | None = None, timeout_seconds: float = 15) -> ProxyMapping:
    headers = {}
    effective_api_key = (api_key or os.environ.get("PARSER_API_KEY") or "").strip()
    if effective_api_key:
        headers["X-Parser-Api-Key"] = effective_api_key

    response = requests.get(url, headers=headers, timeout=timeout_seconds)
    response.raise_for_status()
    payload = response.json()
    return ProxyMapping.from_dict(payload)
