from __future__ import annotations

from fastapi import FastAPI

from app.api.routes import health, recommendations
from app.core.config import get_settings
from app.core.errors import register_exception_handlers
from app.core.logging import configure_logging, register_request_logging
from app.services.hot_products import HotProductsService
from app.services.jobs import InMemoryJobStore
from app.services.product_advice import ProductAdviceService
from app.services.top_forecast import TopForecastService


def create_app() -> FastAPI:
    settings = get_settings()
    configure_logging(settings)

    app = FastAPI(
        title="Ashmes Intelligence Service",
        version=settings.service_version,
        description="Contract-first Python Intelligence service scaffold for AshmesMarketplaces.",
    )
    app.state.settings = settings
    app.state.hot_products_service = HotProductsService(settings)
    app.state.product_advice_service = ProductAdviceService(settings)
    app.state.top_forecast_service = TopForecastService(settings)
    app.state.job_store = InMemoryJobStore(app.state.product_advice_service)

    register_exception_handlers(app)
    register_request_logging(app)
    app.include_router(health.root_router)
    app.include_router(health.api_router, prefix=settings.api_prefix)
    app.include_router(recommendations.router, prefix=settings.api_prefix)
    return app


app = create_app()
