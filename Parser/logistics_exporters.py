from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Iterable


LOGISTICS_SNAPSHOT_FIELDS = [
    "schema_version",
    "parser_run_id",
    "marketplace",
    "observed_at_utc",
    "source_request_family",
    "source_endpoint",
    "request_fingerprint",
    "source_region_dest",
    "delivery_profile_key",
    "delivery_destination_name",
    "delivery_profile_version",
    "delivery_destination_city",
    "delivery_destination_label",
    "delivery_destination_address",
    "delivery_destination_latitude",
    "delivery_destination_longitude",
    "source_category",
    "source_subcategory",
    "source_query",
    "wb_product_id",
    "wb_root_id",
    "seller_id",
    "seller_name",
    "total_quantity_observed",
    "quantity_is_capped",
    "quantity_cap_observed",
    "quantity_semantics",
    "product_wh_raw",
    "product_time1_raw",
    "product_time2_raw",
    "product_dtype_raw",
    "product_dist_raw",
    "visible_delivery_status",
    "visible_delivery_label",
    "visible_delivery_date",
    "visible_delivery_source",
    "visible_delivery_observed_at_utc",
    "visible_delivery_raw_payload",
    "raw_observed_fields",
]


WAREHOUSE_AVAILABILITY_FIELDS = [
    "schema_version",
    "parser_run_id",
    "marketplace",
    "observed_at_utc",
    "source_request_family",
    "source_endpoint",
    "request_fingerprint",
    "source_region_dest",
    "delivery_profile_key",
    "delivery_destination_name",
    "delivery_profile_version",
    "delivery_destination_city",
    "delivery_destination_label",
    "delivery_destination_address",
    "delivery_destination_latitude",
    "delivery_destination_longitude",
    "source_category",
    "source_subcategory",
    "source_query",
    "wb_product_id",
    "wb_root_id",
    "seller_id",
    "seller_name",
    "option_id",
    "size_name",
    "size_orig_name",
    "size_rank",
    "warehouse_id_on_mp",
    "quantity_observed",
    "quantity_is_capped",
    "quantity_cap_observed",
    "quantity_semantics",
    "stock_priority_raw",
    "stock_time1_raw",
    "stock_time2_raw",
    "stock_dtype_raw",
    "stock_dist_raw",
    "price_basic",
    "price_product",
    "price_logistics_raw",
    "price_return_raw",
    "raw_stock",
    "raw_size_observed_fields",
]


def append_jsonl(path: Path, row: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as file:
        file.write(json.dumps(row, ensure_ascii=False, default=str) + "\n")


def iter_jsonl(path: Path) -> Iterable[dict[str, Any]]:
    if not path.exists():
        return

    with path.open(encoding="utf-8") as file:
        for line in file:
            if not line.strip():
                continue
            yield json.loads(line)


def validate_fields(row: dict[str, Any], field_names: list[str], row_name: str) -> None:
    missing = [field for field in field_names if field not in row]
    if missing:
        raise ValueError(f"Canonical {row_name} row is missing fields: {missing}")
