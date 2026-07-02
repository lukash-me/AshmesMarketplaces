from __future__ import annotations

import json
from pathlib import Path


PARSER_DIR = Path(__file__).resolve().parents[1]


def test_production_explicit_niches_are_valid_utf8_russian() -> None:
    path = PARSER_DIR / "presets" / "production" / "wb_explicit_niches.prod.json"
    payload = json.loads(path.read_text(encoding="utf-8"))

    niches = payload["niches"]
    assert [niche["sourceSubcategory"] for niche in niches] == [
        "Платья и сарафаны",
        "Мужские кеды и кроссовки",
        "Органическая косметика",
    ]
    assert [niche["sourcePath"] for niche in niches] == [
        "Женщинам / Одежда / Платья и сарафаны",
        "Обувь / Мужская / Кеды и кроссовки",
        "Красота / Органическая косметика",
    ]
    assert "Р" not in json.dumps(payload, ensure_ascii=False)


def test_smoke_reviews_scope_matches_production_niches() -> None:
    production_path = PARSER_DIR / "presets" / "production" / "wb_explicit_niches.prod.json"
    smoke_path = PARSER_DIR.parent / "parser-smoke-3x3.config.json"
    production_payload = json.loads(production_path.read_text(encoding="utf-8"))
    smoke_payload = json.loads(smoke_path.read_text(encoding="utf-8"))

    expected = [niche["sourceSubcategory"] for niche in production_payload["niches"]]
    actual = smoke_payload["modes"]["batched_full_enrichment"]["reviews"]["source_subcategories"]
    assert actual == expected


def test_production_parser_limits_match_stable_proxy_baseline() -> None:
    path = PARSER_DIR / "presets" / "production" / "market_refresh_selected_niches_batched.prod.json"
    payload = json.loads(path.read_text(encoding="utf-8"))
    mode = payload["modes"]["batched_full_enrichment"]
    env = mode["product"]["env"]

    assert mode["batching"]["batch_size"] == 100
    assert env["PARSER_CATALOG_REQUEST_GROUP_SIZE"] == "10"
    assert "PARSER_BATCH_SIZE" not in env
    assert env["PARSER_MAX_CONCURRENT"] == "2"
    assert env["PARSER_PROXY_MIN_REQUEST_GAP_SECONDS"] == "0.3"
    assert env["PARSER_PROXY_REQUEST_JITTER_SECONDS"] == "0.6"
    assert env["PARSER_FILTER_MIN_REQUEST_GAP_SECONDS"] == "0.3"
    assert env["PARSER_FILTER_JITTER_SECONDS"] == "0.6"
    assert env["PARSER_BATCH_DELAY_MIN_SECONDS"] == "2"
    assert env["PARSER_BATCH_DELAY_MAX_SECONDS"] == "5"
    assert env["PARSER_PRICE_SPLIT_TO_CATALOG_DELAY_SECONDS"] == "10"
    assert env["PARSER_PRICE_SPLIT_TO_CATALOG_JITTER_SECONDS"] == "0"
