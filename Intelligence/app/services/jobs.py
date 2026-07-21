from __future__ import annotations

from datetime import datetime, timezone
from uuid import uuid4

from app.models.common import JobStatus
from app.models.recommendations import JobStatusResponse, ProductAdviceJobRequest
from app.services.product_advice import ProductAdviceService


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


def _new_job_id() -> str:
    return f"job_{uuid4().hex}"


def _new_request_id() -> str:
    return f"req_{uuid4().hex}"


class InMemoryJobStore:
    """Local/dev-only job store. It is intentionally not production persistence."""

    def __init__(self, advice_service: ProductAdviceService) -> None:
        self._advice_service = advice_service
        self._jobs: dict[str, JobStatusResponse] = {}

    def submit_product_advice(self, request: ProductAdviceJobRequest) -> JobStatusResponse:
        job_id = _new_job_id()
        request_id = request.request_id or _new_request_id()
        now = _utc_now()

        result = self._advice_service.calculate(
            request_id=request_id,
            product=request.product,
            options=request.options,
        )
        record = JobStatusResponse(
            job_id=job_id,
            request_id=request_id,
            status=JobStatus.COMPLETED,
            created_at_utc=now,
            updated_at_utc=_utc_now(),
            result=result,
            error=None,
        )
        self._jobs[job_id] = record
        return record

    def get(self, job_id: str) -> JobStatusResponse | None:
        return self._jobs.get(job_id)
