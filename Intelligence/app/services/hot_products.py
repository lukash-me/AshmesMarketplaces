from __future__ import annotations

import hashlib
import json
import math
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from statistics import median
from typing import Any

from app.core.config import Settings
from app.models.common import FactorDirection, HotProductsStatus, RecommendationFactorDto
from app.models.recommendations import (
    HotProductRecommendationDto,
    HotProductsOptionsDto,
    HotProductsRequest,
    HotProductsResponse,
    MarketProductFeatureDto,
)


ALGORITHM = "rule_based_hot_products_v1"
ALGORITHM_VERSION = "1.0.0"
NO_MODEL_VERSION = "none"

MIN_SCORE = 60.0
DEFAULT_MIN_CONFIDENCE = 0.45
DEFAULT_MIN_PRODUCTS_FOR_SCORING = 20
MIN_ELIGIBLE_PRODUCTS = 5
DEFAULT_VALID_FOR_HOURS = 24

FACTOR_WEIGHTS = {
    "position": 0.30,
    "rating": 0.20,
    "reviews": 0.20,
    "price": 0.15,
    "stock": 0.05,
    "completeness": 0.10,
}

OPPORTUNITY_FACTOR_WEIGHTS = {
    "high_position_weak_card": 1.00,
    "bad_recent_reviews": 0.90,
    "expensive_without_advantage": 0.84,
    "fast_position_growth": 0.88,
    "top_low_stock": 0.80,
    "duplicate_cards": 0.72,
    "repeated_review_complaint": 0.86,
    "weak_visible_description": 0.70,
    "weak_description": 0.70,
    "missing_key_specs": 0.68,
    "low_review_count_top_position": 0.78,
    "good_reviews_weak_visibility": 0.74,
    "good_reviews_weak_card": 0.73,
    "good_reviews_low_stock": 0.73,
    "good_reviews_high_price": 0.72,
}


@dataclass(frozen=True)
class FactorScore:
    code: str
    label: str
    value: Any | None
    score: float
    confidence: float
    weight: float
    direction: FactorDirection
    debug: dict[str, Any]


@dataclass(frozen=True)
class EligibleProduct:
    product: MarketProductFeatureDto
    product_key: str
    wb_product_id: str | None
    price: float | None
    rating: float | None
    review_count: int | None
    position: int | None
    observed_range_limit: int | None
    total_quantity: int | None
    source_subcategory: str


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


def _clamp(value: float, minimum: float, maximum: float) -> float:
    return max(minimum, min(maximum, value))


def _valid_positive_float(value: float | None) -> float | None:
    if value is None or not math.isfinite(value) or value <= 0:
        return None
    return float(value)


def _valid_non_negative_int(value: int | None) -> int | None:
    if value is None or value < 0:
        return None
    return int(value)


def _valid_position(value: int | None) -> int | None:
    if value is None or value <= 0:
        return None
    return int(value)


def _valid_rating(value: float | None) -> float | None:
    if value is None or not math.isfinite(value) or value <= 0 or value > 5:
        return None
    return float(value)


def _selected_price(product: MarketProductFeatureDto) -> float | None:
    for value in (product.wallet_price, product.price, product.price_without_discount):
        price = _valid_positive_float(value)
        if price is not None:
            return price
    return None


def _selected_review_count(product: MarketProductFeatureDto) -> int | None:
    values = [
        _valid_non_negative_int(product.feedback_count),
        _valid_non_negative_int(product.parsed_review_count),
    ]
    useful_values = [value for value in values if value is not None]
    return max(useful_values) if useful_values else None


def _eligible_product(product: MarketProductFeatureDto) -> EligibleProduct | None:
    source_category = (product.source_category or "").strip()
    source_subcategory = (product.source_subcategory or "").strip()
    wb_product_id = product.wb_product_id.strip() if product.wb_product_id else None
    product_key = (product.product_key or "").strip()
    if not product_key and wb_product_id:
        product_key = f"wildberries:{wb_product_id}"

    if not product_key or not source_category or not source_subcategory:
        return None

    position = _valid_position(product.position)
    rating = _valid_rating(product.rating)
    review_count = _selected_review_count(product)
    price = _selected_price(product)
    total_quantity = _valid_non_negative_int(product.total_quantity)
    observed_range_limit = _valid_position(product.observed_range_limit)

    has_signal = any(
        value is not None
        for value in (
            position,
            rating,
            review_count,
            price,
            total_quantity,
        )
    )
    if not has_signal:
        return None

    return EligibleProduct(
        product=product,
        product_key=product_key,
        wb_product_id=wb_product_id,
        price=price,
        rating=rating,
        review_count=review_count,
        position=position,
        observed_range_limit=observed_range_limit,
        total_quantity=total_quantity,
        source_subcategory=source_subcategory,
    )


def _percentile(values: list[float], percentile: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    if len(ordered) == 1:
        return ordered[0]
    rank = (len(ordered) - 1) * percentile
    lower = math.floor(rank)
    upper = math.ceil(rank)
    if lower == upper:
        return ordered[lower]
    return ordered[lower] + (ordered[upper] - ordered[lower]) * (rank - lower)


def _direction(score: float) -> FactorDirection:
    if score >= 60:
        return FactorDirection.POSITIVE
    if score < 45:
        return FactorDirection.NEGATIVE
    return FactorDirection.NEUTRAL


def _position_factor(item: EligibleProduct, all_positions: list[int]) -> FactorScore:
    if item.position is None:
        return FactorScore(
            code="position",
            label="Позиция",
            value="нет наблюдения",
            score=40,
            confidence=0.20,
            weight=FACTOR_WEIGHTS["position"],
            direction=FactorDirection.NEUTRAL,
            debug={"position": None},
        )

    range_limit = item.observed_range_limit or (max(all_positions) if all_positions else 100)
    range_limit = max(range_limit, item.position, 2)
    ratio = (item.position - 1) / (range_limit - 1)
    score = _clamp(100 - ratio * 70, 30, 100)
    return FactorScore(
        code="position",
        label="Позиция",
        value=f"#{item.position}",
        score=score,
        confidence=0.95,
        weight=FACTOR_WEIGHTS["position"],
        direction=_direction(score),
        debug={"position": item.position, "rangeLimit": range_limit},
    )


def _rating_factor(item: EligibleProduct) -> FactorScore:
    if item.rating is None:
        return FactorScore(
            code="rating",
            label="Рейтинг",
            value="нет данных",
            score=50,
            confidence=0.20,
            weight=FACTOR_WEIGHTS["rating"],
            direction=FactorDirection.NEUTRAL,
            debug={"rating": None},
        )

    if item.rating >= 4.7:
        score = 90 + min((item.rating - 4.7) / 0.3, 1) * 10
    elif item.rating >= 4.2:
        score = 65 + ((item.rating - 4.2) / 0.5) * 17
    else:
        score = 25 + (item.rating / 4.2) * 25
    score = _clamp(score, 0, 100)
    return FactorScore(
        code="rating",
        label="Рейтинг",
        value=round(item.rating, 2),
        score=score,
        confidence=0.85,
        weight=FACTOR_WEIGHTS["rating"],
        direction=_direction(score),
        debug={"rating": item.rating},
    )


def _reviews_factor(item: EligibleProduct, group_review_counts: list[int]) -> FactorScore:
    if item.review_count is None:
        return FactorScore(
            code="reviews",
            label="Отзывы",
            value="нет данных",
            score=45,
            confidence=0.20,
            weight=FACTOR_WEIGHTS["reviews"],
            direction=FactorDirection.NEUTRAL,
            debug={"reviewCount": None},
        )

    if item.review_count == 0:
        score = 35
    else:
        reference = max(_percentile([float(value) for value in group_review_counts], 0.9), float(item.review_count), 1)
        score = 35 + 65 * (math.log1p(item.review_count) / math.log1p(reference))
    score = _clamp(score, 0, 100)
    return FactorScore(
        code="reviews",
        label="Отзывы",
        value=item.review_count,
        score=score,
        confidence=0.80 if item.review_count > 0 else 0.55,
        weight=FACTOR_WEIGHTS["reviews"],
        direction=_direction(score),
        debug={"reviewCount": item.review_count},
    )


def _price_factor(item: EligibleProduct, subcategory_prices: list[float], all_prices: list[float]) -> FactorScore:
    if item.price is None:
        return FactorScore(
            code="price",
            label="Цена",
            value="нет данных",
            score=50,
            confidence=0.20,
            weight=FACTOR_WEIGHTS["price"],
            direction=FactorDirection.NEUTRAL,
            debug={"comparison": "missing"},
        )

    if len(subcategory_prices) >= MIN_ELIGIBLE_PRODUCTS:
        prices = subcategory_prices
        comparison = "subcategory"
        confidence = 0.75
    elif len(all_prices) >= MIN_ELIGIBLE_PRODUCTS:
        prices = all_prices
        comparison = "request"
        confidence = 0.45
    else:
        return FactorScore(
            code="price",
            label="Цена",
            value="недостаточно сравнений",
            score=50,
            confidence=0.25,
            weight=FACTOR_WEIGHTS["price"],
            direction=FactorDirection.NEUTRAL,
            debug={"comparison": "insufficient"},
        )

    p10 = _percentile(prices, 0.10)
    p25 = _percentile(prices, 0.25)
    p75 = _percentile(prices, 0.75)
    p90 = _percentile(prices, 0.90)
    center = median(prices)

    if p25 <= item.price <= p75:
        score = 75
        value = "в основном диапазоне группы"
    elif p10 <= item.price <= p90:
        score = 62
        value = "рядом с диапазоном группы"
    else:
        distance = abs(item.price - center) / max(center, 1)
        score = 50 - min(distance, 1) * 15
        value = "вне основного диапазона группы"

    return FactorScore(
        code="price",
        label="Цена",
        value=value,
        score=_clamp(score, 0, 100),
        confidence=confidence,
        weight=FACTOR_WEIGHTS["price"],
        direction=_direction(score),
        debug={
            "comparison": comparison,
            "price": item.price,
            "p10": round(p10, 2),
            "p90": round(p90, 2),
        },
    )


def _stock_factor(item: EligibleProduct) -> FactorScore:
    if item.total_quantity is None:
        return FactorScore(
            code="stock",
            label="Остаток",
            value="нет данных",
            score=50,
            confidence=0.20,
            weight=FACTOR_WEIGHTS["stock"],
            direction=FactorDirection.NEUTRAL,
            debug={"quantity": None, "capped": None},
        )

    if item.total_quantity == 0:
        score = 20
        value = "нет доступного остатка"
        direction = FactorDirection.NEGATIVE
        confidence = 0.70
        capped = False
    elif item.total_quantity >= 40:
        score = 62
        value = "есть, значение может быть ограничено"
        direction = FactorDirection.NEUTRAL
        confidence = 0.60
        capped = True
    else:
        score = 65
        value = "есть"
        direction = FactorDirection.POSITIVE
        confidence = 0.65
        capped = False

    return FactorScore(
        code="stock",
        label="Остаток",
        value=value,
        score=score,
        confidence=confidence,
        weight=FACTOR_WEIGHTS["stock"],
        direction=direction,
        debug={"quantity": item.total_quantity, "capped": capped},
    )


def _completeness_factor(item: EligibleProduct) -> FactorScore:
    available = sum(
        1
        for value in (
            item.position,
            item.rating,
            item.review_count,
            item.price,
            item.total_quantity,
            item.product.snapshot_at_utc,
        )
        if value is not None
    )
    score = (available / 6) * 100
    return FactorScore(
        code="completeness",
        label="Полнота данных",
        value=f"{available}/6",
        score=score,
        confidence=0.90,
        weight=FACTOR_WEIGHTS["completeness"],
        direction=_direction(score),
        debug={"availableSignals": available},
    )


def _format_money(value: float) -> str:
    return f"{value:.0f} ₽"


def _median_or_none(values: list[float]) -> float | None:
    clean = [value for value in values if math.isfinite(value)]
    return median(clean) if clean else None


def _top_position_limit(item: EligibleProduct, all_positions: list[int]) -> int:
    observed_limit = item.observed_range_limit or (max(all_positions) if all_positions else 100)
    return max(20, min(30, math.ceil(observed_limit * 0.2)))


def _normalized_name(value: str | None) -> str:
    if not value:
        return ""
    letters = [char.lower() if char.isalnum() else " " for char in value]
    tokens = [token for token in "".join(letters).split() if len(token) > 2]
    return " ".join(tokens[:8])


def _name_tokens(value: str | None) -> list[str]:
    if not value:
        return []
    letters = [char.lower() if char.isalnum() else " " for char in value]
    return [token for token in "".join(letters).split() if len(token) > 2]


def _is_top_visible(item: EligibleProduct, all_positions: list[int]) -> bool:
    return item.position is not None and item.position <= _top_position_limit(item, all_positions)


def _peer_review_counts_for_item(item: EligibleProduct, all_items: list[EligibleProduct]) -> list[int]:
    counts: list[int] = []
    item_price = item.price
    item_position = item.position

    for peer in all_items:
        if peer.product_key == item.product_key:
            continue
        if peer.source_subcategory != item.source_subcategory:
            continue
        if peer.review_count is None:
            continue

        if item_price is not None:
            if peer.price is None:
                continue
            lower_price = item_price * 0.70
            upper_price = item_price * 1.30
            if peer.price < lower_price or peer.price > upper_price:
                continue

        if item_position is not None:
            if peer.position is None:
                continue
            max_peer_position = max(50, item_position * 2)
            if peer.position > max_peer_position:
                continue

        counts.append(peer.review_count)

    return counts


def _opportunity_factors(
    *,
    item: EligibleProduct,
    all_positions: list[int],
    all_prices: list[float],
    all_review_counts: list[int],
    all_items: list[EligibleProduct],
    subcategory_prices: list[float],
    subcategory_review_counts: list[int],
) -> list[FactorScore]:
    factors: list[FactorScore] = []
    top_visible = _is_top_visible(item, all_positions)
    median_price = _median_or_none(subcategory_prices) or _median_or_none(all_prices)
    median_reviews = _median_or_none([float(value) for value in subcategory_review_counts]) or _median_or_none(
        [float(value) for value in all_review_counts]
    )

    weak_rating = item.rating is not None and item.rating < 4.5
    weak_reviews = item.review_count is not None and item.review_count < 25
    low_stock = item.total_quantity is not None and item.total_quantity <= 5
    title_tokens = _name_tokens(item.product.name)
    has_spec_tokens = any(
        any(char.isdigit() for char in token)
        or token in {"см", "мм", "вт", "led", "ip", "комплект", "набор"}
        for token in title_tokens
    )

    if top_visible and (weak_rating or weak_reviews or low_stock):
        parts: list[str] = []
        if weak_rating:
            parts.append(f"оценка {item.rating:g}")
        if weak_reviews:
            parts.append(f"отзывов {item.review_count}")
        if low_stock:
            parts.append(f"остаток {item.total_quantity}")
        factors.append(FactorScore(
            code="high_position_weak_card",
            label="Высоко в выдаче, но слабая карточка",
            value=", ".join(parts),
            score=92,
            confidence=0.86,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["high_position_weak_card"],
            direction=FactorDirection.NEGATIVE,
            debug={"position": item.position, "rating": item.rating, "reviewCount": item.review_count, "stock": item.total_quantity},
        ))

    if top_visible and (weak_rating or weak_reviews):
        parts = []
        if weak_rating:
            parts.append(f"оценка ниже 4,5: {item.rating:g}")
        if weak_reviews:
            parts.append(f"мало отзывов: {item.review_count}")
        factors.append(FactorScore(
            code="high_position_weak_reviews",
            label="Высоко в выдаче, но слабые отзывы",
            value=", ".join(parts),
            score=88,
            confidence=0.84,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["high_position_weak_reviews"],
            direction=FactorDirection.NEGATIVE,
            debug={"position": item.position, "rating": item.rating, "reviewCount": item.review_count},
        ))

    if (
        item.product.parsed_review_count is not None
        and item.product.parsed_review_count > 0
        and item.rating is not None
        and item.rating < 4.4
    ):
        factors.append(FactorScore(
            code="bad_recent_reviews",
            label="Плохие последние отзывы",
            value=f"оценка {item.rating:g}, проверено отзывов {item.product.parsed_review_count}",
            score=86,
            confidence=0.78,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["bad_recent_reviews"],
            direction=FactorDirection.NEGATIVE,
            debug={"rating": item.rating, "parsedReviewCount": item.product.parsed_review_count},
        ))

    if top_visible and item.review_count is not None and item.review_count < 100:
        factors.append(FactorScore(
            code="low_review_count_top_position",
            label="Мало отзывов в топе",
            value=f"позиция #{item.position}, отзывов {item.review_count}",
            score=76,
            confidence=0.74,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["low_review_count_top_position"],
            direction=FactorDirection.NEUTRAL,
            debug={"position": item.position, "reviewCount": item.review_count},
        ))

    if (
        not top_visible
        and item.rating is not None
        and item.rating >= 4.8
        and item.review_count is not None
        and item.review_count >= 100
        and item.position is not None
    ):
        factors.append(FactorScore(
            code="good_reviews_weak_visibility",
            label="Хорошие отзывы, слабая видимость",
            value=f"оценка {item.rating:g}, отзывов {item.review_count}, позиция #{item.position}",
            score=74,
            confidence=0.70,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["good_reviews_weak_visibility"],
            direction=FactorDirection.NEUTRAL,
            debug={"position": item.position, "rating": item.rating, "reviewCount": item.review_count},
        ))

    if len(title_tokens) < 4:
        factors.append(FactorScore(
            code="weak_description",
            label="Слабое описание",
            value="короткое название: мало признаков товара",
            score=70,
            confidence=0.58,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["weak_description"],
            direction=FactorDirection.NEGATIVE,
            debug={"nameTokenCount": len(title_tokens)},
        ))

    if title_tokens and not has_spec_tokens:
        factors.append(FactorScore(
            code="missing_key_specs",
            label="Нет важных характеристик",
            value="в названии не видно размера, мощности, комплекта или других характеристик",
            score=68,
            confidence=0.56,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["missing_key_specs"],
            direction=FactorDirection.NEGATIVE,
            debug={"nameTokenCount": len(title_tokens)},
        ))

    if (
        item.price is not None
        and median_price is not None
        and item.price >= median_price * 1.25
        and (item.rating is None or item.rating < 4.7)
        and (item.review_count is None or median_reviews is None or item.review_count <= median_reviews)
    ):
        factors.append(FactorScore(
            code="expensive_without_advantage",
            label="Высокая цена без явного преимущества",
            value=f"{_format_money(item.price)} против медианы {_format_money(median_price)}",
            score=82,
            confidence=0.78,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["expensive_without_advantage"],
            direction=FactorDirection.NEGATIVE,
            debug={"price": item.price, "medianPrice": median_price, "rating": item.rating, "reviewCount": item.review_count},
        ))

    if top_visible and low_stock:
        factors.append(FactorScore(
            code="top_low_stock",
            label="В топе, но низкий остаток",
            value=f"остаток {item.total_quantity}",
            score=80,
            confidence=0.82,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["top_low_stock"],
            direction=FactorDirection.NEGATIVE,
            debug={"position": item.position, "stock": item.total_quantity},
        ))

    same_root_count = 0
    if item.product.wb_root_id:
        same_root_count = sum(1 for peer in all_items if peer.product.wb_root_id == item.product.wb_root_id)
    normalized_name = _normalized_name(item.product.name)
    same_name_count = 0
    if normalized_name:
        same_name_count = sum(1 for peer in all_items if _normalized_name(peer.product.name) == normalized_name)
    duplicate_count = max(same_root_count, same_name_count)
    if duplicate_count >= 3:
        factors.append(FactorScore(
            code="duplicate_cards",
            label="Много одинаковых карточек",
            value=f"похожих карточек: {duplicate_count}",
            score=72,
            confidence=0.72,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["duplicate_cards"],
            direction=FactorDirection.NEUTRAL,
            debug={"sameRootCount": same_root_count, "sameNameCount": same_name_count},
        ))

    return factors


def _snapshot_hash(request: HotProductsRequest, item: EligibleProduct) -> str:
    product = item.product
    payload = {
        "algorithm": ALGORITHM,
        "algorithmVersion": ALGORITHM_VERSION,
        "marketplace": request.marketplace,
        "scope": request.scope.model_dump(by_alias=True, mode="json"),
        "product": {
            "productKey": product.product_key,
            "wbProductId": product.wb_product_id,
            "wbRootId": product.wb_root_id,
            "sourceCategory": product.source_category,
            "sourceSubcategory": product.source_subcategory,
            "price": product.price,
            "priceWithoutDiscount": product.price_without_discount,
            "walletPrice": product.wallet_price,
            "rating": product.rating,
            "feedbackCount": product.feedback_count,
            "parsedReviewCount": product.parsed_review_count,
            "parsedReplyCount": product.parsed_reply_count,
            "position": product.position,
            "positionState": product.position_state,
            "observedRangeLimit": product.observed_range_limit,
            "totalQuantity": product.total_quantity,
            "description": product.description,
            "characteristics": product.characteristics,
            "imageCount": product.image_count,
            "reviewSignals": product.review_signals.model_dump(by_alias=True, mode="json") if product.review_signals else None,
            "snapshotAtUtc": product.snapshot_at_utc.isoformat() if product.snapshot_at_utc else None,
        },
    }
    encoded = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _recommendation_key(item: EligibleProduct, input_snapshot_hash: str) -> str:
    identity = item.wb_product_id or item.product_key
    encoded = f"{ALGORITHM}:{identity}:{input_snapshot_hash}".encode("utf-8")
    return f"hot_{hashlib.sha256(encoded).hexdigest()[:24]}"


def _score_product(
    *,
    request: HotProductsRequest,
    item: EligibleProduct,
    all_items: list[EligibleProduct],
    all_positions: list[int],
    all_prices: list[float],
    all_review_counts: list[int],
    subcategory_prices: list[float],
    subcategory_review_counts: list[int],
    computed_at: datetime,
    valid_for_hours: int,
    include_debug: bool,
) -> HotProductRecommendationDto:
    factors = _opportunity_factors(
        item=item,
        all_positions=all_positions,
        all_prices=all_prices,
        all_review_counts=all_review_counts,
        all_items=all_items,
        subcategory_prices=subcategory_prices,
        subcategory_review_counts=subcategory_review_counts,
    )
    score = 0.0 if not factors else _clamp(max(factor.score for factor in factors), 0, 100)
    confidence = 0.0 if not factors else _clamp(sum(factor.confidence for factor in factors) / len(factors), 0, 1)
    snapshot_hash = _snapshot_hash(request, item)
    dto_factors = [
        RecommendationFactorDto(
            code=factor.code,
            label=factor.label,
            value=factor.value,
            weight=factor.weight,
            direction=factor.direction,
        )
        for factor in factors
    ]

    debug = None
    if include_debug:
        debug = {
            "score": round(score, 4),
            "confidence": round(confidence, 4),
            "factorScores": {
                factor.code: {
                    "score": round(factor.score, 4),
                    "confidence": round(factor.confidence, 4),
                    **factor.debug,
                }
                for factor in factors
            },
        }

    return HotProductRecommendationDto(
        recommendation_key=_recommendation_key(item, snapshot_hash),
        product_key=item.product_key,
        wb_product_id=item.wb_product_id,
        wb_root_id=item.product.wb_root_id,
        score=round(score, 2),
        confidence=round(confidence, 4),
        title=factors[0].label if factors else "Карточка требует проверки",
        reason=(
            "Подборка основана на наблюдаемых рыночных признаках. Перед запуском проверьте "
            "маржинальность, поставщика и конкуренцию."
        ),
        factors=dto_factors,
        input_snapshot_hash=snapshot_hash,
        valid_until_utc=computed_at + timedelta(hours=valid_for_hours),
        debug=debug,
    )


def _characteristics_count(value: Any | None) -> int:
    if value is None:
        return 0
    if isinstance(value, dict):
        return len([key for key, entry in value.items() if str(key).strip() and entry not in (None, "", [])])
    if isinstance(value, list):
        return len([entry for entry in value if entry not in (None, "", {})])
    return 0


def _card_content_value(
    weak_description: bool,
    weak_characteristics: bool,
    weak_visual: bool,
) -> dict[str, Any]:
    parts: list[str] = []
    if weak_description:
        parts.append("описание короткое")
    if weak_characteristics:
        parts.append("характеристик мало")
    if weak_visual:
        parts.append("мало изображений")
    return {
        "label": ", ".join(parts) if parts else "карточка требует проверки",
        "weakDescription": weak_description,
        "weakCharacteristics": weak_characteristics,
        "weakVisual": weak_visual,
    }


def _opportunity_factors(
    *,
    item: EligibleProduct,
    all_positions: list[int],
    all_prices: list[float],
    all_review_counts: list[int],
    all_items: list[EligibleProduct],
    subcategory_prices: list[float],
    subcategory_review_counts: list[int],
) -> list[FactorScore]:
    factors: list[FactorScore] = []
    top_visible = _is_top_visible(item, all_positions)
    median_price = _median_or_none(subcategory_prices) or _median_or_none(all_prices)
    median_reviews = _median_or_none([float(value) for value in subcategory_review_counts]) or _median_or_none(
        [float(value) for value in all_review_counts]
    )

    review_signals = item.product.review_signals
    rated_reviews = review_signals.rated_review_count if review_signals else 0
    low_rating_reviews = review_signals.low_rating_review_count if review_signals else 0
    negative_text_reviews = review_signals.negative_text_review_count if review_signals else 0
    average_recent_rating = _valid_rating(review_signals.average_rating) if review_signals else None
    low_stock = item.total_quantity is not None and item.total_quantity <= 5

    description = (item.product.description or "").strip()
    has_description_data = item.product.description is not None
    has_characteristics_data = item.product.characteristics is not None
    image_count = _valid_non_negative_int(item.product.image_count)
    weak_description = has_description_data and len(description) < 120
    weak_characteristics = has_characteristics_data and _characteristics_count(item.product.characteristics) < 4
    weak_visual = image_count is not None and image_count <= 2
    weak_card_content = weak_description or weak_characteristics or weak_visual
    good_reviews = item.rating is not None and item.rating >= 4.7 and item.review_count is not None and item.review_count >= 50

    if rated_reviews > 0 and (low_rating_reviews > 0 or negative_text_reviews > 0):
        label_parts: list[str] = []
        if low_rating_reviews > 0:
            label_parts.append(f"низких оценок: {low_rating_reviews}")
        if negative_text_reviews > 0:
            label_parts.append(f"негативных текстов: {negative_text_reviews}")
        factors.append(FactorScore(
            code="bad_recent_reviews",
            label="Плохие последние отзывы",
            value={
                "label": ", ".join(label_parts),
                "ratedReviews": rated_reviews,
                "lowRatingReviews": low_rating_reviews,
                "negativeTextReviews": negative_text_reviews,
                "averageRating": average_recent_rating,
            },
            score=86,
            confidence=0.78,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["bad_recent_reviews"],
            direction=FactorDirection.NEGATIVE,
            debug={
                "ratedReviews": rated_reviews,
                "lowRatingReviews": low_rating_reviews,
                "negativeTextReviews": negative_text_reviews,
                "averageRating": average_recent_rating,
            },
        ))

    peer_review_counts = _peer_review_counts_for_item(item, all_items)
    peer_median_reviews = _median_or_none([float(value) for value in peer_review_counts])
    if (
        top_visible
        and item.review_count is not None
        and peer_median_reviews is not None
        and len(peer_review_counts) >= 5
        and peer_median_reviews >= 10
        and item.review_count < peer_median_reviews * 0.55
    ):
        factors.append(FactorScore(
            code="low_review_count_top_position",
            label="Мало отзывов в топе",
            value={
                "label": f"{item.review_count} против {round(peer_median_reviews)} у похожих карточек",
                "reviewCount": item.review_count,
                "peerMedianReviewCount": round(peer_median_reviews),
                "peerSampleSize": len(peer_review_counts),
                "position": item.position,
            },
            score=76,
            confidence=0.74,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["low_review_count_top_position"],
            direction=FactorDirection.NEGATIVE,
            debug={
                "position": item.position,
                "reviewCount": item.review_count,
                "peerMedianReviews": peer_median_reviews,
                "peerSampleSize": len(peer_review_counts),
            },
        ))

    if (
        not top_visible
        and item.rating is not None
        and item.rating >= 4.8
        and item.review_count is not None
        and item.review_count >= 100
        and item.position is not None
    ):
        factors.append(FactorScore(
            code="good_reviews_weak_visibility",
            label="Хорошие отзывы, слабая видимость",
            value={
                "label": f"оценка {item.rating:g}, отзывов {item.review_count}, позиция #{item.position}",
                "rating": item.rating,
                "reviewCount": item.review_count,
                "position": item.position,
            },
            score=74,
            confidence=0.70,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["good_reviews_weak_visibility"],
            direction=FactorDirection.NEUTRAL,
            debug={"position": item.position, "rating": item.rating, "reviewCount": item.review_count},
        ))

    if top_visible and weak_card_content:
        factors.append(FactorScore(
            code="high_position_weak_card",
            label="Слабая карточка в топе",
            value=_card_content_value(weak_description, weak_characteristics, weak_visual),
            score=90,
            confidence=0.74,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["high_position_weak_card"],
            direction=FactorDirection.NEGATIVE,
            debug={"position": item.position, "imageCount": image_count},
        ))

    if weak_description:
        factors.append(FactorScore(
            code="weak_description",
            label="Слабое описание",
            value={
                "label": f"описание короткое: {len(description)} символов",
                "descriptionLength": len(description),
            },
            score=70,
            confidence=0.68,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["weak_description"],
            direction=FactorDirection.NEGATIVE,
            debug={"descriptionLength": len(description)},
        ))

    if weak_characteristics:
        count = _characteristics_count(item.product.characteristics)
        factors.append(FactorScore(
            code="missing_key_specs",
            label="Проверьте характеристики",
            value={"label": f"характеристик мало: {count}", "characteristicsCount": count},
            score=68,
            confidence=0.66,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["missing_key_specs"],
            direction=FactorDirection.NEGATIVE,
            debug={"characteristicsCount": count},
        ))

    if (
        item.price is not None
        and median_price is not None
        and item.price >= median_price * 1.25
        and (item.rating is None or item.rating < 4.7)
        and (item.review_count is None or median_reviews is None or item.review_count <= median_reviews)
    ):
        factors.append(FactorScore(
            code="expensive_without_advantage",
            label="Высокая цена без явного преимущества",
            value={
                "label": f"{_format_money(item.price)} против медианы {_format_money(median_price)}",
                "price": round(item.price),
                "peerMedianPrice": round(median_price),
            },
            score=82,
            confidence=0.78,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["expensive_without_advantage"],
            direction=FactorDirection.NEGATIVE,
            debug={"price": item.price, "medianPrice": median_price, "rating": item.rating, "reviewCount": item.review_count},
        ))

    if top_visible and low_stock:
        factors.append(FactorScore(
            code="top_low_stock",
            label="В топе, но низкий остаток",
            value={"label": f"остаток {item.total_quantity}", "stock": item.total_quantity, "position": item.position},
            score=80,
            confidence=0.82,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["top_low_stock"],
            direction=FactorDirection.NEGATIVE,
            debug={"position": item.position, "stock": item.total_quantity},
        ))

    if good_reviews and weak_card_content:
        factors.append(FactorScore(
            code="good_reviews_weak_card",
            label="Хороший товар, слабая карточка",
            value=_card_content_value(weak_description, weak_characteristics, weak_visual),
            score=73,
            confidence=0.70,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["good_reviews_weak_card"],
            direction=FactorDirection.NEUTRAL,
            debug={"rating": item.rating, "reviewCount": item.review_count, "imageCount": image_count},
        ))

    if good_reviews and low_stock:
        factors.append(FactorScore(
            code="good_reviews_low_stock",
            label="Хорошие отзывы, низкий остаток",
            value={"label": f"оценка {item.rating:g}, остаток {item.total_quantity}", "stock": item.total_quantity},
            score=73,
            confidence=0.70,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["good_reviews_low_stock"],
            direction=FactorDirection.NEUTRAL,
            debug={"rating": item.rating, "reviewCount": item.review_count, "stock": item.total_quantity},
        ))

    if (
        good_reviews
        and item.price is not None
        and median_price is not None
        and item.price >= median_price * 1.25
    ):
        factors.append(FactorScore(
            code="good_reviews_high_price",
            label="Хорошие отзывы, высокая цена",
            value={
                "label": f"{_format_money(item.price)} против медианы {_format_money(median_price)}",
                "price": round(item.price),
                "peerMedianPrice": round(median_price),
            },
            score=72,
            confidence=0.68,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["good_reviews_high_price"],
            direction=FactorDirection.NEUTRAL,
            debug={"rating": item.rating, "reviewCount": item.review_count, "price": item.price, "medianPrice": median_price},
        ))

    same_root_count = 0
    if item.product.wb_root_id:
        same_root_count = sum(1 for peer in all_items if peer.product.wb_root_id == item.product.wb_root_id)
    normalized_name = _normalized_name(item.product.name)
    same_name_count = 0
    if normalized_name:
        same_name_count = sum(1 for peer in all_items if _normalized_name(peer.product.name) == normalized_name)
    duplicate_count = max(same_root_count, same_name_count)
    if duplicate_count >= 3:
        factors.append(FactorScore(
            code="duplicate_cards",
            label="Много одинаковых карточек",
            value={"label": f"похожих карточек: {duplicate_count}", "similarCards": duplicate_count},
            score=72,
            confidence=0.72,
            weight=OPPORTUNITY_FACTOR_WEIGHTS["duplicate_cards"],
            direction=FactorDirection.NEUTRAL,
            debug={"sameRootCount": same_root_count, "sameNameCount": same_name_count},
        ))

    return factors


def _score_product(
    *,
    request: HotProductsRequest,
    item: EligibleProduct,
    all_items: list[EligibleProduct],
    all_positions: list[int],
    all_prices: list[float],
    all_review_counts: list[int],
    subcategory_prices: list[float],
    subcategory_review_counts: list[int],
    computed_at: datetime,
    valid_for_hours: int,
    include_debug: bool,
) -> HotProductRecommendationDto:
    factors = _opportunity_factors(
        item=item,
        all_positions=all_positions,
        all_prices=all_prices,
        all_review_counts=all_review_counts,
        all_items=all_items,
        subcategory_prices=subcategory_prices,
        subcategory_review_counts=subcategory_review_counts,
    )
    score = 0.0 if not factors else _clamp(max(factor.score for factor in factors), 0, 100)
    confidence = 0.0 if not factors else _clamp(sum(factor.confidence for factor in factors) / len(factors), 0, 1)
    snapshot_hash = _snapshot_hash(request, item)
    dto_factors = [
        RecommendationFactorDto(
            code=factor.code,
            label=factor.label,
            value=factor.value,
            weight=factor.weight,
            direction=factor.direction,
        )
        for factor in factors
    ]

    debug = None
    if include_debug:
        debug = {
            "score": round(score, 4),
            "confidence": round(confidence, 4),
            "factorScores": {
                factor.code: {
                    "score": round(factor.score, 4),
                    "confidence": round(factor.confidence, 4),
                    **factor.debug,
                }
                for factor in factors
            },
        }

    return HotProductRecommendationDto(
        recommendation_key=_recommendation_key(item, snapshot_hash),
        product_key=item.product_key,
        wb_product_id=item.wb_product_id,
        wb_root_id=item.product.wb_root_id,
        score=round(score, 2),
        confidence=round(confidence, 4),
        title=factors[0].label if factors else "Карточка требует проверки",
        reason="Подборка основана на наблюдаемых рыночных признаках. Проверьте товар, поставщика и конкуренцию.",
        factors=dto_factors,
        input_snapshot_hash=snapshot_hash,
        valid_until_utc=computed_at + timedelta(hours=valid_for_hours),
        debug=debug,
    )


class HotProductsService:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    def calculate(self, request: HotProductsRequest) -> HotProductsResponse:
        options = request.options or HotProductsOptionsDto()
        algorithm = options.algorithm or ALGORITHM
        computed_at = _utc_now()

        if algorithm != ALGORITHM:
            return HotProductsResponse(
                request_id=request.request_id,
                status=HotProductsStatus.UNSUPPORTED,
                algorithm=algorithm,
                algorithm_version="unsupported",
                model_version=NO_MODEL_VERSION,
                computed_at_utc=computed_at,
                recommendations=[],
                warnings=[f"Algorithm '{algorithm}' is not supported by this service."],
            )

        effective_min_products = max(MIN_ELIGIBLE_PRODUCTS, options.min_products_for_scoring)
        effective_min_confidence = DEFAULT_MIN_CONFIDENCE if options.min_confidence is None else options.min_confidence

        if not request.products:
            return self._not_enough_data(
                request=request,
                computed_at=computed_at,
                warnings=["Product snapshot is empty; rule-based scoring requires comparable products."],
            )

        if len(request.products) < effective_min_products:
            return self._not_enough_data(
                request=request,
                computed_at=computed_at,
                warnings=[
                    f"Product snapshot contains {len(request.products)} products; at least "
                    f"{effective_min_products} are required for rule-based comparison."
                ],
            )

        eligible = [item for item in (_eligible_product(product) for product in request.products) if item is not None]
        if len(eligible) < MIN_ELIGIBLE_PRODUCTS:
            return self._not_enough_data(
                request=request,
                computed_at=computed_at,
                warnings=[
                    f"Only {len(eligible)} eligible products remain after filtering invalid rows; "
                    f"at least {MIN_ELIGIBLE_PRODUCTS} are required."
                ],
            )

        all_positions = [item.position for item in eligible if item.position is not None]
        all_prices = [item.price for item in eligible if item.price is not None]
        all_review_counts = [item.review_count for item in eligible if item.review_count is not None]
        include_debug = self._settings.enable_debug or options.include_debug
        valid_for_hours = min(options.valid_for_hours, DEFAULT_VALID_FOR_HOURS)

        recommendations: list[HotProductRecommendationDto] = []
        for item in eligible:
            subcategory_prices = [
                peer.price
                for peer in eligible
                if peer.source_subcategory == item.source_subcategory and peer.price is not None
            ]
            subcategory_review_counts = [
                peer.review_count
                for peer in eligible
                if peer.source_subcategory == item.source_subcategory and peer.review_count is not None
            ]
            recommendation = _score_product(
                request=request,
                item=item,
                all_items=eligible,
                all_positions=all_positions,
                all_prices=all_prices,
                all_review_counts=all_review_counts,
                subcategory_prices=subcategory_prices,
                subcategory_review_counts=subcategory_review_counts,
                computed_at=computed_at,
                valid_for_hours=valid_for_hours,
                include_debug=include_debug,
            )
            if recommendation.score >= MIN_SCORE and recommendation.confidence >= effective_min_confidence:
                recommendations.append(recommendation)

        recommendations.sort(key=lambda item: item.score, reverse=True)
        recommendations = recommendations[: options.max_recommendations]

        warnings: list[str] = []
        if not recommendations:
            warnings.append(
                "Data was sufficient for scoring, but no product passed the score and confidence thresholds."
            )
        elif len(recommendations) < options.max_recommendations:
            warnings.append("Fewer products passed the conservative score and confidence thresholds.")

        return HotProductsResponse(
            request_id=request.request_id,
            status=HotProductsStatus.COMPLETED,
            algorithm=ALGORITHM,
            algorithm_version=ALGORITHM_VERSION,
            model_version=NO_MODEL_VERSION,
            computed_at_utc=computed_at,
            recommendations=recommendations,
            warnings=warnings,
        )

    def _not_enough_data(
        self,
        *,
        request: HotProductsRequest,
        computed_at: datetime,
        warnings: list[str],
    ) -> HotProductsResponse:
        return HotProductsResponse(
            request_id=request.request_id,
            status=HotProductsStatus.NOT_ENOUGH_DATA,
            algorithm=ALGORITHM,
            algorithm_version=ALGORITHM_VERSION,
            model_version=NO_MODEL_VERSION,
            computed_at_utc=computed_at,
            recommendations=[],
            warnings=warnings,
        )
