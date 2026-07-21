from __future__ import annotations

import importlib.util
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]


def _load_encoding_guard():
    module_path = REPO_ROOT / "tools" / "check_encoding.py"
    spec = importlib.util.spec_from_file_location("check_encoding", module_path)
    assert spec is not None
    assert spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_encoding_guard_fails_on_mojibake_fixture(monkeypatch, tmp_path: Path) -> None:
    guard = _load_encoding_guard()
    source = tmp_path / "bad.py"
    mojibake = (
        chr(0x0420)
        + chr(0x045F)
        + chr(0x0421)
        + chr(0x0402)
        + chr(0x0420)
        + chr(0x0455)
        + chr(0x0420)
        + chr(0x0454)
        + chr(0x0421)
        + chr(0x0403)
        + chr(0x0420)
        + chr(0x0451)
    )
    source.write_text(f'message = "{mojibake}"\n', encoding="utf-8")

    monkeypatch.setattr(guard, "ROOT", tmp_path)
    monkeypatch.setattr(guard, "ALLOWLIST", set())
    monkeypatch.setattr(guard, "source_files", lambda: [source])

    assert guard.main() == 1


def test_encoding_guard_accepts_valid_utf8_cyrillic(monkeypatch, tmp_path: Path) -> None:
    guard = _load_encoding_guard()
    source = tmp_path / "good.py"
    source.write_text('message = "Прокси"\n', encoding="utf-8")

    monkeypatch.setattr(guard, "ROOT", tmp_path)
    monkeypatch.setattr(guard, "ALLOWLIST", set())
    monkeypatch.setattr(guard, "source_files", lambda: [source])

    assert guard.main() == 0
