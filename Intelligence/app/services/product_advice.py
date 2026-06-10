from __future__ import annotations

from datetime import datetime, timezone
import math
import re

from app.core.config import Settings
from app.models.common import RecommendationStatus
from app.models.recommendations import (
    MarketProductFeatureDto,
    ProductAdviceDto,
    ProductAdviceOptionsDto,
    ProductAdviceResponse,
    WorkspaceProductAnalysisRequest,
    WorkspaceProductAnalysisResponse,
    WorkspaceProductSignalDto,
    WorkspaceSimilarProductGroupDto,
    WorkspaceSimilarProductGroupItemDto,
    WorkspaceSimilarProductDto,
)


CONTRACT_ONLY_ALGORITHM = "contract_only_product_advice"
CONTRACT_ONLY_ALGORITHM_VERSION = "0.1.0"
WORKSPACE_ANALYSIS_ALGORITHM = "workspace_product_analysis_v1"
WORKSPACE_ANALYSIS_ALGORITHM_VERSION = "1.0.0"
NO_MODEL_VERSION = "none"


class ProductAdviceService:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    def calculate(
        self,
        *,
        request_id: str,
        product: MarketProductFeatureDto,
        options: ProductAdviceOptionsDto | None = None,
    ) -> ProductAdviceResponse:
        algorithm = (options.algorithm if options else None) or CONTRACT_ONLY_ALGORITHM
        computed_at = datetime.now(timezone.utc)
        has_identity = any(
            value
            for value in (
                product.product_key,
                product.wb_product_id,
                product.wb_root_id,
            )
        )

        if not has_identity:
            status = RecommendationStatus.NOT_ENOUGH_DATA
            warnings = ["Product identity is missing; product advice cannot be evaluated."]
        else:
            status = RecommendationStatus.UNSUPPORTED
            warnings = ["Product advice calculation is not implemented in Stage 6A."]

        debug = None
        if self._settings.enable_debug:
            debug = {
                "hasProductIdentity": has_identity,
                "stage": "6A_contract_scaffold",
            }

        return ProductAdviceResponse(
            request_id=request_id,
            status=status,
            advice=ProductAdviceDto(
                summary=None,
                recommendations=[],
                factors=[],
                warnings=warnings,
                debug=debug,
            ),
            algorithm=algorithm,
            algorithm_version=CONTRACT_ONLY_ALGORITHM_VERSION,
            model_version=NO_MODEL_VERSION,
            computed_at_utc=computed_at,
        )

    def calculate_workspace_product_analysis(
        self,
        payload: WorkspaceProductAnalysisRequest,
    ) -> WorkspaceProductAnalysisResponse:
        algorithm = (payload.options.algorithm if payload.options else None) or WORKSPACE_ANALYSIS_ALGORITHM
        computed_at = datetime.now(timezone.utc)
        max_similar = payload.options.max_similar_products if payload.options else 5

        if algorithm != WORKSPACE_ANALYSIS_ALGORITHM:
            return WorkspaceProductAnalysisResponse(
                request_id=payload.request_id,
                status=RecommendationStatus.UNSUPPORTED,
                algorithm=algorithm,
                algorithm_version=WORKSPACE_ANALYSIS_ALGORITHM_VERSION,
                model_version=NO_MODEL_VERSION,
                computed_at_utc=computed_at,
                signals=[],
                similar_products=[],
                similar_product_groups=[],
                warnings=[f"Algorithm '{algorithm}' is not supported for workspace product analysis."],
            )

        if not _has_identity(payload.product):
            return WorkspaceProductAnalysisResponse(
                request_id=payload.request_id,
                status=RecommendationStatus.NOT_ENOUGH_DATA,
                algorithm=algorithm,
                algorithm_version=WORKSPACE_ANALYSIS_ALGORITHM_VERSION,
                model_version=NO_MODEL_VERSION,
                computed_at_utc=computed_at,
                signals=[],
                similar_products=[],
                similar_product_groups=[],
                warnings=["Product identity is missing; workspace analysis cannot be evaluated."],
            )

        warnings: list[str] = []
        similar_products = _rank_similar_products(payload.product, payload.candidates, max_similar)
        similar_product_groups = _build_similar_product_groups(payload.product, payload.candidates, max_similar)
        signals = _build_workspace_signals(payload.product, payload.history, payload.candidates)

        if not payload.candidates:
            warnings.append("Market candidates are missing; similar products were not calculated.")

        status = RecommendationStatus.COMPLETED
        if not signals and not similar_products and not _has_history(payload):
            status = RecommendationStatus.NOT_ENOUGH_DATA
            warnings.append("Not enough observations for workspace analysis.")

        return WorkspaceProductAnalysisResponse(
            request_id=payload.request_id,
            status=status,
            algorithm=algorithm,
            algorithm_version=WORKSPACE_ANALYSIS_ALGORITHM_VERSION,
            model_version=NO_MODEL_VERSION,
            computed_at_utc=computed_at,
            signals=signals,
            similar_products=similar_products,
            similar_product_groups=similar_product_groups,
            warnings=warnings,
        )


def _has_identity(product: MarketProductFeatureDto) -> bool:
    return bool(product.product_key or product.wb_product_id or product.wb_root_id)


def _has_history(payload: WorkspaceProductAnalysisRequest) -> bool:
    history = payload.history
    return any(
        len(values) > 1
        for values in (
            history.price_observations,
            history.position_observations,
            history.stock_observations,
            history.feedback_observations,
        )
    )


def _tokens(value: str | None) -> set[str]:
    if not value:
        return set()
    return {part for part in re.split(r"[^0-9a-zа-яё]+", value.lower()) if len(part) > 2}


def _safe_float(value: float | int | None) -> float | None:
    if value is None:
        return None
    try:
        result = float(value)
    except (TypeError, ValueError):
        return None
    return result if math.isfinite(result) else None


def _closeness(left: float | None, right: float | None, scale: float) -> float:
    left_value = _safe_float(left)
    right_value = _safe_float(right)
    if left_value is None or right_value is None or scale <= 0:
        return 0
    return max(0.0, 1.0 - min(abs(left_value - right_value) / scale, 1.0))


def _overlap_score(left: set[str], right: set[str]) -> float:
    if not left or not right:
        return 0
    return len(left & right) / len(left | right)


def _rank_similar_products(
    product: MarketProductFeatureDto,
    candidates: list[MarketProductFeatureDto],
    max_similar: int,
) -> list[WorkspaceSimilarProductDto]:
    product_tokens = _tokens(product.name)
    scored: list[tuple[float, MarketProductFeatureDto, list[str]]] = []

    for candidate in candidates:
        if not candidate.product_key or not _has_identity(candidate):
            continue

        score = 0.0
        reasons: list[str] = []

        if product.source_subcategory and product.source_subcategory == candidate.source_subcategory:
            score += 24
            reasons.append("та же ниша")

        name_score = _overlap_score(product_tokens, _tokens(candidate.name))
        if name_score:
            score += name_score * 30
            reasons.append("похожие слова в названии")

        price_score = _closeness(product.wallet_price or product.price, candidate.wallet_price or candidate.price, 1500)
        if price_score:
            score += price_score * 18
            reasons.append("близкая цена")

        rating_score = _closeness(product.rating, candidate.rating, 1.2)
        if rating_score:
            score += rating_score * 10

        feedback_score = _closeness(
            math.log1p(product.feedback_count or 0),
            math.log1p(candidate.feedback_count or 0),
            4,
        )
        if feedback_score:
            score += feedback_score * 8

        position_score = _closeness(product.position, candidate.position, 80)
        if position_score:
            score += position_score * 6

        stock_score = _closeness(product.total_quantity, candidate.total_quantity, 80)
        if stock_score:
            score += stock_score * 4

        if score >= 30:
            scored.append((min(score, 100), candidate, reasons or ["похожие рыночные признаки"]))

    scored.sort(key=lambda item: item[0], reverse=True)
    return [
        WorkspaceSimilarProductDto(
            product_key=candidate.product_key or "",
            wb_product_id=candidate.wb_product_id,
            wb_root_id=candidate.wb_root_id,
            similarity_score=round(score, 1),
            reason=", ".join(reasons[:3]),
        )
        for score, candidate, reasons in scored[:max_similar]
    ]


def _build_similar_product_groups(
    product: MarketProductFeatureDto,
    candidates: list[MarketProductFeatureDto],
    max_similar: int,
) -> list[WorkspaceSimilarProductGroupDto]:
    candidate_features = [candidate for candidate in candidates if candidate.product_key and _has_identity(candidate)]
    if not candidate_features:
        return []

    product_price = _safe_float(product.wallet_price or product.price)
    product_position = _safe_float(product.position)
    product_feedback = _safe_float(product.feedback_count)
    product_rating = _safe_float(product.rating)
    product_stock = _safe_float(product.total_quantity)

    definitions = [
        (
            "price_disadvantage",
            "Дешевле",
            "Похожие карточки дешевле текущей наблюдаемой карточки.",
            lambda candidate: product_price is not None and _price(candidate) is not None and _price(candidate) < product_price,
            lambda candidate: _price_disadvantage_facts(product_price, candidate),
            lambda candidate: -(product_price - (_price(candidate) or product_price)),
        ),
        (
            "position_disadvantage",
            "Выше в выдаче",
            "Похожие карточки стоят выше в выдаче.",
            lambda candidate: product_position is not None and _safe_float(candidate.position) is not None and _safe_float(candidate.position) < product_position,
            lambda candidate: _position_disadvantage_facts(product_position, candidate),
            lambda candidate: -(product_position - (_safe_float(candidate.position) or product_position)),
        ),
        (
            "review_count_disadvantage",
            "Больше отзывов",
            "У похожих карточек отзывов больше, чем у текущей.",
            lambda candidate: product_feedback is not None and _safe_float(candidate.feedback_count) is not None and _safe_float(candidate.feedback_count) > product_feedback,
            lambda candidate: _feedback_disadvantage_facts(product_feedback, candidate),
            lambda candidate: -((_safe_float(candidate.feedback_count) or product_feedback) - product_feedback),
        ),
        (
            "rating_disadvantage",
            "Оценка выше",
            "У похожих карточек оценка выше, чем у текущей.",
            lambda candidate: product_rating is not None and _safe_float(candidate.rating) is not None and _safe_float(candidate.rating) > product_rating,
            lambda candidate: _rating_disadvantage_facts(product_rating, candidate),
            lambda candidate: -((_safe_float(candidate.rating) or product_rating) - product_rating),
        ),
        (
            "stock_disadvantage",
            "Остаток выше",
            "У похожих карточек наблюдаемый остаток выше.",
            lambda candidate: product_stock is not None and _safe_float(candidate.total_quantity) is not None and _safe_float(candidate.total_quantity) > product_stock,
            lambda candidate: _stock_disadvantage_facts(product_stock, candidate),
            lambda candidate: -((_safe_float(candidate.total_quantity) or product_stock) - product_stock),
        ),
        (
            "weak_competitor_cards",
            "Слабые похожие",
            "Похожие карточки с собственными слабыми параметрами.",
            _is_weak_competitor_card,
            _weak_competitor_facts,
            lambda candidate: -len(_weak_competitor_facts(candidate)),
        ),
    ]

    groups: list[WorkspaceSimilarProductGroupDto] = []
    for key, title, description, predicate, facts_builder, sort_key in definitions:
        matched = [candidate for candidate in candidate_features if predicate(candidate)]
        matched.sort(key=sort_key)
        items = [
            WorkspaceSimilarProductGroupItemDto(
                product_key=candidate.product_key or "",
                facts=facts_builder(candidate),
                tags=facts_builder(candidate),
            )
            for candidate in matched[:max_similar]
        ]
        if items:
            groups.append(WorkspaceSimilarProductGroupDto(
                key=key,
                title=title,
                description=description,
                items=items,
            ))

    return groups


def _price(candidate: MarketProductFeatureDto) -> float | None:
    return _safe_float(candidate.wallet_price or candidate.price)


def _price_disadvantage_facts(product_price: float | None, candidate: MarketProductFeatureDto) -> list[str]:
    candidate_price = _price(candidate)
    if product_price is None or candidate_price is None:
        return []
    return [f"Похожая дешевле на {product_price - candidate_price:.0f} ₽"]


def _position_disadvantage_facts(product_position: float | None, candidate: MarketProductFeatureDto) -> list[str]:
    candidate_position = _safe_float(candidate.position)
    if product_position is None or candidate_position is None:
        return []
    delta = int(product_position - candidate_position)
    return [f"Похожая выше на {delta} {_place_word(delta)}"]


def _feedback_disadvantage_facts(product_feedback: float | None, candidate: MarketProductFeatureDto) -> list[str]:
    candidate_feedback = _safe_float(candidate.feedback_count)
    if product_feedback is None or candidate_feedback is None:
        return []
    return [f"У похожей отзывов больше на {int(candidate_feedback - product_feedback)}"]


def _rating_disadvantage_facts(product_rating: float | None, candidate: MarketProductFeatureDto) -> list[str]:
    candidate_rating = _safe_float(candidate.rating)
    if product_rating is None or candidate_rating is None:
        return []
    return [f"Оценка похожей выше на {candidate_rating - product_rating:.1f}"]


def _stock_disadvantage_facts(product_stock: float | None, candidate: MarketProductFeatureDto) -> list[str]:
    candidate_stock = _safe_float(candidate.total_quantity)
    if product_stock is None or candidate_stock is None:
        return []
    return [f"У похожей остаток выше на {int(candidate_stock - product_stock)}"]


def _is_weak_competitor_card(candidate: MarketProductFeatureDto) -> bool:
    return bool(_weak_competitor_facts(candidate))


def _weak_competitor_facts(candidate: MarketProductFeatureDto) -> list[str]:
    facts: list[str] = []
    rating = _safe_float(candidate.rating)
    feedback = _safe_float(candidate.feedback_count)
    stock = _safe_float(candidate.total_quantity)

    if rating is not None and rating < 4.5:
        facts.append(f"У похожей низкая оценка: {rating:g}")
    if feedback is not None and feedback < 25:
        facts.append(f"У похожей мало отзывов: {int(feedback)}")
    if stock is not None and stock <= 5:
        facts.append(f"У похожей низкий остаток: {int(stock)}")
    return facts


def _place_word(value: int) -> str:
    normalized = abs(value)
    last_two = normalized % 100
    last = normalized % 10
    if 11 <= last_two <= 14:
        return "мест"
    if last == 1:
        return "место"
    if 2 <= last <= 4:
        return "места"
    return "мест"


def _latest_delta(points) -> tuple[float | None, float | None, float | None]:
    if len(points) < 2:
        current = _safe_float(points[0].value) if points else None
        return current, None, None
    current = _safe_float(points[0].value)
    previous = _safe_float(points[1].value)
    if current is None or previous is None:
        return current, previous, None
    return current, previous, current - previous


def _median(values: list[float]) -> float | None:
    clean = sorted(value for value in values if math.isfinite(value))
    if not clean:
        return None
    midpoint = len(clean) // 2
    if len(clean) % 2:
        return clean[midpoint]
    return (clean[midpoint - 1] + clean[midpoint]) / 2


def _build_workspace_signals(
    product: MarketProductFeatureDto,
    history,
    candidates: list[MarketProductFeatureDto],
) -> list[WorkspaceProductSignalDto]:
    signals: list[WorkspaceProductSignalDto] = []
    position = _safe_float(product.position)
    rating = _safe_float(product.rating)
    feedback_count = _safe_float(product.feedback_count)
    stock = _safe_float(product.total_quantity)
    price = _safe_float(product.wallet_price or product.price)

    if position is not None and position <= 20 and rating is not None and rating < 4.5:
        signals.append(_signal(
            "high_position_bad_reviews",
            "high",
            "Высокая позиция при слабом рейтинге",
            "Товар заметен в выдаче, но оценка покупателей ниже ожидаемой для сильной карточки.",
            [f"позиция: {int(position)}", f"рейтинг WB: {rating:g}"],
            0.78,
        ))

    if position is not None and position <= 20 and (feedback_count is None or feedback_count < 25):
        signals.append(_signal(
            "high_position_weak_card",
            "medium",
            "Высокая позиция при слабой базе отзывов",
            "Карточка находится высоко, но отзывов мало. Стоит изучить, за счет чего она держит позицию.",
            [f"позиция: {int(position)}", f"отзывов WB: {int(feedback_count or 0)}"],
            0.66,
        ))

    candidate_prices = [_safe_float(candidate.wallet_price or candidate.price) for candidate in candidates]
    median_price = _median([value for value in candidate_prices if value is not None])
    candidate_ratings = [_safe_float(candidate.rating) for candidate in candidates]
    median_rating = _median([value for value in candidate_ratings if value is not None])
    if price is not None and median_price is not None and price > median_price * 1.25 and (
        rating is None or median_rating is None or rating <= median_rating
    ):
        signals.append(_signal(
            "expensive_without_clear_advantage",
            "medium",
            "Цена выше выборки без явного преимущества",
            "Цена товара заметно выше похожих карточек, при этом рейтинг не выглядит сильнее медианы.",
            [f"цена: {price:g}", f"медиана похожих: {median_price:g}"],
            0.62,
        ))

    if position is not None and position <= 30 and stock is not None and stock <= 5:
        signals.append(_signal(
            "good_position_low_stock",
            "medium",
            "Хорошая позиция при низком остатке",
            "Товар заметен в выдаче, но наблюдаемый остаток низкий.",
            [f"позиция: {int(position)}", f"остаток WB: {int(stock)}"],
            0.7,
        ))

    for code, title, points, severity in (
        ("price_changed", "Изменилась цена", history.price_observations, "medium"),
        ("position_changed", "Изменилась позиция", history.position_observations, "medium"),
        ("stock_changed", "Изменились остатки", history.stock_observations, "medium"),
        ("new_reviews", "Появились новые отзывы", history.feedback_observations, "low"),
    ):
        _, previous, delta = _latest_delta(points)
        if delta is None or delta == 0:
            continue
        if code == "new_reviews" and delta < 0:
            continue
        direction = "выросло" if delta > 0 else "снизилось"
        signals.append(_signal(
            code,
            severity,
            title,
            f"Между двумя последними наблюдениями значение изменилось.",
            [f"было: {previous:g}", f"{direction}: {abs(delta):g}"],
            0.74,
        ))

    return signals[:6]


def _signal(
    code: str,
    severity: str,
    title: str,
    description: str,
    metric_facts: list[str],
    confidence: float,
) -> WorkspaceProductSignalDto:
    return WorkspaceProductSignalDto(
        code=code,
        severity=severity,
        title=title,
        description=description,
        metric_facts=metric_facts,
        confidence=confidence,
    )
