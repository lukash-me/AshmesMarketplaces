from __future__ import annotations

import os
from dataclasses import asdict, dataclass, field
from pathlib import Path


BASE_DIR = Path(__file__).resolve().parent


def _read_env_file(path: Path) -> dict[str, str]:
    if not path.exists():
        return {}

    values: dict[str, str] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue

        key, value = line.split("=", 1)
        values[key.strip()] = value.strip().strip('"').strip("'")

    return values


def _env_value(values: dict[str, str], key: str, default: str) -> str:
    return os.environ.get(key) or values.get(key) or default


def _env_bool(values: dict[str, str], key: str, default: bool) -> bool:
    raw_value = _env_value(values, key, "true" if default else "false").strip().lower()
    return raw_value in {"1", "true", "yes", "y", "on"}


def _env_int(values: dict[str, str], key: str, default: int) -> int:
    return int(_env_value(values, key, str(default)))


def _env_float(values: dict[str, str], key: str, default: float) -> float:
    return float(_env_value(values, key, str(default)))


def _env_list(values: dict[str, str], key: str, default: list[str]) -> list[str]:
    raw_value = _env_value(values, key, ",".join(default))
    parts = raw_value.replace(";", ",").split(",")
    return [part.strip() for part in parts if part.strip()]


@dataclass(frozen=True)
class ParserConfig:
    PRODUCT_FETCH_MODES = {"price_split", "direct"}

    marketplace: str = "wildberries"
    parent_category: str = "Обувь"
    subcategory_allowlist: list[str] = field(
        default_factory=lambda: ["Обувь для девочек", "Обувь для мальчиков"]
    )
    source_region_dest: str = "12354108"
    output_base_dir: Path = BASE_DIR / "output"
    include_xlsx: bool = True
    enable_raw_samples: bool = False
    raw_sample_limit: int = 3
    acquire_token: bool = True
    wb_token_secret: str | None = None
    include_wb_wallet_prices: bool = True
    product_fetch_mode: str = "price_split"
    max_concurrent: int = 1
    batch_size: int = 5
    timeout_seconds: int = 10
    max_retries: int = 2
    request_delay_min_seconds: float = 2.0
    request_delay_max_seconds: float = 5.0
    batch_delay_min_seconds: float = 15.0
    batch_delay_max_seconds: float = 30.0
    max_catalog_pages_per_subcategory: int = 0
    max_items_per_subcategory: int = 0
    max_limit_signals_per_subcategory: int = 2
    fail_fast: bool = False

    @classmethod
    def load(cls, env_path: Path | None = None) -> "ParserConfig":
        env_values = _read_env_file(env_path or BASE_DIR / ".env")

        config = cls(
            marketplace=_env_value(env_values, "PARSER_MARKETPLACE", cls.marketplace),
            parent_category=_env_value(env_values, "PARSER_PARENT_CATEGORY", cls.parent_category),
            subcategory_allowlist=_env_list(
                env_values,
                "PARSER_SUBCATEGORY_ALLOWLIST",
                ["Обувь для девочек", "Обувь для мальчиков"],
            ),
            source_region_dest=_env_value(env_values, "PARSER_SOURCE_REGION_DEST", cls.source_region_dest),
            output_base_dir=Path(_env_value(env_values, "PARSER_OUTPUT_BASE_DIR", str(BASE_DIR / "output"))),
            include_xlsx=_env_bool(env_values, "PARSER_INCLUDE_XLSX", True),
            enable_raw_samples=_env_bool(env_values, "PARSER_ENABLE_RAW_SAMPLES", False),
            raw_sample_limit=_env_int(env_values, "PARSER_RAW_SAMPLE_LIMIT", 3),
            acquire_token=_env_bool(env_values, "PARSER_ACQUIRE_TOKEN", True),
            wb_token_secret=_env_value(env_values, "PARSER_WB_TOKEN", "").strip() or None,
            include_wb_wallet_prices=_env_bool(env_values, "PARSER_INCLUDE_WB_WALLET_PRICES", True),
            product_fetch_mode=_env_value(env_values, "PARSER_PRODUCT_FETCH_MODE", cls.product_fetch_mode),
            max_concurrent=_env_int(env_values, "PARSER_MAX_CONCURRENT", 1),
            batch_size=_env_int(env_values, "PARSER_BATCH_SIZE", 5),
            timeout_seconds=_env_int(env_values, "PARSER_TIMEOUT_SECONDS", 10),
            max_retries=_env_int(env_values, "PARSER_MAX_RETRIES", 2),
            request_delay_min_seconds=_env_float(env_values, "PARSER_REQUEST_DELAY_MIN_SECONDS", 2.0),
            request_delay_max_seconds=_env_float(env_values, "PARSER_REQUEST_DELAY_MAX_SECONDS", 5.0),
            batch_delay_min_seconds=_env_float(env_values, "PARSER_BATCH_DELAY_MIN_SECONDS", 15.0),
            batch_delay_max_seconds=_env_float(env_values, "PARSER_BATCH_DELAY_MAX_SECONDS", 30.0),
            max_catalog_pages_per_subcategory=_env_int(
                env_values,
                "PARSER_MAX_CATALOG_PAGES_PER_SUBCATEGORY",
                0,
            ),
            max_items_per_subcategory=_env_int(env_values, "PARSER_MAX_ITEMS_PER_SUBCATEGORY", 0),
            max_limit_signals_per_subcategory=_env_int(
                env_values,
                "PARSER_MAX_LIMIT_SIGNALS_PER_SUBCATEGORY",
                2,
            ),
            fail_fast=_env_bool(env_values, "PARSER_FAIL_FAST", False),
        )
        config.validate()
        return config

    def validate(self) -> None:
        if self.marketplace != "wildberries":
            raise ValueError("Only wildberries marketplace is supported by this runner.")

        if self.product_fetch_mode not in self.PRODUCT_FETCH_MODES:
            raise ValueError("PARSER_PRODUCT_FETCH_MODE must be one of: direct, price_split.")

        if not self.parent_category.strip():
            raise ValueError("PARSER_PARENT_CATEGORY is required.")

        if not self.subcategory_allowlist:
            raise ValueError("PARSER_SUBCATEGORY_ALLOWLIST is required. Full-category traversal is disabled.")

        if any(item.strip() == "*" for item in self.subcategory_allowlist):
            raise ValueError("Wildcard subcategory allowlists are not allowed.")

        if self.max_concurrent < 1 or self.max_concurrent > 2:
            raise ValueError("PARSER_MAX_CONCURRENT must be 1 or 2 for the safe debug runner.")

        if self.batch_size < 1 or self.batch_size > 10:
            raise ValueError("PARSER_BATCH_SIZE must be between 1 and 10 for the safe debug runner.")

        if self.max_retries < 0 or self.max_retries > 3:
            raise ValueError("PARSER_MAX_RETRIES must be between 0 and 3.")

        if self.request_delay_min_seconds < 0:
            raise ValueError("PARSER_REQUEST_DELAY_MIN_SECONDS must be non-negative.")

        if self.request_delay_max_seconds < self.request_delay_min_seconds:
            raise ValueError("Request delay max must be greater than or equal to request delay min.")

        if self.batch_delay_max_seconds < self.batch_delay_min_seconds:
            raise ValueError("Batch delay max must be greater than or equal to batch delay min.")

        if self.max_catalog_pages_per_subcategory < 0:
            raise ValueError("PARSER_MAX_CATALOG_PAGES_PER_SUBCATEGORY must be zero or positive.")

        if self.max_items_per_subcategory < 0:
            raise ValueError("PARSER_MAX_ITEMS_PER_SUBCATEGORY must be zero or positive.")

    def safe_snapshot(self) -> dict:
        snapshot = asdict(self)
        snapshot.pop("wb_token_secret", None)
        snapshot["output_base_dir"] = str(self.output_base_dir)
        return snapshot


@dataclass(frozen=True)
class ReviewsParserConfig:
    marketplace: str = "wildberries"
    endpoint_base: str = "https://feedbacks1.wb.ru/feedbacks/v1"
    output_base_dir: Path = BASE_DIR / "output"
    max_concurrent: int = 3
    timeout_seconds: int = 10
    max_retries: int = 2
    request_delay_min_seconds: float = 1.0
    request_delay_max_seconds: float = 3.0
    backoff_base_seconds: float = 1.0
    backoff_max_seconds: float = 30.0
    retain_raw_payloads: bool = True
    raw_payload_compression: str = "gzip"
    fail_fast: bool = False

    @classmethod
    def load(cls, env_path: Path | None = None) -> "ReviewsParserConfig":
        env_values = _read_env_file(env_path or BASE_DIR / ".env")

        config = cls(
            marketplace=_env_value(env_values, "PARSER_MARKETPLACE", cls.marketplace),
            endpoint_base=_env_value(
                env_values,
                "PARSER_REVIEWS_ENDPOINT_BASE",
                cls.endpoint_base,
            ).rstrip("/"),
            output_base_dir=Path(
                _env_value(
                    env_values,
                    "PARSER_OUTPUT_BASE_DIR",
                    str(BASE_DIR / "output"),
                )
            ),
            max_concurrent=_env_int(env_values, "PARSER_REVIEWS_MAX_CONCURRENT", 3),
            timeout_seconds=_env_int(env_values, "PARSER_REVIEWS_TIMEOUT_SECONDS", 10),
            max_retries=_env_int(env_values, "PARSER_REVIEWS_MAX_RETRIES", 2),
            request_delay_min_seconds=_env_float(
                env_values,
                "PARSER_REVIEWS_REQUEST_DELAY_MIN_SECONDS",
                1.0,
            ),
            request_delay_max_seconds=_env_float(
                env_values,
                "PARSER_REVIEWS_REQUEST_DELAY_MAX_SECONDS",
                3.0,
            ),
            backoff_base_seconds=_env_float(
                env_values,
                "PARSER_REVIEWS_BACKOFF_BASE_SECONDS",
                1.0,
            ),
            backoff_max_seconds=_env_float(
                env_values,
                "PARSER_REVIEWS_BACKOFF_MAX_SECONDS",
                30.0,
            ),
            retain_raw_payloads=_env_bool(
                env_values,
                "PARSER_REVIEWS_RETAIN_RAW_PAYLOADS",
                True,
            ),
            raw_payload_compression=_env_value(
                env_values,
                "PARSER_REVIEWS_RAW_PAYLOAD_COMPRESSION",
                "gzip",
            ),
            fail_fast=_env_bool(env_values, "PARSER_REVIEWS_FAIL_FAST", False),
        )
        config.validate()
        return config

    def validate(self) -> None:
        if self.marketplace != "wildberries":
            raise ValueError("Only wildberries marketplace is supported by the reviews runner.")

        if not self.endpoint_base.strip():
            raise ValueError("PARSER_REVIEWS_ENDPOINT_BASE is required.")

        if self.max_concurrent < 1 or self.max_concurrent > 12:
            raise ValueError("PARSER_REVIEWS_MAX_CONCURRENT must be between 1 and 12.")

        if self.timeout_seconds < 1:
            raise ValueError("PARSER_REVIEWS_TIMEOUT_SECONDS must be positive.")

        if self.max_retries < 0 or self.max_retries > 3:
            raise ValueError("PARSER_REVIEWS_MAX_RETRIES must be between 0 and 3.")

        if self.request_delay_min_seconds < 0:
            raise ValueError("PARSER_REVIEWS_REQUEST_DELAY_MIN_SECONDS must be non-negative.")

        if self.request_delay_max_seconds < self.request_delay_min_seconds:
            raise ValueError("Review request delay max must be greater than or equal to min.")

        if self.backoff_base_seconds < 0:
            raise ValueError("PARSER_REVIEWS_BACKOFF_BASE_SECONDS must be non-negative.")

        if self.backoff_max_seconds < self.backoff_base_seconds:
            raise ValueError("Review backoff max must be greater than or equal to base.")

        if self.raw_payload_compression != "gzip":
            raise ValueError("PARSER_REVIEWS_RAW_PAYLOAD_COMPRESSION must be gzip in v1.")

    def safe_snapshot(self) -> dict:
        snapshot = asdict(self)
        snapshot["output_base_dir"] = str(self.output_base_dir)
        return snapshot
