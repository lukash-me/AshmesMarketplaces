from __future__ import annotations

import sys
from pathlib import Path


PARSER_ROOT = Path(__file__).resolve().parents[1]
if str(PARSER_ROOT) not in sys.path:
    sys.path.insert(0, str(PARSER_ROOT))

from app.market_refresh_runner import main, parse_args, run_pipeline  # noqa: E402


__all__ = ["main", "parse_args", "run_pipeline"]


if __name__ == "__main__":
    main()
