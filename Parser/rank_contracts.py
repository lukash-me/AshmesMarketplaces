from __future__ import annotations

import hashlib
import json
from typing import Any

from rank_config import RankContextConfig


RANK_SCHEMA_VERSION = 1
WB_SEARCH_ENDPOINT = "https://search.wb.ru/exactmatch/ru/common/v18/search"


def build_search_params(
    *,
    context: RankContextConfig,
    page: int,
    dest: str,
) -> dict[str, str]:
    params: dict[str, str] = {
        "ab_testing": "false",
        "appType": "1",
        "autoselectFilters": "false",
        "curr": "rub",
        "dest": dest,
        "inheritFilters": "false",
        "lang": "ru",
        "locale": "ru",
        "page": str(page),
        "query": context.query,
        "resultset": "catalog",
        "sort": context.sort,
        "spp": "30",
        "suppressSpellcheck": "false",
    }

    for key, value in sorted(context.filters.items()):
        if value is None:
            continue
        params[str(key)] = str(value)

    return params


def request_fingerprint(*, endpoint: str, params: dict[str, Any]) -> str:
    payload = {
        "endpoint": endpoint,
        "params": {str(key): str(value) for key, value in sorted(params.items())},
    }
    normalized = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def response_total(payload: dict[str, Any]) -> int | None:
    candidates = [
        payload.get("total"),
        payload.get("totalCount"),
        payload.get("data", {}).get("total") if isinstance(payload.get("data"), dict) else None,
        payload.get("metadata", {}).get("total") if isinstance(payload.get("metadata"), dict) else None,
    ]
    for value in candidates:
        if value is None:
            continue
        try:
            return int(value)
        except (TypeError, ValueError):
            continue
    return None


def products_from_payload(payload: dict[str, Any]) -> list[dict[str, Any]]:
    products = payload.get("products")
    if isinstance(products, list):
        return [item for item in products if isinstance(item, dict)]

    data = payload.get("data")
    if isinstance(data, dict) and isinstance(data.get("products"), list):
        return [item for item in data["products"] if isinstance(item, dict)]

    return []


def build_rank_rows(
    *,
    products: list[dict[str, Any]],
    parser_run_id: str,
    observed_at_utc: str,
    marketplace: str,
    context: RankContextConfig,
    source_region_dest: str,
    request_fingerprint_value: str,
    page: int,
    page_size: int,
    response_total_value: int | None,
    remaining_slots: int,
) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for index, product in enumerate(products, start=1):
        if len(rows) >= remaining_slots:
            break

        product_id = product.get("id")
        if product_id is None:
            continue

        rows.append(
            {
                "schema_version": RANK_SCHEMA_VERSION,
                "parser_run_id": parser_run_id,
                "observed_at_utc": observed_at_utc,
                "marketplace": marketplace,
                "rank_context_id": context.id,
                "rank_context_type": context.type,
                "source_category": context.source_category,
                "source_subcategory": context.source_subcategory,
                "query": context.query,
                "source_region_dest": source_region_dest,
                "sort": context.sort,
                "filters": context.filters,
                "request_fingerprint": request_fingerprint_value,
                "page": page,
                "position_on_page": index,
                "absolute_position": ((page - 1) * page_size) + index,
                "wb_product_id": str(product_id),
                "wb_root_id": str(product["root"]) if product.get("root") is not None else None,
                "response_total": response_total_value,
                "fetch_status": "succeeded",
            }
        )

    return rows


def build_page_fetch_event(
    *,
    parser_run_id: str,
    observed_at_utc: str,
    context: RankContextConfig,
    source_region_dest: str,
    request_fingerprint_value: str,
    page: int,
    status: str,
    product_count: int,
    response_total_value: int | None,
    retry_count: int = 0,
    http_status: int | None = None,
    wb_code: str | int | None = None,
    message: str | None = None,
) -> dict[str, Any]:
    return {
        "schema_version": RANK_SCHEMA_VERSION,
        "parser_run_id": parser_run_id,
        "observed_at_utc": observed_at_utc,
        "rank_context_id": context.id,
        "rank_context_type": context.type,
        "source_category": context.source_category,
        "source_subcategory": context.source_subcategory,
        "query": context.query,
        "source_region_dest": source_region_dest,
        "sort": context.sort,
        "filters": context.filters,
        "request_fingerprint": request_fingerprint_value,
        "page": page,
        "status": status,
        "product_count": product_count,
        "response_total": response_total_value,
        "retry_count": retry_count,
        "http_status": http_status,
        "wb_code": str(wb_code) if wb_code is not None else None,
        "message": message,
    }
