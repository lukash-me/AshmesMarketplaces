from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, field
from typing import Any


SCHEMA_VERSION = 1
REVIEW_ATTRIBUTION_MODE = "root_payload"
PRODUCT_FULL_ATTRIBUTION_MODE = "product_full"
ROOT_VARIANT_FILTERED_ATTRIBUTION_MODE = "root_variant_filtered"


@dataclass(frozen=True)
class SelectedProduct:
    wb_product_id: str
    wb_root_id: str
    feedback_count: int | None = None
    source_category: str | None = None
    source_subcategory: str | None = None
    source_query: str | None = None
    source_region_dest: str | None = None


@dataclass(frozen=True)
class MappingIssue:
    phase: str
    message: str
    details: dict[str, Any] = field(default_factory=dict)


@dataclass
class MappedReviewRows:
    review_rows: list[dict[str, Any]] = field(default_factory=list)
    reply_rows: list[dict[str, Any]] = field(default_factory=list)
    issues: list[MappingIssue] = field(default_factory=list)
    payload_feedback_count: int | None = None
    payload_feedback_rows_seen: int = 0
    selected_review_rows_seen: int = 0
    foreign_review_rows_seen: int = 0
    foreign_wb_product_ids: set[str] = field(default_factory=set)
    replies_seen: int = 0


def _value(raw: dict[str, Any], *keys: str) -> Any:
    for key in keys:
        value = raw.get(key)
        if value is not None:
            return value
    return None


def _string_id(value: Any) -> str | None:
    if value is None:
        return None

    normalized = str(value).strip()
    return normalized or None


def _observed_fields(value: dict[str, Any]) -> list[str]:
    return sorted(str(key) for key in value.keys())


def _reviewer_details(feedback: dict[str, Any]) -> dict[str, Any]:
    details = feedback.get("wbUserDetails")
    return details if isinstance(details, dict) else {}


def _votes(feedback: dict[str, Any]) -> dict[str, Any]:
    votes = feedback.get("votes")
    return votes if isinstance(votes, dict) else {}


def _normalize_reply_text(value: Any) -> str:
    text = "" if value is None else str(value)
    return " ".join(text.split())


def make_reply_fallback_hash(
    *,
    marketplace: str,
    review_id_on_mp: str,
    created_at_on_mp: str | None,
    text: Any,
) -> str:
    source = "|".join(
        [
            marketplace,
            review_id_on_mp,
            created_at_on_mp or "",
            _normalize_reply_text(text),
        ]
    )
    return hashlib.sha256(source.encode("utf-8")).hexdigest()


def map_feedback_payload(
    *,
    payload: dict[str, Any],
    selected_products: dict[str, SelectedProduct],
    parser_run_id: str,
    parsed_at_utc: str,
    marketplace: str,
    input_products_parser_run_id: str | None,
    input_products_jsonl: str,
    source_wb_root_id: str,
    review_attribution_mode: str = REVIEW_ATTRIBUTION_MODE,
) -> MappedReviewRows:
    mapped = MappedReviewRows(
        payload_feedback_count=_int_or_none(payload.get("feedbackCount")),
    )
    raw_feedbacks = payload.get("feedbacks") or []
    if not isinstance(raw_feedbacks, list):
        mapped.issues.append(
            MappingIssue(
                phase="parse",
                message="WB root feedback payload feedbacks field is not a list.",
                details={"payload_type": type(raw_feedbacks).__name__},
            )
        )
        return mapped

    mapped.payload_feedback_rows_seen = len(raw_feedbacks)
    mapped.replies_seen = sum(
        1 for feedback in raw_feedbacks
        if isinstance(feedback, dict) and isinstance(feedback.get("answer"), dict)
    )

    for feedback in raw_feedbacks:
        if not isinstance(feedback, dict):
            mapped.issues.append(
                MappingIssue(
                    phase="parse",
                    message="WB feedback row is not an object.",
                    details={"row_type": type(feedback).__name__},
                )
            )
            continue

        wb_product_id = _string_id(_value(feedback, "nmId", "wbProductId"))
        if not wb_product_id or wb_product_id not in selected_products:
            if wb_product_id:
                mapped.foreign_review_rows_seen += 1
                mapped.foreign_wb_product_ids.add(wb_product_id)
            continue

        mapped.selected_review_rows_seen += 1
        selected_product = selected_products[wb_product_id]
        review_id = _string_id(_value(feedback, "id", "feedbackId", "reviewId"))
        if not review_id:
            mapped.issues.append(
                MappingIssue(
                    phase="normalize",
                    message="WB feedback row is missing review id.",
                    details={
                        "wb_product_id": wb_product_id,
                        "source_wb_root_id": source_wb_root_id,
                        "raw_observed_fields": _observed_fields(feedback),
                    },
                )
            )
            continue

        reviewer = _reviewer_details(feedback)
        votes = _votes(feedback)
        review_row = {
            "schema_version": SCHEMA_VERSION,
            "parser_run_id": parser_run_id,
            "parsed_at_utc": parsed_at_utc,
            "marketplace": marketplace,
            "input_products_parser_run_id": input_products_parser_run_id,
            "input_products_jsonl": input_products_jsonl,
            "source_wb_root_id": source_wb_root_id,
            "wb_product_id": wb_product_id,
            "review_attribution_mode": review_attribution_mode,
            "review_id_on_mp": review_id,
            "rating": _int_or_none(_value(feedback, "productValuation", "rating")),
            "text": _value(feedback, "text"),
            "pros": _value(feedback, "pros"),
            "cons": _value(feedback, "cons"),
            "created_at_on_mp": _value(feedback, "createdDate", "dateCreate"),
            "reviewer_name": _value(reviewer, "name"),
            "reviewer_country": _value(reviewer, "country"),
            "reviewer_has_photo": _value(reviewer, "hasPhoto"),
            "helpful_plus": _int_or_none(_value(votes, "pluses", "likes")),
            "helpful_minus": _int_or_none(_value(votes, "minuses", "dislikes")),
            "source_category": selected_product.source_category,
            "source_subcategory": selected_product.source_subcategory,
            "source_query": selected_product.source_query,
            "source_region_dest": selected_product.source_region_dest,
            "raw_observed_fields": _observed_fields(feedback),
        }
        mapped.review_rows.append(review_row)

        answer = feedback.get("answer")
        if not isinstance(answer, dict):
            continue

        reply_id = _string_id(_value(answer, "id", "answerId", "replyId"))
        reply_created_at = _value(answer, "createDate", "createdDate", "dateReply")
        reply_text = _value(answer, "text")
        reply_row = {
            "schema_version": SCHEMA_VERSION,
            "parser_run_id": parser_run_id,
            "parsed_at_utc": parsed_at_utc,
            "marketplace": marketplace,
            "input_products_parser_run_id": input_products_parser_run_id,
            "input_products_jsonl": input_products_jsonl,
            "source_wb_root_id": source_wb_root_id,
            "wb_product_id": wb_product_id,
            "review_attribution_mode": review_attribution_mode,
            "review_id_on_mp": review_id,
            "reply_id_on_mp": reply_id,
            "reply_fallback_hash": None,
            "text": reply_text,
            "created_at_on_mp": reply_created_at,
            "updated_at_on_mp": _value(answer, "lastUpdate", "updatedDate"),
            "reply_author": _reply_author(answer),
            "reply_state": _value(answer, "state"),
            "source_category": selected_product.source_category,
            "source_subcategory": selected_product.source_subcategory,
            "source_query": selected_product.source_query,
            "source_region_dest": selected_product.source_region_dest,
            "raw_observed_fields": _observed_fields(answer),
        }
        if not reply_id:
            reply_row["reply_fallback_hash"] = make_reply_fallback_hash(
                marketplace=marketplace,
                review_id_on_mp=review_id,
                created_at_on_mp=_string_id(reply_created_at),
                text=reply_text,
            )
        mapped.reply_rows.append(reply_row)

    return mapped


def _int_or_none(value: Any) -> int | None:
    if value is None or value == "":
        return None

    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _reply_author(answer: dict[str, Any]) -> Any:
    author = _value(answer, "author", "supplierName", "sourceAuthor")
    if isinstance(author, dict):
        return json.dumps(author, ensure_ascii=False, sort_keys=True)
    return author
