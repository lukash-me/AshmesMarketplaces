from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Any


MOSCOW_TZ = timezone(timedelta(hours=3), name="Europe/Moscow")
WB_DTYPE_WAREHOUSE_BIT = 8
VISIBLE_DELIVERY_STATUS_EMPTY = "empty"
VISIBLE_DELIVERY_STATUS_CALCULATED = "calculated"
VISIBLE_DELIVERY_SOURCE_TIME1_TIME2 = "wb_time1_plus_time2_calculated"

_MONTHS_RU = {
    1: "\u044f\u043d\u0432\u0430\u0440\u044f",
    2: "\u0444\u0435\u0432\u0440\u0430\u043b\u044f",
    3: "\u043c\u0430\u0440\u0442\u0430",
    4: "\u0430\u043f\u0440\u0435\u043b\u044f",
    5: "\u043c\u0430\u044f",
    6: "\u0438\u044e\u043d\u044f",
    7: "\u0438\u044e\u043b\u044f",
    8: "\u0430\u0432\u0433\u0443\u0441\u0442\u0430",
    9: "\u0441\u0435\u043d\u0442\u044f\u0431\u0440\u044f",
    10: "\u043e\u043a\u0442\u044f\u0431\u0440\u044f",
    11: "\u043d\u043e\u044f\u0431\u0440\u044f",
    12: "\u0434\u0435\u043a\u0430\u0431\u0440\u044f",
}


@dataclass(frozen=True)
class VisibleDeliveryEvidence:
    status: str
    label: str | None = None
    date: str | None = None
    source: str | None = None
    observed_at_utc: str | None = None
    raw_payload: dict[str, Any] | None = None


def build_visible_delivery_evidence(
    *,
    product: dict[str, Any],
    supplier_payload: dict[str, Any] | None,
    observed_at_utc: str,
    now_utc: datetime | None = None,
) -> VisibleDeliveryEvidence:
    """Build visible delivery evidence from WB time1/time2 card-detail signals."""

    now = _ensure_utc(now_utc or datetime.now(timezone.utc))
    dtype = _int_or_none(product.get("dtype"))
    supplier_duration = _int_or_none((supplier_payload or {}).get("deliveryDuration"))
    product_time1 = _int_or_none(product.get("time1"))
    product_time2 = _int_or_none(product.get("time2"))
    total_quantity = _int_or_none(product.get("totalQuantity"))
    warehouse_name = (
        "\u0441\u043a\u043b\u0430\u0434 WB"
        if _has_dtype_flag(dtype, WB_DTYPE_WAREHOUSE_BIT)
        else "\u0441\u043a\u043b\u0430\u0434 \u043f\u0440\u043e\u0434\u0430\u0432\u0446\u0430"
    )

    if total_quantity == 0:
        return VisibleDeliveryEvidence(
            status=VISIBLE_DELIVERY_STATUS_EMPTY,
            observed_at_utc=observed_at_utc,
            raw_payload={
                "reason": "out_of_stock",
                "product_time1": product_time1,
                "product_time2": product_time2,
                "product_dtype": dtype,
                "total_quantity": total_quantity,
                "supplier_delivery_duration": supplier_duration,
            },
        )

    if product_time1 is None or product_time2 is None:
        return VisibleDeliveryEvidence(
            status=VISIBLE_DELIVERY_STATUS_EMPTY,
            observed_at_utc=observed_at_utc,
            raw_payload={
                "reason": "missing_time1_or_time2",
                "product_time1": product_time1,
                "product_time2": product_time2,
                "product_dtype": dtype,
                "total_quantity": total_quantity,
                "supplier_delivery_duration": supplier_duration,
            },
        )

    delivery_hours = product_time1 + product_time2
    if delivery_hours <= 0:
        return VisibleDeliveryEvidence(
            status=VISIBLE_DELIVERY_STATUS_EMPTY,
            observed_at_utc=observed_at_utc,
            raw_payload={
                "reason": "non_positive_delivery_hours",
                "delivery_hours": delivery_hours,
                "product_time1": product_time1,
                "product_time2": product_time2,
                "product_dtype": dtype,
                "total_quantity": total_quantity,
                "supplier_delivery_duration": supplier_duration,
            },
        )

    delivery_date = _wb_delivery_date(now, delivery_hours)
    label = f"{_format_delivery_day(delivery_date, now.astimezone(MOSCOW_TZ).date())}, {warehouse_name}"
    return VisibleDeliveryEvidence(
        status=VISIBLE_DELIVERY_STATUS_CALCULATED,
        label=label,
        date=f"{delivery_date.isoformat()}T00:00:00Z",
        source=VISIBLE_DELIVERY_SOURCE_TIME1_TIME2,
        observed_at_utc=observed_at_utc,
        raw_payload={
            "delivery_hours": delivery_hours,
            "calculation_version": "wb_time1_plus_time2_v1",
            "product_time1": product_time1,
            "product_time2": product_time2,
            "product_dtype": dtype,
            "product_wh": _int_or_none(product.get("wh")),
            "total_quantity": total_quantity,
            "supplier_id": _int_or_none(product.get("supplierId")),
            "supplier_delivery_duration": supplier_duration,
            "warehouse_label": warehouse_name,
            "calculation": "delivery_hours=product_time1+product_time2; formatted_like_wb_deliveryDateTxt",
        },
    )


def _wb_delivery_date(now_utc: datetime, delivery_hours: int) -> datetime.date:
    now = now_utc.astimezone(MOSCOW_TZ)
    arrival = now + timedelta(hours=delivery_hours)
    if arrival.month == 1 and arrival.day == 1:
        arrival += timedelta(days=1)
        delivery_hours += 24

    hours_until_midnight = 24 - now.hour
    arrival_hour = arrival.hour
    is_dec_31 = now.month == 12 and now.day == 31
    is_jan_1 = now.month == 1 and now.day == 1

    if now.date() == arrival.date() and delivery_hours < 24:
        if arrival_hour < 9:
            return (now + timedelta(days=1)).date() if is_jan_1 else now.date()
        if arrival_hour < 23:
            return (now + timedelta(days=1)).date() if is_jan_1 and delivery_hours < 5 else now.date()
        return (now + timedelta(days=2 if is_dec_31 else 1)).date()

    if delivery_hours < hours_until_midnight + 9:
        return (now + timedelta(days=2 if is_dec_31 else 1)).date()

    if delivery_hours < 24 + hours_until_midnight:
        return (now + timedelta(days=1 if not is_dec_31 and arrival_hour < 23 else 2)).date()

    if delivery_hours < 48 + hours_until_midnight and arrival_hour < 23:
        return (now + timedelta(days=2)).date()

    if arrival_hour >= 23:
        delivery_hours += hours_until_midnight

    delivery_date = (now + timedelta(hours=delivery_hours)).date()
    if delivery_date.month == 1 and delivery_date.day == 1:
        delivery_date += timedelta(days=1)
    return delivery_date


def _format_delivery_day(delivery_date: datetime.date, today: datetime.date) -> str:
    delta_days = (delivery_date - today).days
    if delta_days == 0:
        return "\u0421\u0435\u0433\u043e\u0434\u043d\u044f"
    if delta_days == 1:
        return "\u0417\u0430\u0432\u0442\u0440\u0430"
    if delta_days == 2:
        return "\u041f\u043e\u0441\u043b\u0435\u0437\u0430\u0432\u0442\u0440\u0430"
    return f"{delivery_date.day} {_MONTHS_RU[delivery_date.month]}"


def _has_dtype_flag(value: int | None, flag: int) -> bool:
    return value is not None and (value & flag) != 0


def _int_or_none(value: Any) -> int | None:
    if value is None:
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _ensure_utc(value: datetime) -> datetime:
    if value.tzinfo is None:
        return value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone.utc)
