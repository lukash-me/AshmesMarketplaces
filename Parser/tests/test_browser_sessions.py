from __future__ import annotations

import json
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from app.browser_sessions import (  # noqa: E402
    browser_session_cache_root,
    get_browser_session_for_proxy,
    get_token_for_proxy,
    invalidate_proxy_session,
    masked_playwright_proxy_options,
    playwright_proxy_options,
    proxy_profile_dir,
    proxy_session_cache_path,
    safe_proxy_key,
    session_snapshot,
)
from app.proxy_mapping import ProxyDefinition  # noqa: E402


class BrowserSessionTests(unittest.TestCase):
    def test_profile_paths_are_scoped_by_proxy_key(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)

            proxy_1 = proxy_profile_dir("proxy-1", root=root)
            proxy_2 = proxy_profile_dir("proxy-2", root=root)

            self.assertNotEqual(proxy_1, proxy_2)
            self.assertEqual(proxy_1.name, "proxy-1")
            self.assertEqual(proxy_2.name, "proxy-2")

    def test_cache_path_uses_safe_proxy_key(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = proxy_session_cache_path("proxy 1/@secret", root=Path(temp))

            self.assertEqual(path.name, "proxy_1_secret.json")
            self.assertEqual(safe_proxy_key("proxy 1/@secret"), "proxy_1_secret")

    def test_session_scope_changes_profile_and_cache_paths(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            import app.browser_sessions as browser_sessions

            old_scope = browser_sessions.os.environ.get("PARSER_SESSION_SCOPE")
            try:
                browser_sessions.os.environ["PARSER_SESSION_SCOPE"] = "cycle-1/proxy-1"
                profile_1 = proxy_profile_dir("proxy-1", root=root / "profiles")
                cache_1 = proxy_session_cache_path("proxy-1", root=root / "sessions")

                browser_sessions.os.environ["PARSER_SESSION_SCOPE"] = "cycle-2/proxy-1"
                profile_2 = proxy_profile_dir("proxy-1", root=root / "profiles")
                cache_2 = proxy_session_cache_path("proxy-1", root=root / "sessions")

                self.assertNotEqual(profile_1, profile_2)
                self.assertNotEqual(cache_1, cache_2)
                self.assertEqual(profile_1, root / "profiles" / "cycle-1" / "proxy-1")
                self.assertEqual(cache_1, root / "sessions" / "cycle-1" / "proxy-1" / "session.json")
            finally:
                if old_scope is None:
                    browser_sessions.os.environ.pop("PARSER_SESSION_SCOPE", None)
                else:
                    browser_sessions.os.environ["PARSER_SESSION_SCOPE"] = old_scope

    def test_playwright_proxy_options_use_credentials_without_masked_password(self) -> None:
        proxy = ProxyDefinition(
            key="proxy-1",
            type="http-proxy",
            base_url="http://127.0.0.1:8080",
            credentials={"login": "user@example.com", "password": "secret"},
        )

        options = playwright_proxy_options(proxy)
        masked = masked_playwright_proxy_options(proxy)

        self.assertEqual(options, {"server": "http://127.0.0.1:8080", "username": "user@example.com", "password": "secret"})
        self.assertEqual(masked, {"server": "http://127.0.0.1:8080", "username": "***", "password": "***"})
        self.assertNotIn("secret", json.dumps(masked))

    def test_direct_proxy_has_no_playwright_proxy_options(self) -> None:
        self.assertIsNone(playwright_proxy_options(ProxyDefinition(key="local", type="direct")))

    def test_cached_token_is_scoped_and_reused_before_expiry(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            cache_root = Path(temp)
            old_value = browser_session_cache_root
            try:
                import app.browser_sessions as browser_sessions

                browser_sessions.browser_session_cache_root = lambda: cache_root
                expires_at = datetime.now(timezone.utc) + timedelta(minutes=10)
                proxy_session_cache_path("proxy-1").write_text(
                    json.dumps({"token": "token-for-proxy-1", "expiresAtUtc": expires_at.isoformat()}),
                    encoding="utf-8",
                )
                proxy_session_cache_path("proxy-2").write_text(
                    json.dumps({"token": "token-for-proxy-2", "expiresAtUtc": expires_at.isoformat()}),
                    encoding="utf-8",
                )

                self.assertEqual(get_token_for_proxy("proxy-1"), "token-for-proxy-1")
                self.assertEqual(get_token_for_proxy("proxy-2"), "token-for-proxy-2")
            finally:
                browser_sessions.browser_session_cache_root = old_value

    def test_run_cache_mode_ignores_valid_disk_cache_but_reuses_memory_session(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            cache_root = Path(temp)
            import app.browser_sessions as browser_sessions

            old_cache_root = browser_session_cache_root
            old_refresh = browser_sessions._refresh_browser_session_for_proxy
            old_mode = browser_sessions.os.environ.get("PARSER_SESSION_CACHE_MODE")
            old_scope = browser_sessions.os.environ.get("PARSER_SESSION_SCOPE")
            calls: list[str] = []
            try:
                browser_sessions.browser_session_cache_root = lambda: cache_root
                browser_sessions.os.environ["PARSER_SESSION_CACHE_MODE"] = "run"
                browser_sessions.os.environ["PARSER_SESSION_SCOPE"] = "cycle-run/proxy-1"
                expires_at = datetime.now(timezone.utc) + timedelta(minutes=10)
                cache_path = proxy_session_cache_path("proxy-1")
                cache_path.parent.mkdir(parents=True, exist_ok=True)
                cache_path.write_text(
                    json.dumps(
                        {
                            "token": "old-disk-token",
                            "cookies": [{"name": "x_wbaas_token", "value": "old-disk-token"}],
                            "userAgent": "Old UA",
                            "expiresAtUtc": expires_at.isoformat(),
                        }
                    ),
                    encoding="utf-8",
                )

                def fake_refresh(proxy_key: str, proxy=None, *, user_agent: str | None = None):
                    calls.append(proxy_key)
                    return browser_sessions.BrowserSessionData(
                        proxy_key=proxy_key,
                        token="fresh-run-token",
                        cookies={"x_wbaas_token": "fresh-run-token", "session": "fresh-cookie"},
                        user_agent=user_agent or "Fresh UA",
                        token_ref="fresh...oken",
                        profile_dir=proxy_profile_dir(proxy_key),
                        cache_path=proxy_session_cache_path(proxy_key),
                    )

                browser_sessions._refresh_browser_session_for_proxy = fake_refresh

                first = get_browser_session_for_proxy("proxy-1", user_agent="Fresh UA")
                second = get_browser_session_for_proxy("proxy-1", user_agent="Fresh UA")

                self.assertEqual(first.token, "fresh-run-token")
                self.assertEqual(second.token, "fresh-run-token")
                self.assertEqual(calls, ["proxy-1"])
            finally:
                browser_sessions.browser_session_cache_root = old_cache_root
                browser_sessions._refresh_browser_session_for_proxy = old_refresh
                browser_sessions._MEMORY_SESSIONS.clear()
                if old_mode is None:
                    browser_sessions.os.environ.pop("PARSER_SESSION_CACHE_MODE", None)
                else:
                    browser_sessions.os.environ["PARSER_SESSION_CACHE_MODE"] = old_mode
                if old_scope is None:
                    browser_sessions.os.environ.pop("PARSER_SESSION_SCOPE", None)
                else:
                    browser_sessions.os.environ["PARSER_SESSION_SCOPE"] = old_scope

    def test_invalidate_marks_only_one_proxy_session_expired(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            cache_root = Path(temp)
            old_value = browser_session_cache_root
            try:
                import app.browser_sessions as browser_sessions

                browser_sessions.browser_session_cache_root = lambda: cache_root
                expires_at = datetime.now(timezone.utc) + timedelta(minutes=10)
                for proxy_key in ("proxy-1", "proxy-2"):
                    proxy_session_cache_path(proxy_key).write_text(
                        json.dumps({"status": "valid", "token": f"token-{proxy_key}", "expiresAtUtc": expires_at.isoformat()}),
                        encoding="utf-8",
                    )

                invalidate_proxy_session("proxy-1", reason="429")

                self.assertEqual(session_snapshot("proxy-1").status, "cooldown")
                self.assertEqual(session_snapshot("proxy-2").status, "valid")
            finally:
                browser_sessions.browser_session_cache_root = old_value


if __name__ == "__main__":
    unittest.main()
