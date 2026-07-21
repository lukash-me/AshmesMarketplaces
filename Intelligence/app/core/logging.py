from __future__ import annotations

import json
import logging
import time
from uuid import uuid4

from fastapi import FastAPI, Request

from app.core.config import Settings


def configure_logging(settings: Settings) -> None:
    logging.basicConfig(
        level=getattr(logging, settings.log_level.upper(), logging.INFO),
        format="%(message)s",
    )
    logging.getLogger("httpx").setLevel(logging.WARNING)
    logging.getLogger("httpcore").setLevel(logging.WARNING)


def register_request_logging(app: FastAPI) -> None:
    logger = logging.getLogger("ashmes.intelligence.requests")

    @app.middleware("http")
    async def request_logging_middleware(request: Request, call_next):
        trace_id = request.headers.get("x-trace-id") or uuid4().hex
        request.state.trace_id = trace_id
        request.state.request_id = request.headers.get("x-request-id")
        started = time.perf_counter()

        response = await call_next(request)
        duration_ms = round((time.perf_counter() - started) * 1000, 2)
        log_record = {
            "event": "http_request",
            "requestId": getattr(request.state, "request_id", None),
            "traceId": trace_id,
            "method": request.method,
            "path": request.url.path,
            "status": response.status_code,
            "durationMs": duration_ms,
            "errorCode": getattr(request.state, "error_code", None),
        }
        logger.info(json.dumps(log_record, ensure_ascii=False, separators=(",", ":")))
        response.headers["x-trace-id"] = trace_id
        return response
