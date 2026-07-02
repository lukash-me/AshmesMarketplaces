from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from app.proxy_supervisor import build_child_process_plans


def _write_json(path: Path, payload: dict) -> None:
    path.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")


def _write_supervisor_fixture(root: Path) -> tuple[Path, Path]:
    explicit_niches = root / "explicit_niches.json"
    _write_json(
        explicit_niches,
        {
            "niches": [
                {
                    "wbCategoryId": 8137,
                    "sourceCategory": "Женщинам",
                    "sourceSubcategory": "Платья и сарафаны",
                    "sourcePath": "Женщинам / Платья и сарафаны",
                    "searchQuery": "menu_v3_8137 платье женское",
                },
                {
                    "wbCategoryId": 8194,
                    "sourceCategory": "Обувь",
                    "sourceSubcategory": "Мужские кеды и кроссовки",
                    "sourcePath": "Обувь / Мужская / Кеды и кроссовки",
                    "searchQuery": "menu_redirect_subject_v2_8194 мужские кеды и кроссовки",
                },
                {
                    "wbCategoryId": 10012,
                    "sourceCategory": "Косметика",
                    "sourceSubcategory": "Органическая косметика",
                    "sourcePath": "Косметика / Органическая косметика",
                    "searchQuery": "menu_redirect_subject_v2_10012 органическая косметика",
                },
            ]
        },
    )
    config = root / "pipeline.json"
    _write_json(
        config,
        {
            "pipeline_name": "supervisor_test",
            "output_base_dir": str(root / "output"),
            "modes": {
                "batched_full_enrichment": {
                    "proxy_mapping_file": str(root / "proxy_mapping.json"),
                    "product": {
                        "config": "Parser/presets/home_goods_demo.env",
                        "env": {
                            "PARSER_EXPLICIT_NICHES_FILE": str(explicit_niches),
                            "PARSER_BATCH_QUEUE_URL": "http://api.local/api/v1/parser",
                        },
                    },
                }
            },
        },
    )
    proxy_mapping = root / "proxy_mapping.json"
    _write_json(
        proxy_mapping,
        {
            "defaultProxy": {"key": "local-proxy", "type": "direct"},
            "proxies": [
                {"key": "proxy-1", "type": "http-proxy", "baseUrl": "http://127.0.0.1:8001"},
                {"key": "proxy-2", "type": "http-proxy", "baseUrl": "http://127.0.0.1:8002"},
                {"key": "proxy-3", "type": "http-proxy", "baseUrl": "http://127.0.0.1:8003"},
                {"key": "proxy-4", "type": "http-proxy", "baseUrl": "http://127.0.0.1:8004"},
            ],
            "niches": [
                {"sourceCategory": "Женщинам", "sourceSubcategory": "Платья и сарафаны", "proxyKey": "proxy-1", "enabled": True},
                {"sourceCategory": "Обувь", "sourceSubcategory": "Мужские кеды и кроссовки", "proxyKey": "proxy-2", "enabled": True},
                {"sourceCategory": "Косметика", "sourceSubcategory": "Органическая косметика", "proxyKey": "proxy-3", "enabled": True},
                {"sourceCategory": "Игрушки", "sourceSubcategory": "Конструкторы", "proxyKey": "proxy-4", "enabled": False},
            ],
        },
    )
    return config, proxy_mapping


def test_supervisor_builds_one_child_process_per_enabled_proxy(tmp_path: Path) -> None:
    config, proxy_mapping = _write_supervisor_fixture(tmp_path)

    plans = build_child_process_plans(
        config_path=config,
        mode="batched_full_enrichment",
        proxy_mapping_path=proxy_mapping,
        parser_instance_id="parser-local-01",
        outbox_root_dir=tmp_path / "outbox",
        smoke_max_batches=3,
        python_executable="python",
    )

    assert [plan.proxy_key for plan in plans] == ["proxy-1", "proxy-2", "proxy-3"]
    assert [plan.source_subcategory for plan in plans] == [
        "Платья и сарафаны",
        "Мужские кеды и кроссовки",
        "Органическая косметика",
    ]
    assert {plan.environment["PARSER_OUTBOX_DIR"] for plan in plans} == {
        str(tmp_path / "outbox" / "proxy-1"),
        str(tmp_path / "outbox" / "proxy-2"),
        str(tmp_path / "outbox" / "proxy-3"),
    }
    assert all("--stage-to-db" not in plan.command for plan in plans)
    assert all("--skip-rank" in plan.command for plan in plans)
    assert all("--only-proxy" in plan.command for plan in plans)
    assert all("--smoke-max-batches" in plan.command for plan in plans)
    assert {plan.environment["PARSER_BATCH_QUEUE_URL"] for plan in plans} == {"http://api.local/api/v1/parser"}
    assert {plan.environment["PARSER_CYCLE_ID"] for plan in plans} == {plans[0].environment["PARSER_CYCLE_ID"]}
    assert {plan.environment["PARSER_CYCLE_KIND"] for plan in plans} == {"production"}


def test_supervisor_can_filter_to_one_proxy_without_starting_idle_proxies(tmp_path: Path) -> None:
    config, proxy_mapping = _write_supervisor_fixture(tmp_path)

    plans = build_child_process_plans(
        config_path=config,
        mode="batched_full_enrichment",
        proxy_mapping_path=proxy_mapping,
        parser_instance_id="parser-local-01",
        outbox_root_dir=tmp_path / "outbox",
        only_proxy="proxy-2",
        python_executable="python",
    )

    assert [plan.proxy_key for plan in plans] == ["proxy-2"]
    assert plans[0].environment["PARSER_CYCLE_KIND"] == "diagnostic"
    assert plans[0].source_subcategory == "Мужские кеды и кроссовки"


def test_supervisor_rejects_assignment_missing_from_explicit_niches(tmp_path: Path) -> None:
    config, proxy_mapping = _write_supervisor_fixture(tmp_path)
    mapping_payload = json.loads(proxy_mapping.read_text(encoding="utf-8"))
    mapping_payload["niches"].append(
        {"sourceCategory": "Дом", "sourceSubcategory": "Несуществующая ниша", "proxyKey": "proxy-4", "enabled": True}
    )
    _write_json(proxy_mapping, mapping_payload)

    with pytest.raises(ValueError, match="not found in explicit niches"):
        build_child_process_plans(
            config_path=config,
            mode="batched_full_enrichment",
            proxy_mapping_path=proxy_mapping,
            parser_instance_id="parser-local-01",
            outbox_root_dir=tmp_path / "outbox",
            python_executable="python",
        )
