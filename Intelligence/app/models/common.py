from __future__ import annotations

from datetime import datetime
from enum import Enum
from typing import Any

from pydantic import BaseModel, ConfigDict, Field


class ContractModel(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="forbid")


class HealthResponse(ContractModel):
    status: str
    service_name: str = Field(alias="serviceName")
    service_version: str = Field(alias="serviceVersion")
    checked_at_utc: datetime = Field(alias="checkedAtUtc")


class MetadataResponse(ContractModel):
    service_name: str = Field(alias="serviceName")
    service_version: str = Field(alias="serviceVersion")
    contract_version: str = Field(alias="contractVersion")
    supported_algorithms: list[str] = Field(alias="supportedAlgorithms")
    supported_endpoints: list[str] = Field(alias="supportedEndpoints")


class ErrorResponse(ContractModel):
    error_code: str = Field(alias="errorCode")
    message: str
    details: Any | None = None
    request_id: str | None = Field(default=None, alias="requestId")
    trace_id: str = Field(alias="traceId")


class RecommendationStatus(str, Enum):
    COMPLETED = "completed"
    QUEUED = "queued"
    NOT_ENOUGH_DATA = "not_enough_data"
    UNSUPPORTED = "unsupported"


class HotProductsStatus(str, Enum):
    COMPLETED = "completed"
    NOT_ENOUGH_DATA = "not_enough_data"
    UNSUPPORTED = "unsupported"


class JobStatus(str, Enum):
    QUEUED = "queued"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


class FactorDirection(str, Enum):
    POSITIVE = "positive"
    NEGATIVE = "negative"
    NEUTRAL = "neutral"


class RecommendationFactorDto(ContractModel):
    code: str
    label: str
    value: Any | None = None
    weight: float
    direction: FactorDirection
