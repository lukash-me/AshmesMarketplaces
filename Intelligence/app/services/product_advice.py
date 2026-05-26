from __future__ import annotations

from datetime import datetime, timezone

from app.core.config import Settings
from app.models.common import RecommendationStatus
from app.models.recommendations import (
    MarketProductFeatureDto,
    ProductAdviceDto,
    ProductAdviceOptionsDto,
    ProductAdviceResponse,
)


CONTRACT_ONLY_ALGORITHM = "contract_only_product_advice"
CONTRACT_ONLY_ALGORITHM_VERSION = "0.1.0"
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
