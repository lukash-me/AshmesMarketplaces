from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from app.proxy_mapping import ProxyMapping, ProxyScheduler
from app.proxy_transport import http_proxy_url_from_definition, masked_proxy_url


class ProxyMappingTests(unittest.TestCase):
    def test_resolves_configured_proxy_for_niche(self) -> None:
        mapping = ProxyMapping.from_dict(
            {
                "defaultProxy": {"key": "local-proxy", "type": "direct"},
                "proxies": [{"key": "bath-proxy", "type": "http-proxy", "baseUrl": "http://proxy.local"}],
                "niches": [
                    {
                        "sourceCategory": "Товары для дома",
                        "sourceSubcategory": "Коврики для ванной",
                        "proxyKey": "bath-proxy",
                        "enabled": True,
                    }
                ],
            }
        )

        resolved = mapping.resolve("Товары для дома", "Коврики для ванной")

        self.assertEqual(resolved.proxy.key, "bath-proxy")
        self.assertEqual(resolved.proxy.type, "http-proxy")
        self.assertTrue(resolved.enabled)

    def test_resolves_distinct_proxies_for_current_production_wb_leaf_niches(self) -> None:
        mapping = ProxyMapping.from_dict(
            {
                "defaultProxy": {"key": "local-proxy", "type": "direct"},
                "proxies": [
                    {"key": "proxy-1", "type": "http-proxy", "baseUrl": "http://proxy-1.local"},
                    {"key": "proxy-2", "type": "http-proxy", "baseUrl": "http://proxy-2.local"},
                    {"key": "proxy-3", "type": "http-proxy", "baseUrl": "http://proxy-3.local"},
                    {"key": "proxy-4", "type": "http-proxy", "baseUrl": "http://proxy-4.local"},
                    {"key": "proxy-5", "type": "http-proxy", "baseUrl": "http://proxy-5.local"},
                ],
                "niches": [
                    {
                        "sourceCategory": "Женщинам",
                        "sourceSubcategory": "Платья и сарафаны",
                        "proxyKey": "proxy-1",
                        "enabled": True,
                    },
                    {
                        "sourceCategory": "Обувь",
                        "sourceSubcategory": "Кеды и кроссовки",
                        "proxyKey": "proxy-2",
                        "enabled": True,
                    },
                    {
                        "sourceCategory": "Красота",
                        "sourceSubcategory": "Органическая косметика",
                        "proxyKey": "proxy-3",
                        "enabled": True,
                    },
                ],
            }
        )

        self.assertEqual(mapping.resolve("Женщинам", "Платья и сарафаны").proxy.key, "proxy-1")
        self.assertEqual(mapping.resolve("Обувь", "Кеды и кроссовки").proxy.key, "proxy-2")
        self.assertEqual(mapping.resolve("Красота", "Органическая косметика").proxy.key, "proxy-3")
        self.assertEqual(mapping.resolve("Товары для дома", "Коврики для ванной").proxy.key, "local-proxy")

    def test_reloading_mapping_file_uses_changed_proxy(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "proxy_mapping.json"
            path.write_text(
                json.dumps(
                    {
                        "defaultProxy": {"key": "proxy-a", "type": "direct"},
                        "niches": [
                            {
                                "sourceCategory": "Товары для дома",
                                "sourceSubcategory": "Светильники бра",
                                "proxyKey": "proxy-a",
                            }
                        ],
                    },
                    ensure_ascii=False,
                ),
                encoding="utf-8",
            )
            self.assertEqual(
                ProxyMapping.load(path).resolve("Товары для дома", "Светильники бра").proxy.key,
                "proxy-a",
            )

            path.write_text(
                json.dumps(
                    {
                        "defaultProxy": {"key": "proxy-b", "type": "direct"},
                        "niches": [
                            {
                                "sourceCategory": "Товары для дома",
                                "sourceSubcategory": "Светильники бра",
                                "proxyKey": "proxy-b",
                            }
                        ],
                    },
                    ensure_ascii=False,
                ),
                encoding="utf-8",
            )

            self.assertEqual(
                ProxyMapping.load(path).resolve("Товары для дома", "Светильники бра").proxy.key,
                "proxy-b",
            )

    def test_load_with_local_override_prefers_local_mapping(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "proxy_mapping.json"
            local_path = Path(temp) / "proxy_mapping.local.json"
            path.write_text(json.dumps({"defaultProxy": {"key": "fallback", "type": "direct"}}), encoding="utf-8")
            local_path.write_text(json.dumps({"defaultProxy": {"key": "local", "type": "direct"}}), encoding="utf-8")

            mapping = ProxyMapping.load_with_local_override(path)

            self.assertEqual(mapping.default_proxy.key, "local")

    def test_http_proxy_url_uses_credentials_and_masking_hides_password(self) -> None:
        mapping = ProxyMapping.from_dict(
            {
                "defaultProxy": {
                    "key": "proxy-1",
                    "type": "http-proxy",
                    "baseUrl": "http://10.0.0.10:8000",
                    "socks5Url": "socks5://10.0.0.10:9000",
                    "credentials": {"login": "user@example.com", "password": "secret"},
                }
            }
        )

        proxy = mapping.default_proxy
        proxy_url = http_proxy_url_from_definition(proxy)

        self.assertEqual(proxy.socks5_url, "socks5://10.0.0.10:9000")
        self.assertEqual(proxy_url, "http://user%40example.com:secret@10.0.0.10:8000")
        self.assertNotIn("secret", masked_proxy_url(proxy_url))
        self.assertIn("***:***@", masked_proxy_url(proxy_url))

    def test_scheduler_isolates_busy_and_cooldown_proxy(self) -> None:
        now = {"value": 100.0}
        scheduler = ProxyScheduler(now=lambda: now["value"])
        proxy_mapping = ProxyMapping.from_dict(
            {
                "defaultProxy": {"key": "proxy-a", "type": "direct", "cooldownSeconds": 30},
                "proxies": [{"key": "proxy-b", "type": "direct"}],
            }
        )

        proxy_a = proxy_mapping.proxies["proxy-a"]
        scheduler.mark_started(proxy_a.key)

        self.assertEqual(scheduler.status("proxy-a"), "busy")
        self.assertEqual(scheduler.status("proxy-b"), "ready")

        scheduler.mark_failed(proxy_a)
        self.assertEqual(scheduler.status("proxy-a"), "cooldown")
        self.assertEqual(scheduler.status("proxy-b"), "ready")

        now["value"] = 131.0
        self.assertEqual(scheduler.status("proxy-a"), "ready")


if __name__ == "__main__":
    unittest.main()
