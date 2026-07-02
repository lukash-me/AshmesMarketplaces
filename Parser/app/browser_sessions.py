from __future__ import annotations

import json
import os
import re
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlsplit, urlunsplit

try:
    from common_data import HEADERS
except ImportError:  # pragma: no cover - direct app module execution
    HEADERS = {
        "user-agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36"
        )
    }

try:
    from app.proxy_mapping import ProxyDefinition
except ImportError:  # pragma: no cover - direct script execution from Parser/app
    from proxy_mapping import ProxyDefinition


COOKIE_NAME = "x_wbaas_token"
WB_HOME_URL = "https://www.wildberries.ru/"
PARSER_ROOT = Path(__file__).resolve().parents[1]


@dataclass(frozen=True)
class BrowserSessionSnapshot:
    proxy_key: str
    status: str
    profile_dir: Path
    cache_path: Path
    refreshed_at_utc: datetime | None
    expires_at_utc: datetime | None
    failure_count: int = 0
    last_error: str | None = None
    token_ref: str | None = None


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


def _parse_utc(value: Any) -> datetime | None:
    if not value:
        return None
    try:
        text = str(value)
        if text.endswith("Z"):
            text = f"{text[:-1]}+00:00"
        parsed = datetime.fromisoformat(text)
        if parsed.tzinfo is None:
            return parsed.replace(tzinfo=timezone.utc)
        return parsed.astimezone(timezone.utc)
    except ValueError:
        return None


def _format_utc(value: datetime) -> str:
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def safe_proxy_key(proxy_key: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_.-]+", "_", proxy_key).strip("._")
    return cleaned or "default"


def browser_profiles_root() -> Path:
    value = os.environ.get("PARSER_BROWSER_PROFILE_DIR")
    return Path(value) if value else PARSER_ROOT / "output" / "browser_profiles"


def browser_session_cache_root() -> Path:
    value = os.environ.get("PARSER_BROWSER_SESSION_CACHE_DIR")
    return Path(value) if value else PARSER_ROOT / "output" / "browser_sessions"


def proxy_profile_dir(proxy_key: str, *, root: Path | None = None) -> Path:
    return (root or browser_profiles_root()) / safe_proxy_key(proxy_key)


def proxy_session_cache_path(proxy_key: str, *, root: Path | None = None) -> Path:
    return (root or browser_session_cache_root()) / f"{safe_proxy_key(proxy_key)}.json"


def token_ref(token: str | None) -> str | None:
    if not token:
        return None
    if len(token) <= 10:
        return "***"
    return f"{token[:6]}...{token[-4:]}"


def playwright_proxy_options(proxy: ProxyDefinition | None) -> dict[str, str] | None:
    if proxy is None or proxy.type == "direct":
        return None
    if proxy.type != "http-proxy":
        raise ValueError(f"Unsupported proxy type for Playwright session: {proxy.type}")
    if not proxy.base_url:
        raise ValueError(f"Proxy {proxy.key} has no baseUrl.")

    parts = urlsplit(proxy.base_url)
    host = parts.hostname or ""
    if not host:
        raise ValueError(f"Proxy {proxy.key} baseUrl has no host.")

    netloc = host
    if parts.port:
        netloc = f"{netloc}:{parts.port}"
    server = urlunsplit((parts.scheme or "http", netloc, parts.path, parts.query, parts.fragment))

    credentials = proxy.credentials or {}
    username = str(credentials.get("login") or credentials.get("username") or parts.username or "").strip()
    password = str(credentials.get("password") or parts.password or "").strip()

    result = {"server": server}
    if username:
        result["username"] = unquote(username)
    if password:
        result["password"] = unquote(password)
    return result


def masked_playwright_proxy_options(proxy: ProxyDefinition | None) -> dict[str, str] | None:
    options = playwright_proxy_options(proxy)
    if options is None:
        return None
    safe = dict(options)
    if "username" in safe:
        safe["username"] = "***"
    if "password" in safe:
        safe["password"] = "***"
    return safe


def _ttl_minutes() -> int:
    raw = os.environ.get("PARSER_SESSION_TTL_MINUTES")
    try:
        return max(1, int(raw)) if raw else 60
    except ValueError:
        return 60


def _headed() -> bool:
    return os.environ.get("PARSER_BROWSER_HEADED", "").strip().lower() in {"1", "true", "yes", "y", "on"}


def _force_refresh() -> bool:
    return os.environ.get("PARSER_FORCE_REFRESH_TOKEN", "").strip().lower() in {"1", "true", "yes", "y", "on"}


def _read_cache(path: Path) -> dict[str, Any] | None:
    if not path.exists():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None


def _write_cache(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def _is_cache_valid(payload: dict[str, Any] | None, *, now: datetime | None = None) -> bool:
    if not payload or not payload.get("token"):
        return False
    expires_at = _parse_utc(payload.get("expiresAtUtc"))
    if expires_at is None:
        return False
    return expires_at > (now or _utc_now())


def session_snapshot(proxy_key: str) -> BrowserSessionSnapshot:
    cache_path = proxy_session_cache_path(proxy_key)
    payload = _read_cache(cache_path) or {}
    return BrowserSessionSnapshot(
        proxy_key=proxy_key,
        status=str(payload.get("status") or "missing"),
        profile_dir=proxy_profile_dir(proxy_key),
        cache_path=cache_path,
        refreshed_at_utc=_parse_utc(payload.get("refreshedAtUtc")),
        expires_at_utc=_parse_utc(payload.get("expiresAtUtc")),
        failure_count=int(payload.get("failureCount") or 0),
        last_error=payload.get("lastError"),
        token_ref=token_ref(payload.get("token")),
    )


def get_token_for_proxy(
    proxy_key: str,
    proxy: ProxyDefinition | None = None,
    *,
    user_agent: str | None = None,
) -> str | None:
    cache_path = proxy_session_cache_path(proxy_key)
    cached = _read_cache(cache_path)
    if not _force_refresh() and _is_cache_valid(cached):
        return str(cached["token"])

    return _refresh_token_for_proxy(proxy_key, proxy, user_agent=user_agent)


def get_cookies_for_proxy(
    proxy_key: str,
    proxy: ProxyDefinition | None = None,
    *,
    user_agent: str | None = None,
) -> dict[str, str] | None:
    token = get_token_for_proxy(proxy_key, proxy, user_agent=user_agent)
    if not token:
        return None

    cached = _read_cache(proxy_session_cache_path(proxy_key)) or {}
    cookies: dict[str, str] = {}
    for cookie in cached.get("cookies") or []:
        if not isinstance(cookie, dict):
            continue
        name = str(cookie.get("name") or "").strip()
        value = cookie.get("value")
        if name and value is not None:
            cookies[name] = str(value)
    cookies[COOKIE_NAME] = token
    return cookies


def invalidate_proxy_session(proxy_key: str, *, reason: str | None = None) -> None:
    cache_path = proxy_session_cache_path(proxy_key)
    cached = _read_cache(cache_path) or {}
    cached.update(
        {
            "status": "cooldown",
            "expiresAtUtc": _format_utc(_utc_now()),
            "lastError": reason,
            "updatedAtUtc": _format_utc(_utc_now()),
        }
    )
    _write_cache(cache_path, cached)


def _refresh_token_for_proxy(
    proxy_key: str,
    proxy: ProxyDefinition | None,
    *,
    user_agent: str | None = None,
) -> str | None:
    cache_path = proxy_session_cache_path(proxy_key)
    previous = _read_cache(cache_path) or {}
    now = _utc_now()
    profile_dir = proxy_profile_dir(proxy_key)
    profile_dir.mkdir(parents=True, exist_ok=True)

    try:
        from playwright.sync_api import TimeoutError as PlaywrightTimeoutError
        from playwright.sync_api import sync_playwright
    except ModuleNotFoundError as exception:  # pragma: no cover - environment-specific
        raise RuntimeError(
            "Playwright is not installed. Run: python -m pip install -r Parser/requirements.txt "
            "and python -m playwright install chromium."
        ) from exception

    token: str | None = None
    cookies: list[dict[str, Any]] = []
    effective_user_agent = user_agent or HEADERS.get("user-agent")
    status = "refreshing"

    try:
        with sync_playwright() as playwright:
            context = playwright.chromium.launch_persistent_context(
                user_data_dir=str(profile_dir),
                headless=not _headed(),
                user_agent=effective_user_agent,
                proxy=playwright_proxy_options(proxy),
                locale="ru-RU",
                viewport={"width": 1365, "height": 768},
            )
            try:
                page = context.pages[0] if context.pages else context.new_page()
                page.goto(WB_HOME_URL, wait_until="domcontentloaded", timeout=60000)
                try:
                    page.wait_for_load_state("networkidle", timeout=15000)
                except PlaywrightTimeoutError:
                    pass

                for _ in range(6):
                    cookies = context.cookies()
                    token = next(
                        (str(cookie.get("value")) for cookie in cookies if cookie.get("name") == COOKIE_NAME),
                        None,
                    )
                    if token:
                        break
                    page.wait_for_timeout(5000)
            finally:
                context.close()

        status = "valid" if token else "failed"
        refreshed_at = _utc_now()
        expires_at = refreshed_at + timedelta(minutes=_ttl_minutes())
        payload = {
            "proxyKey": proxy_key,
            "status": status,
            "token": token,
            "tokenRef": token_ref(token),
            "cookies": cookies,
            "userAgent": effective_user_agent,
            "refreshedAtUtc": _format_utc(refreshed_at),
            "expiresAtUtc": _format_utc(expires_at if token else now),
            "failureCount": 0 if token else int(previous.get("failureCount") or 0) + 1,
            "lastError": None if token else f"Cookie {COOKIE_NAME} was not found.",
            "updatedAtUtc": _format_utc(refreshed_at),
        }
        _write_cache(cache_path, payload)
        return token
    except Exception as exception:
        failure_count = int(previous.get("failureCount") or 0) + 1
        _write_cache(
            cache_path,
            {
                **previous,
                "proxyKey": proxy_key,
                "status": "failed",
                "failureCount": failure_count,
                "lastError": str(exception),
                "expiresAtUtc": _format_utc(now),
                "updatedAtUtc": _format_utc(_utc_now()),
            },
        )
        raise
