from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class PipelineContext:
    """Shared parser pipeline context for new orchestration code."""

    repo_root: Path
    parser_root: Path
    pipeline_run_id: str
    pipeline_run_dir: Path
    test_run: bool = False
    test_label: str | None = None

