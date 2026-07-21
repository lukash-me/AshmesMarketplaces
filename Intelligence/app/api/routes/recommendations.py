from __future__ import annotations

from fastapi import APIRouter, Request

from app.core.errors import AppError
from app.models.common import MetadataResponse
from app.models.recommendations import (
    HotProductsRequest,
    HotProductsResponse,
    JobStatusResponse,
    ProductAdviceJobRequest,
    ProductAdviceRequest,
    ProductAdviceResponse,
    TopForecastPredictRequest,
    TopForecastPredictResponse,
    TopForecastTrainRequest,
    TopForecastTrainResponse,
    WorkspaceProductAnalysisRequest,
    WorkspaceProductAnalysisResponse,
)


router = APIRouter(tags=["intelligence"])


SUPPORTED_ENDPOINTS = [
    "GET /health/live",
    "GET /health/ready",
    "GET /api/v1/health",
    "GET /api/v1/intelligence/metadata",
    "POST /api/v1/recommendations/hot-products",
    "POST /api/v1/recommendations/top-forecast/train",
    "POST /api/v1/recommendations/top-forecast/predict",
    "POST /api/v1/recommendations/workspace-product-analysis",
    "POST /api/v1/recommendations/product-advice",
    "POST /api/v1/jobs/product-advice",
    "GET /api/v1/jobs/{jobId}",
]


@router.get("/intelligence/metadata", response_model=MetadataResponse)
async def metadata(request: Request) -> MetadataResponse:
    settings = request.app.state.settings
    return MetadataResponse(
        service_name=settings.service_name,
        service_version=settings.service_version,
        contract_version=settings.contract_version,
        supported_algorithms=[
            "rule_based_hot_products_v1",
            "catboost_top100_v1",
            "workspace_product_analysis_v1",
            "contract_only_product_advice",
        ],
        supported_endpoints=SUPPORTED_ENDPOINTS,
    )


@router.post("/recommendations/hot-products", response_model=HotProductsResponse)
async def hot_products(payload: HotProductsRequest, request: Request) -> HotProductsResponse:
    request.state.request_id = payload.request_id
    return request.app.state.hot_products_service.calculate(payload)


@router.post("/recommendations/top-forecast/train", response_model=TopForecastTrainResponse)
async def top_forecast_train(payload: TopForecastTrainRequest, request: Request) -> TopForecastTrainResponse:
    request.state.request_id = payload.request_id
    return request.app.state.top_forecast_service.train(payload)


@router.post("/recommendations/top-forecast/predict", response_model=TopForecastPredictResponse)
async def top_forecast_predict(payload: TopForecastPredictRequest, request: Request) -> TopForecastPredictResponse:
    request.state.request_id = payload.request_id
    return request.app.state.top_forecast_service.predict(payload)


@router.post("/recommendations/product-advice", response_model=ProductAdviceResponse)
async def product_advice(payload: ProductAdviceRequest, request: Request) -> ProductAdviceResponse:
    request.state.request_id = payload.request_id
    return request.app.state.product_advice_service.calculate(
        request_id=payload.request_id,
        product=payload.product,
        options=payload.options,
    )


@router.post("/recommendations/workspace-product-analysis", response_model=WorkspaceProductAnalysisResponse)
async def workspace_product_analysis(
    payload: WorkspaceProductAnalysisRequest,
    request: Request,
) -> WorkspaceProductAnalysisResponse:
    request.state.request_id = payload.request_id
    return request.app.state.product_advice_service.calculate_workspace_product_analysis(payload)


@router.post("/jobs/product-advice", response_model=JobStatusResponse)
async def submit_product_advice_job(payload: ProductAdviceJobRequest, request: Request) -> JobStatusResponse:
    response = request.app.state.job_store.submit_product_advice(payload)
    request.state.request_id = response.request_id
    return response


@router.get("/jobs/{jobId}", response_model=JobStatusResponse)
async def get_job(jobId: str, request: Request) -> JobStatusResponse:
    record = request.app.state.job_store.get(jobId)
    if record is None:
        raise AppError(
            error_code="job_not_found",
            message="Job was not found.",
            status_code=404,
            details={"jobId": jobId},
        )

    request.state.request_id = record.request_id
    return record
