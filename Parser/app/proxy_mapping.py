from __future__ import annotations

import json
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class ProxyDefinition:
    key: str
    type: str = "direct"
    base_url: str | None = None
    socks5_url: str | None = None
    credentials: dict[str, Any] | None = None
    healthcheck: str | None = None
    rate_limit_per_minute: int | None = None
    cooldown_seconds: int = 300


@dataclass(frozen=True)
class NicheProxyAssignment:
    source_category: str
    source_subcategory: str
    proxy_key: str
    enabled: bool = True
    wb_category_id: int | None = None
    source_path: str | None = None
    search_query: str | None = None
    parser_search_text: str | None = None


@dataclass(frozen=True)
class ResolvedProxy:
    proxy: ProxyDefinition
    assignment: NicheProxyAssignment | None
    enabled: bool = True


class ProxyMapping:
    def __init__(
        self,
        *,
        default_proxy: ProxyDefinition,
        proxies: dict[str, ProxyDefinition],
        assignments: list[NicheProxyAssignment],
    ) -> None:
        self.default_proxy = default_proxy
        self.proxies = dict(proxies)
        self.proxies.setdefault(default_proxy.key, default_proxy)
        self.assignments = list(assignments)

    @classmethod
    def load(cls, path: str | Path) -> "ProxyMapping":
        payload = json.loads(Path(path).read_text(encoding="utf-8-sig"))
        return cls.from_dict(payload)

    @classmethod
    def effective_path(cls, path: str | Path) -> Path:
        requested = Path(path)
        if requested.name.endswith(".local.json"):
            return requested

        local_path = requested.with_name(f"{requested.stem}.local{requested.suffix}")
        return local_path if local_path.exists() else requested

    @classmethod
    def load_with_local_override(cls, path: str | Path) -> "ProxyMapping":
        return cls.load(cls.effective_path(path))

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "ProxyMapping":
        default_proxy = _proxy_from_dict(payload.get("defaultProxy") or {"key": "local-proxy", "type": "direct"})
        proxies = {default_proxy.key: default_proxy}
        for proxy_payload in payload.get("proxies") or []:
            proxy = _proxy_from_dict(proxy_payload)
            proxies[proxy.key] = proxy

        assignments = [
            NicheProxyAssignment(
                source_category=str(item.get("sourceCategory") or "").strip(),
                source_subcategory=str(item.get("sourceSubcategory") or "").strip(),
                proxy_key=str(item.get("proxyKey") or default_proxy.key).strip(),
                enabled=bool(item.get("enabled", True)),
                wb_category_id=_optional_int(item.get("wbCategoryId")),
                source_path=str(item.get("sourcePath") or "").strip() or None,
                search_query=str(item.get("searchQuery") or "").strip() or None,
                parser_search_text=str(item.get("parserSearchText") or "").strip() or None,
            )
            for item in payload.get("niches") or []
        ]
        return cls(default_proxy=default_proxy, proxies=proxies, assignments=assignments)

    @classmethod
    def local_default(cls, proxy_key: str = "local") -> "ProxyMapping":
        proxy = ProxyDefinition(key=proxy_key, type="direct", cooldown_seconds=300)
        return cls(default_proxy=proxy, proxies={proxy.key: proxy}, assignments=[])

    def resolve(self, source_category: str | None, source_subcategory: str | None) -> ResolvedProxy:
        category = (source_category or "").strip()
        subcategory = (source_subcategory or "").strip()
        assignment = next(
            (
                item
                for item in self.assignments
                if item.source_category == category and item.source_subcategory == subcategory
            ),
            None,
        )
        if assignment is None:
            return ResolvedProxy(proxy=self.default_proxy, assignment=None, enabled=True)

        proxy = self.proxies.get(assignment.proxy_key) or self.default_proxy
        return ResolvedProxy(proxy=proxy, assignment=assignment, enabled=assignment.enabled)


class ProxyScheduler:
    def __init__(self, *, now: Any | None = None) -> None:
        self._busy: set[str] = set()
        self._cooldown_until: dict[str, float] = {}
        self._now = now or time.monotonic

    def status(self, proxy_key: str) -> str:
        now_value = float(self._now())
        if proxy_key in self._busy:
            return "busy"
        if self._cooldown_until.get(proxy_key, 0.0) > now_value:
            return "cooldown"
        return "ready"

    def can_run(self, proxy_key: str) -> bool:
        return self.status(proxy_key) == "ready"

    def mark_started(self, proxy_key: str) -> None:
        self._busy.add(proxy_key)

    def mark_finished(self, proxy_key: str) -> None:
        self._busy.discard(proxy_key)

    def mark_failed(self, proxy: ProxyDefinition, *, cooldown_seconds: int | None = None) -> None:
        self._busy.discard(proxy.key)
        seconds = proxy.cooldown_seconds if cooldown_seconds is None else cooldown_seconds
        self._cooldown_until[proxy.key] = float(self._now()) + max(0, seconds)


def _proxy_from_dict(payload: dict[str, Any]) -> ProxyDefinition:
    key = str(payload.get("key") or "").strip()
    if not key:
        raise ValueError("Proxy key is required.")

    return ProxyDefinition(
        key=key,
        type=str(payload.get("type") or "direct").strip(),
        base_url=payload.get("baseUrl"),
        socks5_url=payload.get("socks5Url"),
        credentials=payload.get("credentials"),
        healthcheck=payload.get("healthcheck"),
        rate_limit_per_minute=payload.get("rateLimitPerMinute"),
        cooldown_seconds=int(payload.get("cooldownSeconds") or 300),
    )


def _optional_int(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None
