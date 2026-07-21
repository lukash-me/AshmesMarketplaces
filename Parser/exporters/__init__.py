"""Compatibility exports for the legacy root-level exporters module."""

from __future__ import annotations

import importlib.util
from pathlib import Path
from types import ModuleType


def _load_legacy_exporters() -> ModuleType:
    module_path = Path(__file__).resolve().parent.parent / "exporters.py"
    spec = importlib.util.spec_from_file_location("_ashmes_legacy_exporters", module_path)
    if spec is None or spec.loader is None:
        raise ImportError(f"Cannot load legacy exporters module from {module_path}")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


_legacy = _load_legacy_exporters()

append_jsonl = _legacy.append_jsonl
product_to_canonical_row = _legacy.product_to_canonical_row
write_csv = _legacy.write_csv
write_xlsx = _legacy.write_xlsx

__all__ = [
    "append_jsonl",
    "product_to_canonical_row",
    "write_csv",
    "write_xlsx",
]
