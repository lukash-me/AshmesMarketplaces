from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from typing import Any


LOGISTICS_SCHEMA_VERSION = 1
SOURCE_REQUEST_FAMILY = "card_detail_v4"
WB_CARD_DETAIL_ENDPOINT = "https://card.wb.ru/cards/v4/detail"
QUANTITY_SEMANTICS = "unknown_or_capped"


@dataclass(frozen=True)
class LogisticsProductInput:
    wb_product_id: str
    wb_root_id: str | None = None
    seller_id: str | int | None = None
    seller_name: str | None = None
    source_category: str | None = None
    source_subcategory: str | None = None
    source_query: str | None = None
    source_region_dest: str | None = None


def build_card_detail_params(*, wb_product_id: str, dest: str) -> dict[str, str]:
    return {
        "appType": "1",
        "curr": "rub",
        "dest": dest,
        "spp": "30",
        "nm": str(wb_product_id),
    }


def request_fingerprint(*, endpoint: str, params: dict[str, Any]) -> str:
    payload = {
        "endpoint": endpoint,
        "params": {str(key): str(value) for key, value in sorted(params.items())},
    }
    normalized = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def products_from_payload(payload: dict[str, Any]) -> list[dict[str, Any]]:
    products = payload.get("products")
    if isinstance(products, list):
        return [item for item in products if isinstance(item, dict)]

    data = payload.get("data")
    if isinstance(data, dict) and isinstance(data.get("products"), list):
        return [item for item in data["products"] if isinstance(item, dict)]

    return []


def map_card_detail_payload(
    *,
    payload: dict[str, Any],
    product_input: LogisticsProductInput,
    parser_run_id: str,
    marketplace: str,
    observed_at_utc: str,
    source_endpoint: str,
    request_fingerprint_value: str,
    source_region_dest: str,
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    snapshots: list[dict[str, Any]] = []
    warehouse_rows: list[dict[str, Any]] = []

    for product in products_from_payload(payload):
        product_id = product.get("id")
        if product_id is None or str(product_id) != product_input.wb_product_id:
            continue

        common = {
            "schema_version": LOGISTICS_SCHEMA_VERSION,
            "parser_run_id": parser_run_id,
            "marketplace": marketplace,
            "observed_at_utc": observed_at_utc,
            "source_request_family": SOURCE_REQUEST_FAMILY,
            "source_endpoint": source_endpoint,
            "request_fingerprint": request_fingerprint_value,
            "source_region_dest": source_region_dest,
            "source_category": product_input.source_category,
            "source_subcategory": product_input.source_subcategory,
            "source_query": product_input.source_query,
            "wb_product_id": str(product_id),
            "wb_root_id": _string_or_none(product.get("root")) or product_input.wb_root_id,
            "seller_id": _string_or_none(product.get("supplierId")) or _string_or_none(product_input.seller_id),
            "seller_name": _string_or_none(product.get("supplier")) or product_input.seller_name,
        }

        snapshots.append({
            **common,
            "total_quantity_observed": _int_or_none(product.get("totalQuantity")),
            "quantity_is_capped": None,
            "quantity_cap_observed": None,
            "quantity_semantics": QUANTITY_SEMANTICS,
            "product_wh_raw": _int_or_none(product.get("wh")),
            "product_time1_raw": _int_or_none(product.get("time1")),
            "product_time2_raw": _int_or_none(product.get("time2")),
            "product_dtype_raw": _int_or_none(product.get("dtype")),
            "product_dist_raw": _int_or_none(product.get("dist")),
            "raw_observed_fields": _observed_fields(product),
        })

        for size in _dicts(product.get("sizes")):
            price = size.get("price") if isinstance(size.get("price"), dict) else {}
            for stock in _dicts(size.get("stocks")):
                warehouse_rows.append({
                    **common,
                    "option_id": _int_or_none(size.get("optionId")),
                    "size_name": _string_or_none(size.get("name")),
                    "size_orig_name": _string_or_none(size.get("origName")),
                    "size_rank": _int_or_none(size.get("rank")),
                    "warehouse_id_on_mp": _string_or_none(stock.get("wh")),
                    "quantity_observed": _int_or_none(stock.get("qty")),
                    "quantity_is_capped": None,
                    "quantity_cap_observed": None,
                    "quantity_semantics": QUANTITY_SEMANTICS,
                    "stock_priority_raw": _int_or_none(stock.get("priority")),
                    "stock_time1_raw": _int_or_none(stock.get("time1")),
                    "stock_time2_raw": _int_or_none(stock.get("time2")),
                    "stock_dtype_raw": _int_or_none(stock.get("dtype")),
                    "stock_dist_raw": _int_or_none(stock.get("dist")),
                    "price_basic": _money_or_none(price.get("basic")),
                    "price_product": _money_or_none(price.get("product")),
                    "price_logistics_raw": _money_or_none(price.get("logistics")),
                    "price_return_raw": _money_or_none(price.get("return")),
                    "raw_stock": stock,
                    "raw_size_observed_fields": _observed_fields(size),
                })

    return snapshots, warehouse_rows


def _dicts(value: Any) -> list[dict[str, Any]]:
    return [item for item in value if isinstance(item, dict)] if isinstance(value, list) else []


def _observed_fields(value: dict[str, Any]) -> list[str]:
    return sorted(str(key) for key in value.keys())


def _string_or_none(value: Any) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def _int_or_none(value: Any) -> int | None:
    if value is None:
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _money_or_none(value: Any) -> float | None:
    if value is None:
        return None
    try:
        return float(value) / 100
    except (TypeError, ValueError):
        return None
