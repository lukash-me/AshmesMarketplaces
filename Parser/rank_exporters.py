from __future__ import annotations

import json
from pathlib import Path
from typing import Any


def append_jsonl(path: Path, row: dict[str, Any]) -> None:
    with path.open("a", encoding="utf-8") as file:
        file.write(json.dumps(row, ensure_ascii=False, default=str) + "\n")


def append_many_jsonl(path: Path, rows: list[dict[str, Any]]) -> None:
    if not rows:
        return

    with path.open("a", encoding="utf-8") as file:
        for row in rows:
            file.write(json.dumps(row, ensure_ascii=False, default=str) + "\n")
