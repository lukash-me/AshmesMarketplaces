from __future__ import annotations

import os
from dataclasses import dataclass
from functools import lru_cache


def _env_bool(name: str, default: bool) -> bool:
    raw = os.getenv(name)
    if raw is None:
        return default

    return raw.strip().lower() in {"1", "true", "yes", "on"}


@dataclass(frozen=True)
class Settings:
    service_name: str = "ashmes-intelligence"
    service_version: str = "0.1.0"
    contract_version: str = "2026-05-stage-6b"
    environment: str = "development"
    log_level: str = "INFO"
    api_prefix: str = "/api/v1"
    enable_debug: bool = False
    top_forecast_model_dir: str = "./data/top_forecast_models"


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings(
        service_name=os.getenv("INTELLIGENCE_SERVICE_NAME", Settings.service_name),
        environment=os.getenv("INTELLIGENCE_ENV", Settings.environment),
        log_level=os.getenv("INTELLIGENCE_LOG_LEVEL", Settings.log_level).upper(),
        api_prefix=os.getenv("INTELLIGENCE_API_PREFIX", Settings.api_prefix),
        enable_debug=_env_bool("INTELLIGENCE_ENABLE_DEBUG", Settings.enable_debug),
        top_forecast_model_dir=os.getenv(
            "INTELLIGENCE_TOP_FORECAST_MODEL_DIR",
            Settings.top_forecast_model_dir,
        ),
    )
