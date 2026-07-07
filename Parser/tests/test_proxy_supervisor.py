from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from app.proxy_mapping import ProxyDefinition, ProxyMapping
from app.proxy_supervisor import ChildProcessPlan, _load_json, build_child_process_plans, run_child_processes


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
                    "parserSearchText": "Платья и сарафаны",
                },
                {
                    "wbCategoryId": 8194,
                    "sourceCategory": "Обувь",
                    "sourceSubcategory": "Мужские кеды и кроссовки",
                    "sourcePath": "Обувь / Мужская / Кеды и кроссовки",
                    "searchQuery": "menu_redirect_subject_v2_8194 мужские кеды и кроссовки",
                    "parserSearchText": "Мужские кеды и кроссовки",
                },
                {
                    "wbCategoryId": 10012,
                    "sourceCategory": "Косметика",
                    "sourceSubcategory": "Органическая косметика",
                    "sourcePath": "Косметика / Органическая косметика",
                    "searchQuery": "menu_redirect_subject_v2_10012 органическая косметика",
                    "parserSearchText": "Органическая косметика",
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
    assert all("--skip-rank" not in plan.command for plan in plans)
    assert all("--only-proxy" in plan.command for plan in plans)
    assert all("--smoke-max-batches" in plan.command for plan in plans)
    assert {plan.environment["PARSER_BATCH_QUEUE_URL"] for plan in plans} == {"http://api.local/api/v1/parser"}
    assert {plan.environment["PARSER_CYCLE_ID"] for plan in plans} == {plans[0].environment["PARSER_CYCLE_ID"]}
    assert {plan.environment["PARSER_CYCLE_KIND"] for plan in plans} == {"production"}
    assert {plan.environment["PARSER_SESSION_SCOPE"] for plan in plans} == {
        f"{plans[0].environment['PARSER_CYCLE_ID']}/proxy-1",
        f"{plans[0].environment['PARSER_CYCLE_ID']}/proxy-2",
        f"{plans[0].environment['PARSER_CYCLE_ID']}/proxy-3",
    }
    assert {plan.environment["PARSER_RUNTIME_RANK_CONTEXT_ID"] for plan in plans} == {
        "proxy-1_Платья_и_сарафаны",
        "proxy-2_Мужские_кеды_и_кроссовки",
        "proxy-3_Органическая_косметика",
    }
    assert {plan.environment["PARSER_BROWSER_PROFILE_DIR"] for plan in plans} == {
        str(tmp_path / "outbox" / "_runtime" / "browser_profiles")
    }
    assert {plan.environment["PARSER_BROWSER_SESSION_CACHE_DIR"] for plan in plans} == {
        str(tmp_path / "outbox" / "_runtime" / "browser_sessions")
    }
    rank_config_paths = {plan.environment["PARSER_RUNTIME_RANK_CONFIG_FILE"] for plan in plans}
    assert len(rank_config_paths) == 1
    rank_config = json.loads(Path(next(iter(rank_config_paths))).read_text(encoding="utf-8"))
    assert rank_config["defaults"]["top_n"] == 1000
    assert rank_config["defaults"]["page_size"] == 100
    assert [context["query"] for context in rank_config["contexts"]] == [
        "Платья и сарафаны",
        "Мужские кеды и кроссовки",
        "Органическая косметика",
    ]


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


def test_supervisor_uses_backend_runtime_assignments(monkeypatch, tmp_path: Path) -> None:
    config, _ = _write_supervisor_fixture(tmp_path)
    runtime_payload = {
        "defaultProxy": {"key": "local-proxy", "type": "direct"},
        "proxies": [
            {
                "key": "proxy-1",
                "type": "http-proxy",
                "baseUrl": "http://10.0.0.1:8080",
                "credentials": {"login": "user", "password": "secret"},
            },
            {
                "key": "proxy-2",
                "type": "http-proxy",
                "baseUrl": "http://10.0.0.2:8080",
                "credentials": {"login": "user", "password": "secret"},
            },
        ],
        "niches": [
            {
                "wbCategoryId": 1,
                "sourceCategory": "Женщинам",
                "sourceSubcategory": "Платья и сарафаны",
                "sourcePath": "Женщинам / Одежда / Платья и сарафаны",
                "searchQuery": "menu_v3_1 платья",
                "parserSearchText": "Платья и сарафаны",
                "proxyKey": "proxy-1",
                "enabled": True,
            },
            {
                "wbCategoryId": 2,
                "sourceCategory": "Обувь",
                "sourceSubcategory": "Кеды и кроссовки",
                "sourcePath": "Обувь / Мужская / Кеды и кроссовки",
                "searchQuery": "menu_v3_2 кеды",
                "parserSearchText": "Кеды и кроссовки",
                "proxyKey": "proxy-2",
                "enabled": True,
            },
        ],
    }

    requested_instance_ids: list[str | None] = []

    def fake_runtime_url(*, parser_instance_id: str | None = None):
        requested_instance_ids.append(parser_instance_id)
        return "http://api/parser/runtime"

    monkeypatch.setattr("app.proxy_supervisor.runtime_proxy_assignments_url", fake_runtime_url)
    monkeypatch.setattr(
        "app.proxy_supervisor.load_runtime_proxy_mapping",
        lambda url: ProxyMapping.from_dict(runtime_payload),
    )

    plans = build_child_process_plans(
        config_path=config,
        mode="batched_full_enrichment",
        parser_instance_id="parser-local-01",
        outbox_root_dir=tmp_path / "outbox",
        smoke_max_batches=3,
        python_executable="python",
        parser_cycle_id="cycle-runtime",
    )

    assert [plan.proxy_key for plan in plans] == ["proxy-1", "proxy-2"]
    assert requested_instance_ids == ["parser-local-01"]
    runtime_mapping_path = Path(plans[0].environment["PARSER_PROXY_MAPPING_FILE"])
    runtime_niches_path = Path(plans[0].environment["PARSER_EXPLICIT_NICHES_FILE"])
    assert runtime_mapping_path.exists()
    assert runtime_niches_path.exists()
    mapping_payload = json.loads(runtime_mapping_path.read_text(encoding="utf-8"))
    niches_payload = json.loads(runtime_niches_path.read_text(encoding="utf-8"))
    assert mapping_payload["proxies"][0]["credentials"]["password"] == "secret"
    assert [item["sourcePath"] for item in niches_payload["niches"]] == [
        "Женщинам / Одежда / Платья и сарафаны",
        "Обувь / Мужская / Кеды и кроссовки",
    ]
    assert [item["parserSearchText"] for item in niches_payload["niches"]] == [
        "Платья и сарафаны",
        "Кеды и кроссовки",
    ]
    assert mapping_payload["niches"][0]["parserSearchText"] == "Платья и сарафаны"
    rank_config_path = Path(plans[0].environment["PARSER_RUNTIME_RANK_CONFIG_FILE"])
    rank_config = json.loads(rank_config_path.read_text(encoding="utf-8"))
    assert [context["query"] for context in rank_config["contexts"]] == [
        "Платья и сарафаны",
        "Кеды и кроссовки",
    ]
    assert plans[0].environment["PARSER_RUNTIME_RANK_CONTEXT_ID"] == "proxy-1_Платья_и_сарафаны"


def test_supervisor_launch_context_is_source_of_truth_for_service_launch(tmp_path: Path) -> None:
    config, _ = _write_supervisor_fixture(tmp_path)
    proxy_guid = "aef8f7a4-f1d2-4144-823d-9889b35b00df"
    launch_context = tmp_path / "launch_context.json"
    _write_json(
        launch_context,
        {
            "launchId": "launch-1",
            "parserCycleId": "parser-cycle-guid",
            "parserInstanceId": "parser-local-01",
            "launchMode": "check_proxy",
            "defaultProxy": {"key": "local-proxy", "type": "direct"},
            "proxies": [
                {
                    "key": proxy_guid,
                    "type": "http-proxy",
                    "baseUrl": "http://10.0.0.1:8080",
                    "credentials": {"login": "user", "password": "secret"},
                }
            ],
            "niches": [
                {
                    "wbCategoryId": 10012,
                    "sourceCategory": "Красота",
                    "sourceSubcategory": "Органическая косметика",
                    "sourcePath": "Красота / Органическая косметика",
                    "searchQuery": "menu_redirect_subject_v2_10012 органическая косметика",
                    "parserSearchText": "Органическая косметика",
                    "proxyKey": proxy_guid,
                    "enabled": True,
                }
            ],
        },
    )

    plans = build_child_process_plans(
        config_path=config,
        mode="batched_full_enrichment",
        parser_instance_id="parser-local-01",
        outbox_root_dir=tmp_path / "outbox",
        smoke_max_batches=3,
        only_proxy=proxy_guid,
        python_executable="python",
        parser_cycle_id="parser-cycle-guid",
        launch_context_path=launch_context,
    )

    assert [plan.proxy_key for plan in plans] == [proxy_guid]
    assert plans[0].source_subcategory == "Органическая косметика"
    assert plans[0].environment["PARSER_LAUNCH_CONTEXT_FILE"] == str(launch_context.resolve())
    runtime_mapping = json.loads(Path(plans[0].environment["PARSER_PROXY_MAPPING_FILE"]).read_text(encoding="utf-8"))
    assert [item["key"] for item in runtime_mapping["proxies"]] == [proxy_guid]
    assert [item["proxyKey"] for item in runtime_mapping["niches"]] == [proxy_guid]


def test_supervisor_load_json_accepts_utf8_bom(tmp_path: Path) -> None:
    payload = {"parserCycleId": "parser-cycle-with-bom"}
    path = tmp_path / "launch_context.json"
    path.write_bytes(b"\xef\xbb\xbf" + json.dumps(payload).encode("utf-8"))

    assert _load_json(path) == payload


def test_supervisor_persists_runner_and_child_logs(tmp_path: Path) -> None:
    plan = ChildProcessPlan(
        proxy_key="proxy-1",
        source_category="Женщинам",
        source_subcategory="Платья и сарафаны",
        command=[
            sys.executable,
            "-c",
            "import sys; print('child stdout'); print('child stderr', file=sys.stderr)",
        ],
        environment={
            "PARSER_OUTBOX_DIR": str(tmp_path / "outbox" / "proxy-1"),
            "PARSER_CYCLE_ID": "cycle-test",
        },
        proxy=ProxyDefinition(key="proxy-1"),
    )

    results = run_child_processes([plan])

    assert len(results) == 1
    assert results[0].exit_code == 0
    assert "child stdout" in results[0].stdout
    assert "child stderr" in results[0].stderr

    log_dir = tmp_path / "outbox" / "_runtime" / "logs" / "cycle-test"
    runner_log = log_dir / "runner.log"
    stdout_log = log_dir / "proxy-1" / "stdout.log"
    stderr_log = log_dir / "proxy-1" / "stderr.log"

    assert runner_log.exists()
    assert stdout_log.read_text(encoding="utf-8").strip() == "child stdout"
    assert stderr_log.read_text(encoding="utf-8").strip() == "child stderr"
    assert "starting proxy=proxy-1" in runner_log.read_text(encoding="utf-8")
    assert "finished proxy=proxy-1" in runner_log.read_text(encoding="utf-8")
