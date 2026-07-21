from __future__ import annotations

import csv
import json
from pathlib import Path
from typing import Any

from models import Item


CANONICAL_PRODUCT_FIELDS = [
    "schema_version",
    "marketplace",
    "parser_run_id",
    "parsed_at_utc",
    "source_category",
    "source_subcategory",
    "source_query",
    "source_region_dest",
    "wb_product_id",
    "sku_product",
    "name",
    "entity",
    "brand_id",
    "brand_name",
    "seller_id",
    "seller_name",
    "price_regular",
    "price_discounted",
    "price_wb_wallet",
    "discount_percent",
    "total_quantity",
    "rating_rounded",
    "review_rating",
    "feedback_count",
    "feedback_count_source",
    "image_urls",
    "image_count",
    "wb_root_id",
    "subject_parent_id",
    "subject_id",
    "raw_observed_fields",
]


def _split_image_urls(value: str | None) -> list[str]:
    if not value:
        return []

    return [
        part.strip().lstrip("\u200b")
        for part in value.split(";")
        if part.strip().lstrip("\u200b")
    ]


def _observed_fields(item: Item) -> list[str]:
    fields = getattr(item, "model_fields_set", set())
    if fields:
        return sorted(str(field) for field in fields)

    return sorted(
        key
        for key, value in item.model_dump(exclude_none=False).items()
        if value is not None
    )


def product_to_canonical_row(
    *,
    item: Item,
    parser_run_id: str,
    parsed_at_utc: str,
    marketplace: str,
    source_category: str,
    source_subcategory: str,
    source_query: str,
    source_region_dest: str,
) -> dict[str, Any]:
    wb_product_id = str(item.id)
    feedback_count = item.nmFeedbacks if item.nmFeedbacks is not None else item.feedbacks
    feedback_count_source = "nmFeedbacks" if item.nmFeedbacks is not None else "feedbacks"
    if feedback_count is None:
        feedback_count_source = None

    return {
        "schema_version": 1,
        "marketplace": marketplace,
        "parser_run_id": parser_run_id,
        "parsed_at_utc": parsed_at_utc,
        "source_category": source_category,
        "source_subcategory": source_subcategory,
        "source_query": source_query,
        "source_region_dest": source_region_dest,
        "wb_product_id": wb_product_id,
        "sku_product": wb_product_id,
        "name": item.name,
        "entity": item.entity,
        "brand_id": item.brandId,
        "brand_name": item.brand,
        "seller_id": item.supplierId,
        "seller_name": item.supplier,
        "price_regular": item.priceU,
        "price_discounted": item.salePriceU,
        "price_wb_wallet": item.wb_wallet,
        "discount_percent": item.sale,
        "total_quantity": item.totalQuantity,
        "rating_rounded": item.rating,
        "review_rating": item.nmReviewRating,
        "feedback_count": feedback_count,
        "feedback_count_source": feedback_count_source,
        "image_urls": _split_image_urls(item.image_links),
        "image_count": item.pics,
        "wb_root_id": item.root,
        "subject_parent_id": item.subjectParentId,
        "subject_id": item.subjectId,
        "raw_observed_fields": _observed_fields(item),
    }


def append_jsonl(path: Path, row: dict[str, Any]) -> None:
    with path.open("a", encoding="utf-8") as file:
        file.write(json.dumps(row, ensure_ascii=False, default=str) + "\n")


def _csv_value(value: Any) -> Any:
    if isinstance(value, (list, dict)):
        return json.dumps(value, ensure_ascii=False, default=str)
    return value


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=CANONICAL_PRODUCT_FIELDS)
        writer.writeheader()
        for row in rows:
            writer.writerow({field: _csv_value(row.get(field)) for field in CANONICAL_PRODUCT_FIELDS})


def write_xlsx(path: Path, rows: list[dict[str, Any]]) -> None:
    import pyexcel as pe

    data = [CANONICAL_PRODUCT_FIELDS]
    for row in rows:
        data.append([_csv_value(row.get(field)) for field in CANONICAL_PRODUCT_FIELDS])

    book = pe.Book({"products": data})
    book.save_as(str(path))
