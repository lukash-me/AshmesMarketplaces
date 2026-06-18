from __future__ import annotations

from datetime import datetime
from typing import Annotated, Any

from pydantic import BeforeValidator, Field

from app.models.common import (
    ContractModel,
    HotProductsStatus,
    JobStatus,
    RecommendationFactorDto,
    RecommendationStatus,
)


def _coerce_wb_identifier(value: Any) -> str | None:
    if value is None:
        return None
    if isinstance(value, bool):
        raise ValueError("Wildberries identifier must be a string or number.")
    text = str(value).strip()
    return text or None


WbIdentifier = Annotated[str | None, BeforeValidator(_coerce_wb_identifier)]


class RankObservationDto(ContractModel):
    observed_at_utc: datetime = Field(alias="observedAtUtc")
    position: int | None = None
    position_state: str | None = Field(default=None, alias="positionState")
    observed_range_limit: int | None = Field(default=None, alias="observedRangeLimit")
    query: str | None = None
    rank_context_id: str | None = Field(default=None, alias="rankContextId")


class PriceObservationDto(ContractModel):
    observed_at_utc: datetime = Field(alias="observedAtUtc")
    price: float | None = None
    price_without_discount: float | None = Field(default=None, alias="priceWithoutDiscount")
    wallet_price: float | None = Field(default=None, alias="walletPrice")


class ReviewObservationDto(ContractModel):
    observed_at_utc: datetime = Field(alias="observedAtUtc")
    feedback_count: int | None = Field(default=None, alias="feedbackCount")
    parsed_review_count: int | None = Field(default=None, alias="parsedReviewCount")
    positive_review_count: int | None = Field(default=None, alias="positiveReviewCount")
    review_sample_size: int | None = Field(default=None, alias="reviewSampleSize")
    parsed_reply_count: int | None = Field(default=None, alias="parsedReplyCount")


class QuantityObservationDto(ContractModel):
    observed_at_utc: datetime = Field(alias="observedAtUtc")
    total_quantity: int | None = Field(default=None, alias="totalQuantity")


class ProductDeliveryDestinationDto(ContractModel):
    region_key: str | None = Field(default=None, alias="regionKey")
    region_name: str | None = Field(default=None, alias="regionName")
    destination_city: str | None = Field(default=None, alias="destinationCity")
    destination_address: str | None = Field(default=None, alias="destinationAddress")
    visible_delivery_label: str | None = Field(default=None, alias="visibleDeliveryLabel")
    visible_delivery_date: datetime | None = Field(default=None, alias="visibleDeliveryDate")
    delivery_hours: int | None = Field(default=None, alias="deliveryHours")
    delivery_source_type: str | None = Field(default=None, alias="deliverySourceType")
    total_quantity_observed: int | None = Field(default=None, alias="totalQuantityObserved")
    observed_at_utc: datetime | None = Field(default=None, alias="observedAtUtc")


class ProductDeliveryProfileDto(ContractModel):
    destinations: list[ProductDeliveryDestinationDto] = Field(default_factory=list)


class MarketProductReviewSignalDto(ContractModel):
    parsed_review_count: int = Field(default=0, alias="parsedReviewCount")
    parsed_reply_count: int = Field(default=0, alias="parsedReplyCount")
    rated_review_count: int = Field(default=0, alias="ratedReviewCount")
    average_rating: float | None = Field(default=None, alias="averageRating")
    low_rating_review_count: int = Field(default=0, alias="lowRatingReviewCount")
    negative_text_review_count: int = Field(default=0, alias="negativeTextReviewCount")
    bad_review_count: int = Field(default=0, alias="badReviewCount")
    review_window_size: int = Field(default=0, alias="reviewWindowSize")
    recent_two_weeks_count: int = Field(default=0, alias="recentTwoWeeksCount")
    latest_review_run_id: str | None = Field(default=None, alias="latestReviewRunId")
    sentiment_version: int = Field(default=1, alias="sentimentVersion")
    review_scope: str = Field(default="product", alias="reviewScope")
    negative_review_evidence: list["ReviewNegativeEvidenceDto"] = Field(
        default_factory=list,
        alias="negativeReviewEvidence",
    )


class ReviewNegativeEvidenceDto(ContractModel):
    review_id_on_mp: str | None = Field(default=None, alias="reviewIdOnMp")
    source_wb_product_id: WbIdentifier = Field(default=None, alias="sourceWbProductId")
    rating: int | None = None
    created_at_on_mp: datetime | None = Field(default=None, alias="createdAtOnMp")
    snippet: str = ""
    reason_codes: list[str] = Field(default_factory=list, alias="reasonCodes")
    score: float = 0.0


class ProductHistoryDto(ContractModel):
    rank_observations: list[RankObservationDto] = Field(default_factory=list, alias="rankObservations")
    price_observations: list[PriceObservationDto] = Field(default_factory=list, alias="priceObservations")
    review_observations: list[ReviewObservationDto] = Field(default_factory=list, alias="reviewObservations")
    quantity_observations: list[QuantityObservationDto] = Field(default_factory=list, alias="quantityObservations")


class MarketProductFeatureDto(ContractModel):
    product_key: str | None = Field(default=None, alias="productKey")
    wb_product_id: WbIdentifier = Field(default=None, alias="wbProductId")
    wb_root_id: WbIdentifier = Field(default=None, alias="wbRootId")
    source_type: str | None = Field(default=None, alias="sourceType")
    name: str | None = None
    brand_name: str | None = Field(default=None, alias="brandName")
    seller_name: str | None = Field(default=None, alias="sellerName")
    source_category: str | None = Field(default=None, alias="sourceCategory")
    source_subcategory: str | None = Field(default=None, alias="sourceSubcategory")
    price: float | None = None
    price_without_discount: float | None = Field(default=None, alias="priceWithoutDiscount")
    wallet_price: float | None = Field(default=None, alias="walletPrice")
    cost_price: float | None = Field(default=None, alias="costPrice")
    rating: float | None = None
    feedback_count: int | None = Field(default=None, alias="feedbackCount")
    parsed_review_count: int | None = Field(default=None, alias="parsedReviewCount")
    positive_review_count: int | None = Field(default=None, alias="positiveReviewCount")
    review_sample_size: int | None = Field(default=None, alias="reviewSampleSize")
    parsed_reply_count: int | None = Field(default=None, alias="parsedReplyCount")
    position: int | None = None
    position_state: str | None = Field(default=None, alias="positionState")
    observed_range_limit: int | None = Field(default=None, alias="observedRangeLimit")
    total_quantity: int | None = Field(default=None, alias="totalQuantity")
    snapshot_at_utc: datetime | None = Field(default=None, alias="snapshotAtUtc")
    description: str | None = None
    characteristics: dict[str, Any] | list[Any] | None = None
    image_count: int | None = Field(default=None, alias="imageCount")
    review_signals: MarketProductReviewSignalDto | None = Field(default=None, alias="reviewSignals")
    delivery_profile: ProductDeliveryProfileDto | None = Field(default=None, alias="deliveryProfile")
    supplier_name: str | None = Field(default=None, alias="supplierName")
    supplier_url: str | None = Field(default=None, alias="supplierUrl")
    history: ProductHistoryDto | None = None


class MarketScopeDto(ContractModel):
    source_category: str | None = Field(default=None, alias="sourceCategory")
    source_subcategories: list[str] = Field(default_factory=list, alias="sourceSubcategories")
    parser_run_id: str | None = Field(default=None, alias="parserRunId")
    rank_run_id: str | None = Field(default=None, alias="rankRunId")
    review_run_ids: list[str] = Field(default_factory=list, alias="reviewRunIds")


class HotProductsOptionsDto(ContractModel):
    max_recommendations: int = Field(default=10, ge=1, le=1000, alias="maxRecommendations")
    min_confidence: float | None = Field(default=0.45, ge=0, le=1, alias="minConfidence")
    min_products_for_scoring: int = Field(default=20, ge=1, le=10000, alias="minProductsForScoring")
    include_debug: bool = Field(default=False, alias="includeDebug")
    valid_for_hours: int = Field(default=24, ge=1, le=24, alias="validForHours")
    algorithm: str | None = None


class HotProductsRequest(ContractModel):
    request_id: str = Field(min_length=1, alias="requestId")
    generated_at_utc: datetime = Field(alias="generatedAtUtc")
    marketplace: str = Field(min_length=1)
    scope: MarketScopeDto
    products: list[MarketProductFeatureDto]
    options: HotProductsOptionsDto | None = None


class HotProductRecommendationDto(ContractModel):
    recommendation_key: str = Field(alias="recommendationKey")
    product_key: str = Field(alias="productKey")
    wb_product_id: WbIdentifier = Field(default=None, alias="wbProductId")
    wb_root_id: WbIdentifier = Field(default=None, alias="wbRootId")
    score: float
    confidence: float
    title: str
    reason: str
    factors: list[RecommendationFactorDto] = Field(default_factory=list)
    input_snapshot_hash: str = Field(alias="inputSnapshotHash")
    valid_until_utc: datetime | None = Field(default=None, alias="validUntilUtc")
    debug: dict[str, Any] | None = None


class HotProductsResponse(ContractModel):
    request_id: str = Field(alias="requestId")
    status: HotProductsStatus
    algorithm: str
    algorithm_version: str = Field(alias="algorithmVersion")
    model_version: str = Field(alias="modelVersion")
    computed_at_utc: datetime = Field(alias="computedAtUtc")
    recommendations: list[HotProductRecommendationDto] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    diagnostics: dict[str, Any] | None = None


class ProductAdviceOptionsDto(ContractModel):
    algorithm: str | None = None
    include_debug: bool | None = Field(default=None, alias="includeDebug")


class ProductAdviceRequest(ContractModel):
    request_id: str = Field(min_length=1, alias="requestId")
    generated_at_utc: datetime = Field(alias="generatedAtUtc")
    product: MarketProductFeatureDto
    market_context: dict[str, Any] | None = Field(default=None, alias="marketContext")
    question: str | None = None
    options: ProductAdviceOptionsDto | None = None


class ProductAdviceJobRequest(ContractModel):
    request_id: str | None = Field(default=None, alias="requestId")
    generated_at_utc: datetime = Field(alias="generatedAtUtc")
    product: MarketProductFeatureDto
    market_context: dict[str, Any] | None = Field(default=None, alias="marketContext")
    question: str | None = None
    options: ProductAdviceOptionsDto | None = None


class ProductAdviceDto(ContractModel):
    summary: str | None = None
    recommendations: list[str] = Field(default_factory=list)
    factors: list[RecommendationFactorDto] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    debug: dict[str, Any] | None = None


class ProductAdviceResponse(ContractModel):
    request_id: str = Field(alias="requestId")
    status: RecommendationStatus
    advice: ProductAdviceDto
    algorithm: str
    algorithm_version: str = Field(alias="algorithmVersion")
    model_version: str = Field(alias="modelVersion")
    computed_at_utc: datetime = Field(alias="computedAtUtc")


class WorkspaceProductHistoryPointDto(ContractModel):
    observed_at_utc: datetime = Field(alias="observedAtUtc")
    value: float | None = None


class WorkspaceProductHistoryDto(ContractModel):
    price_observations: list[WorkspaceProductHistoryPointDto] = Field(default_factory=list, alias="priceObservations")
    position_observations: list[WorkspaceProductHistoryPointDto] = Field(default_factory=list, alias="positionObservations")
    stock_observations: list[WorkspaceProductHistoryPointDto] = Field(default_factory=list, alias="stockObservations")
    feedback_observations: list[WorkspaceProductHistoryPointDto] = Field(default_factory=list, alias="feedbackObservations")


class WorkspaceProductAnalysisOptionsDto(ContractModel):
    max_similar_products: int = Field(default=5, ge=1, le=20, alias="maxSimilarProducts")
    algorithm: str | None = None


class WorkspaceProductAnalysisRequest(ContractModel):
    request_id: str = Field(min_length=1, alias="requestId")
    generated_at_utc: datetime = Field(alias="generatedAtUtc")
    marketplace: str = Field(min_length=1)
    product: MarketProductFeatureDto
    history: WorkspaceProductHistoryDto
    candidates: list[MarketProductFeatureDto] = Field(default_factory=list)
    options: WorkspaceProductAnalysisOptionsDto | None = None


class WorkspaceProductSignalDto(ContractModel):
    code: str
    severity: str
    title: str
    description: str
    metric_facts: list[str] = Field(default_factory=list, alias="metricFacts")
    confidence: float = Field(ge=0, le=1)
    value: dict[str, Any] | None = None


class WorkspaceSimilarProductDto(ContractModel):
    product_key: str = Field(alias="productKey")
    wb_product_id: WbIdentifier = Field(default=None, alias="wbProductId")
    wb_root_id: WbIdentifier = Field(default=None, alias="wbRootId")
    similarity_score: float = Field(alias="similarityScore")
    reason: str


class WorkspaceSimilarProductGroupItemDto(ContractModel):
    product_key: str = Field(alias="productKey")
    facts: list[str] = Field(default_factory=list)
    tags: list[str] = Field(default_factory=list)


class WorkspaceSimilarProductGroupDto(ContractModel):
    key: str
    title: str
    description: str
    items: list[WorkspaceSimilarProductGroupItemDto] = Field(default_factory=list)


class WorkspaceProductAnalysisResponse(ContractModel):
    request_id: str = Field(alias="requestId")
    status: RecommendationStatus
    algorithm: str
    algorithm_version: str = Field(alias="algorithmVersion")
    model_version: str = Field(alias="modelVersion")
    computed_at_utc: datetime = Field(alias="computedAtUtc")
    signals: list[WorkspaceProductSignalDto] = Field(default_factory=list)
    similar_products: list[WorkspaceSimilarProductDto] = Field(default_factory=list, alias="similarProducts")
    similar_product_groups: list[WorkspaceSimilarProductGroupDto] = Field(default_factory=list, alias="similarProductGroups")
    warnings: list[str] = Field(default_factory=list)


class JobStatusResponse(ContractModel):
    job_id: str = Field(alias="jobId")
    request_id: str = Field(alias="requestId")
    status: JobStatus
    created_at_utc: datetime = Field(alias="createdAtUtc")
    updated_at_utc: datetime = Field(alias="updatedAtUtc")
    result: ProductAdviceResponse | None = None
    error: str | None = None
