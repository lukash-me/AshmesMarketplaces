from __future__ import annotations

import json
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

from config import BASE_DIR


KNOWN_RANK_CONTEXT_TYPES = {"search_query", "category_result"}
SUPPORTED_RANK_CONTEXT_TYPES = {"search_query"}


@dataclass(frozen=True)
class RankContextConfig:
    id: str
    type: str
    query: str
    source_category: str | None = None
    source_subcategory: str | None = None
    sort: str = "popular"
    filters: dict[str, Any] = field(default_factory=dict)
    top_n: int | None = None
    source_region_dest: str | None = None


@dataclass(frozen=True)
class RankParserConfig:
    marketplace: str = "wildberries"
    source_region_dest: str = "12354108"
    output_base_dir: Path = BASE_DIR / "output"
    top_n: int = 1000
    page_size: int = 100
    timeout_seconds: int = 10
    max_retries: int = 2
    request_delay_min_seconds: float = 2.0
    request_delay_max_seconds: float = 5.0
    acquire_token: bool = True
    wb_token_secret: str | None = None
    fail_fast: bool = False
    contexts: list[RankContextConfig] = field(default_factory=list)

    @classmethod
    def load(cls, preset_path: Path) -> "RankParserConfig":
        if not preset_path.exists():
            raise FileNotFoundError(f"Rank preset was not found: {preset_path}")

        payload = json.loads(preset_path.read_text(encoding="utf-8"))
        defaults = payload.get("defaults") or {}
        contexts = [
            RankContextConfig(
                id=str(item["id"]),
                type=str(item["type"]),
                query=str(item["query"]),
                source_category=item.get("source_category"),
                source_subcategory=item.get("source_subcategory"),
                sort=str(item.get("sort") or defaults.get("sort") or "popular"),
                filters=dict(item.get("filters") or {}),
                top_n=item.get("top_n"),
                source_region_dest=item.get("source_region_dest"),
            )
            for item in payload.get("contexts") or []
        ]

        config = cls(
            marketplace=str(defaults.get("marketplace") or cls.marketplace),
            source_region_dest=str(defaults.get("source_region_dest") or cls.source_region_dest),
            output_base_dir=Path(defaults.get("output_base_dir") or str(BASE_DIR / "output")),
            top_n=int(defaults.get("top_n") or cls.top_n),
            page_size=int(defaults.get("page_size") or cls.page_size),
            timeout_seconds=int(defaults.get("timeout_seconds") or cls.timeout_seconds),
            max_retries=int(defaults.get("max_retries") or cls.max_retries),
            request_delay_min_seconds=float(
                defaults.get("request_delay_min_seconds")
                if defaults.get("request_delay_min_seconds") is not None
                else cls.request_delay_min_seconds
            ),
            request_delay_max_seconds=float(
                defaults.get("request_delay_max_seconds")
                if defaults.get("request_delay_max_seconds") is not None
                else cls.request_delay_max_seconds
            ),
            acquire_token=bool(defaults.get("acquire_token", cls.acquire_token)),
            wb_token_secret=(str(defaults.get("wb_token")) if defaults.get("wb_token") else None),
            fail_fast=bool(defaults.get("fail_fast", cls.fail_fast)),
            contexts=contexts,
        )
        config.validate()
        return config

    def validate(self) -> None:
        if self.marketplace != "wildberries":
            raise ValueError("Only wildberries marketplace is supported by the rank runner.")

        if self.top_n < 1:
            raise ValueError("Rank top_n must be positive.")

        if self.page_size < 1:
            raise ValueError("Rank page_size must be positive.")

        if self.max_retries < 0 or self.max_retries > 3:
            raise ValueError("Rank max_retries must be between 0 and 3.")

        if self.request_delay_min_seconds < 0:
            raise ValueError("Rank request delay min must be non-negative.")

        if self.request_delay_max_seconds < self.request_delay_min_seconds:
            raise ValueError("Rank request delay max must be greater than or equal to min.")

        if not self.contexts:
            raise ValueError("Rank preset must include at least one context.")

        seen_ids: set[str] = set()
        for context in self.contexts:
            if not context.id.strip():
                raise ValueError("Rank context id is required.")
            if context.id in seen_ids:
                raise ValueError(f"Duplicate rank context id: {context.id}")
            seen_ids.add(context.id)

            if context.type not in KNOWN_RANK_CONTEXT_TYPES:
                raise ValueError(f"Unknown rank context type: {context.type}")
            if not context.query.strip():
                raise ValueError(f"Rank context query is required: {context.id}")
            if context.top_n is not None and int(context.top_n) < 1:
                raise ValueError(f"Rank context top_n must be positive: {context.id}")

    def top_n_for(self, context: RankContextConfig) -> int:
        return int(context.top_n or self.top_n)

    def dest_for(self, context: RankContextConfig) -> str:
        return str(context.source_region_dest or self.source_region_dest)

    def safe_snapshot(self) -> dict[str, Any]:
        snapshot = asdict(self)
        snapshot.pop("wb_token_secret", None)
        snapshot["output_base_dir"] = str(self.output_base_dir)
        return snapshot
