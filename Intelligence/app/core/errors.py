from __future__ import annotations

from typing import Any
from uuid import uuid4

from fastapi import FastAPI, Request, status
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHttpException

from app.models.common import ErrorResponse


class AppError(Exception):
    def __init__(
        self,
        *,
        error_code: str,
        message: str,
        status_code: int,
        details: Any | None = None,
        request_id: str | None = None,
    ) -> None:
        self.error_code = error_code
        self.message = message
        self.status_code = status_code
        self.details = details
        self.request_id = request_id


def get_trace_id(request: Request) -> str:
    trace_id = getattr(request.state, "trace_id", None)
    if trace_id:
        return str(trace_id)

    trace_id = uuid4().hex
    request.state.trace_id = trace_id
    return trace_id


async def _request_id_from_body(request: Request) -> str | None:
    try:
        payload = await request.json()
    except Exception:
        return None

    if isinstance(payload, dict):
        request_id = payload.get("requestId")
        return request_id if isinstance(request_id, str) and request_id.strip() else None

    return None


def _json_error(
    *,
    request: Request,
    status_code: int,
    error_code: str,
    message: str,
    details: Any | None = None,
    request_id: str | None = None,
) -> JSONResponse:
    trace_id = get_trace_id(request)
    request.state.error_code = error_code
    if request_id:
        request.state.request_id = request_id

    body = ErrorResponse(
        error_code=error_code,
        message=message,
        details=details,
        request_id=request_id,
        trace_id=trace_id,
    )
    return JSONResponse(
        status_code=status_code,
        content=body.model_dump(by_alias=True),
    )


def _validation_details(errors: list[dict[str, Any]]) -> list[dict[str, Any]]:
    sanitized: list[dict[str, Any]] = []
    for error in errors:
        sanitized.append(
            {
                "loc": [str(part) for part in error.get("loc", [])],
                "message": str(error.get("msg", "Invalid value.")),
                "type": str(error.get("type", "validation_error")),
            }
        )
    return sanitized


def register_exception_handlers(app: FastAPI) -> None:
    @app.exception_handler(AppError)
    async def app_error_handler(request: Request, exception: AppError) -> JSONResponse:
        return _json_error(
            request=request,
            status_code=exception.status_code,
            error_code=exception.error_code,
            message=exception.message,
            details=exception.details,
            request_id=exception.request_id or getattr(request.state, "request_id", None),
        )

    @app.exception_handler(RequestValidationError)
    async def validation_error_handler(request: Request, exception: RequestValidationError) -> JSONResponse:
        request_id = await _request_id_from_body(request)
        return _json_error(
            request=request,
            status_code=422,
            error_code="validation_error",
            message="Request payload validation failed.",
            details={"errors": _validation_details(exception.errors())},
            request_id=request_id,
        )

    @app.exception_handler(StarletteHttpException)
    async def http_error_handler(request: Request, exception: StarletteHttpException) -> JSONResponse:
        status_code = int(exception.status_code)
        error_code = "not_found" if status_code == status.HTTP_404_NOT_FOUND else "http_error"
        return _json_error(
            request=request,
            status_code=status_code,
            error_code=error_code,
            message=str(exception.detail),
            details=None,
            request_id=getattr(request.state, "request_id", None),
        )

    @app.exception_handler(Exception)
    async def unhandled_error_handler(request: Request, exception: Exception) -> JSONResponse:
        return _json_error(
            request=request,
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            error_code="internal_error",
            message="Unexpected service error.",
            details=None,
            request_id=getattr(request.state, "request_id", None),
        )
