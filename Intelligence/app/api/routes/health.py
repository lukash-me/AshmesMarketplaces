from __future__ import annotations

from datetime import datetime, timezone

from fastapi import APIRouter, Request

from app.core.config import Settings
from app.models.common import HealthResponse


root_router = APIRouter(tags=["health"])
api_router = APIRouter(tags=["health"])


def _health(status: str, settings: Settings) -> HealthResponse:
    return HealthResponse(
        status=status,
        service_name=settings.service_name,
        service_version=settings.service_version,
        checked_at_utc=datetime.now(timezone.utc),
    )


@root_router.get("/health/live", response_model=HealthResponse)
async def live(request: Request) -> HealthResponse:
    return _health("live", request.app.state.settings)


@root_router.get("/health/ready", response_model=HealthResponse)
async def ready(request: Request) -> HealthResponse:
    return _health("ready", request.app.state.settings)


@api_router.get("/health", response_model=HealthResponse)
async def api_health(request: Request) -> HealthResponse:
    return _health("ready", request.app.state.settings)
