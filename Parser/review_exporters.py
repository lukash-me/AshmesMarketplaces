from __future__ import annotations

import gzip
import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Iterable


REVIEW_FIELDS = [
    "schema_version",
    "parser_run_id",
    "parsed_at_utc",
    "marketplace",
    "input_products_parser_run_id",
    "input_products_jsonl",
    "source_wb_root_id",
    "wb_product_id",
    "review_attribution_mode",
    "review_id_on_mp",
    "rating",
    "text",
    "pros",
    "cons",
    "created_at_on_mp",
    "reviewer_name",
    "reviewer_country",
    "reviewer_has_photo",
    "helpful_plus",
    "helpful_minus",
    "source_category",
    "source_subcategory",
    "source_query",
    "source_region_dest",
    "raw_observed_fields",
]

REVIEW_REPLY_FIELDS = [
    "schema_version",
    "parser_run_id",
    "parsed_at_utc",
    "marketplace",
    "input_products_parser_run_id",
    "input_products_jsonl",
    "source_wb_root_id",
    "wb_product_id",
    "review_attribution_mode",
    "review_id_on_mp",
    "reply_id_on_mp",
    "reply_fallback_hash",
    "text",
    "created_at_on_mp",
    "updated_at_on_mp",
    "reply_author",
    "reply_state",
    "source_category",
    "source_subcategory",
    "source_query",
    "source_region_dest",
    "raw_observed_fields",
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


def write_raw_root_payload(path: Path, payload: dict[str, Any]) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        return path

    with gzip.open(path, "wt", encoding="utf-8") as file:
        json.dump(payload, file, ensure_ascii=False, separators=(",", ":"), default=str)
    return path


@dataclass
class ExportWriteResult:
    reviews_written: int = 0
    replies_written: int = 0
    review_duplicates: int = 0
    reply_duplicates: int = 0
    product_ids_with_new_reviews: set[str] = field(default_factory=set)


class ReviewCanonicalExporter:
    def __init__(self, *, reviews_path: Path, replies_path: Path):
        self.reviews_path = reviews_path
        self.replies_path = replies_path
        self.review_keys: set[tuple[str, str]] = set()
        self.reply_keys: set[tuple[str, str, str]] = set()
        self.product_ids_with_reviews: set[str] = set()
        self._load_existing_rows()

    def _load_existing_rows(self) -> None:
        for row in iter_jsonl(self.reviews_path) or []:
            marketplace = str(row.get("marketplace") or "")
            review_id = str(row.get("review_id_on_mp") or "")
            wb_product_id = str(row.get("wb_product_id") or "")
            if marketplace and review_id:
                self.review_keys.add((marketplace, review_id))
            if wb_product_id:
                self.product_ids_with_reviews.add(wb_product_id)

        for row in iter_jsonl(self.replies_path) or []:
            reply_key = self._reply_key(row)
            if reply_key:
                self.reply_keys.add(reply_key)

    def write_rows(
        self,
        *,
        review_rows: list[dict[str, Any]],
        reply_rows: list[dict[str, Any]],
    ) -> ExportWriteResult:
        result = ExportWriteResult()

        for row in review_rows:
            self._validate_fields(row, REVIEW_FIELDS, "review")
            key = (str(row["marketplace"]), str(row["review_id_on_mp"]))
            if key in self.review_keys:
                result.review_duplicates += 1
                continue

            self.review_keys.add(key)
            append_jsonl(self.reviews_path, row)
            wb_product_id = str(row["wb_product_id"])
            if wb_product_id not in self.product_ids_with_reviews:
                result.product_ids_with_new_reviews.add(wb_product_id)
                self.product_ids_with_reviews.add(wb_product_id)
            result.reviews_written += 1

        for row in reply_rows:
            self._validate_fields(row, REVIEW_REPLY_FIELDS, "review reply")
            key = self._reply_key(row)
            if not key:
                raise ValueError("Review reply row is missing both reply id and fallback hash.")
            if key in self.reply_keys:
                result.reply_duplicates += 1
                continue

            self.reply_keys.add(key)
            append_jsonl(self.replies_path, row)
            result.replies_written += 1

        return result

    @staticmethod
    def _reply_key(row: dict[str, Any]) -> tuple[str, str, str] | None:
        marketplace = str(row.get("marketplace") or "")
        review_id = str(row.get("review_id_on_mp") or "")
        reply_identity = str(row.get("reply_id_on_mp") or row.get("reply_fallback_hash") or "")
        if not marketplace or not review_id or not reply_identity:
            return None
        return marketplace, review_id, reply_identity

    @staticmethod
    def _validate_fields(row: dict[str, Any], field_names: list[str], row_name: str) -> None:
        missing = [field for field in field_names if field not in row]
        if missing:
            raise ValueError(f"Canonical {row_name} row is missing fields: {missing}")
