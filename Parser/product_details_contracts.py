from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from typing import Any

from basket_selector import calc_numb_basket


PRODUCT_DETAILS_SCHEMA_VERSION = 1
PRODUCT_DETAILS_FETCH_SCHEMA_VERSION = 1
SOURCE_REQUEST_FAMILY = "basket_card_info"


@dataclass(frozen=True)
class ProductDetailInput:
    wb_product_id: str
    wb_root_id: str | None = None
    source_category: str | None = None
    source_subcategory: str | None = None
    source_query: str | None = None
    source_region_dest: str | None = None


def build_card_info_url(wb_product_id: str) -> str:
    product_id = int(wb_product_id)
    vol = product_id // 100000
    part = product_id // 1000
    basket = calc_numb_basket(vol)
    return f"https://basket-{basket}.wbbasket.ru/vol{vol}/part{part}/{product_id}/info/ru/card.json"


def request_fingerprint(*, endpoint: str) -> str:
    normalized = json.dumps(
        {"endpoint": endpoint},
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    )
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def map_product_detail_payload(
    *,
    payload: dict[str, Any],
    product_input: ProductDetailInput,
    parser_run_id: str,
    marketplace: str,
    parsed_at_utc: str,
    source_endpoint: str,
    request_fingerprint_value: str,
    input_products_parser_run_id: str | None,
    input_products_jsonl: str,
    status: str,
) -> dict[str, Any]:
    characteristics = _extract_characteristics(payload)
    grouped_options = _extract_grouped_options(payload)
    media_count = _extract_media_count(payload)

    return {
        "schema_version": PRODUCT_DETAILS_SCHEMA_VERSION,
        "parser_run_id": parser_run_id,
        "marketplace": marketplace,
        "parsed_at_utc": parsed_at_utc,
        "input_products_parser_run_id": input_products_parser_run_id,
        "input_products_jsonl": input_products_jsonl,
        "source_request_family": SOURCE_REQUEST_FAMILY,
        "source_endpoint": source_endpoint,
        "request_fingerprint": request_fingerprint_value,
        "source_category": product_input.source_category,
        "source_subcategory": product_input.source_subcategory,
        "source_query": product_input.source_query,
        "source_region_dest": product_input.source_region_dest,
        "wb_product_id": product_input.wb_product_id,
        "wb_root_id": _string_or_none(payload.get("imt_id")) or product_input.wb_root_id,
        "description": _string_or_none(payload.get("description")),
        "characteristics": characteristics,
        "grouped_options": grouped_options,
        "media_count": media_count,
        "status": status,
        "raw_detail_fields": sorted(str(key) for key in payload.keys()),
    }


def map_fetch_result(
    *,
    product_input: ProductDetailInput,
    parser_run_id: str,
    parsed_at_utc: str,
    source_endpoint: str,
    request_fingerprint_value: str,
    status: str,
    attempts: int,
    retries: int,
    http_status: int | None,
    message: str | None,
    is_transient: bool,
) -> dict[str, Any]:
    return {
        "schema_version": PRODUCT_DETAILS_FETCH_SCHEMA_VERSION,
        "parser_run_id": parser_run_id,
        "parsed_at_utc": parsed_at_utc,
        "source_request_family": SOURCE_REQUEST_FAMILY,
        "source_endpoint": source_endpoint,
        "request_fingerprint": request_fingerprint_value,
        "wb_product_id": product_input.wb_product_id,
        "wb_root_id": product_input.wb_root_id,
        "source_category": product_input.source_category,
        "source_subcategory": product_input.source_subcategory,
        "source_query": product_input.source_query,
        "source_region_dest": product_input.source_region_dest,
        "status": status,
        "attempts": attempts,
        "retries": retries,
        "http_status": http_status,
        "message": message,
        "is_transient": is_transient,
    }


def _extract_characteristics(payload: dict[str, Any]) -> list[dict[str, Any]]:
    for key in ("options", "characteristics", "chars"):
        value = payload.get(key)
        if isinstance(value, list):
            return [item for item in value if isinstance(item, dict)]
    return []


def _extract_grouped_options(payload: dict[str, Any]) -> list[dict[str, Any]]:
    for key in ("grouped_options", "groupedOptions", "grouped_options_ext"):
        value = payload.get(key)
        if isinstance(value, list):
            return [item for item in value if isinstance(item, dict)]
    return []


def _extract_media_count(payload: dict[str, Any]) -> int | None:
    for key in ("media_count", "mediaCount", "pics"):
        value = payload.get(key)
        if value is not None:
            try:
                return int(value)
            except (TypeError, ValueError):
                return None
    return None


def _string_or_none(value: Any) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None
