from __future__ import annotations

import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CHECKED_EXTENSIONS = {
    ".cs",
    ".vue",
    ".ts",
    ".py",
    ".json",
    ".md",
    ".ps1",
    ".sh",
}
MOJIBAKE_MARKERS = (
    "Рџ",
    "Рќ",
    "Р‘",
    "Р“",
    "Р”",
    "Р–",
    "Р—",
    "Р",
    "Рљ",
    "Р›",
    "Рњ",
    "Рћ",
    "Рџ",
    "РЎ",
    "Рў",
    "Р¦",
    "Р§",
    "РЁ",
    "Р©",
    "Р®",
    "РЇ",
    "Р°",
    "Р±",
    "РІ",
    "Рі",
    "Рґ",
    "Рµ",
    "Р¶",
    "Р·",
    "Рё",
    "Р№",
    "Рє",
    "Р»",
    "Рј",
    "РЅ",
    "Рѕ",
    "Рї",
    "СЂ",
    "СЃ",
    "С‚",
    "Сѓ",
    "С„",
    "С…",
    "С†",
    "С‡",
    "С€",
    "С‰",
    "СЊ",
    "С‹",
    "СЌ",
    "СЋ",
    "СЏ",
    "вЂ",
    "\ufffd",
)
ALLOWLIST = {
    "Parser/ARCHITECTURE_WORKLOG.md",
    "tools/check_encoding.py",
}


def git_files(*args: str) -> list[str]:
    result = subprocess.run(
        ["git", *args],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return [line.strip() for line in result.stdout.splitlines() if line.strip()]


def source_files() -> list[Path]:
    names = set(git_files("ls-files"))
    names.update(git_files("ls-files", "--others", "--exclude-standard"))
    files: list[Path] = []
    for name in sorted(names):
        path = ROOT / name
        if path.suffix.lower() not in CHECKED_EXTENSIONS:
            continue
        if not path.is_file():
            continue
        files.append(path)
    return files


def main() -> int:
    failures: list[str] = []
    for path in source_files():
        relative = path.relative_to(ROOT).as_posix()
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError as exception:
            failures.append(f"{relative}: not valid UTF-8 ({exception})")
            continue

        if relative in ALLOWLIST:
            continue

        for marker in MOJIBAKE_MARKERS:
            if marker in text:
                failures.append(f"{relative}: contains mojibake marker {marker!r}")
                break

    if failures:
        print("Encoding check failed:", file=sys.stderr)
        for failure in failures:
            print(f"  - {failure}", file=sys.stderr)
        return 1

    print("Encoding check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
