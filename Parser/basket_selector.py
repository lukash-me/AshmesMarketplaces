from __future__ import annotations

import json
import re
import urllib.request
from bisect import bisect
from typing import Any


UPSTREAMS_URL = "https://cdn.wbbasket.ru/api/v3/upstreams"
UPSTREAMS_TIMEOUT_SECONDS = 2.0

# Fallback upper volume boundaries for basket-01..basket-46. WB can move new
# volume ranges, so production first tries the upstream route map and uses this
# list only when the route map is unavailable.
BASKET_ENDS = [
    143,
    287,
    431,
    719,
    1007,
    1061,
    1115,
    1169,
    1313,
    1601,
    1655,
    1919,
    2045,
    2189,
    2405,
    2621,
    2837,
    3053,
    3269,
    3485,
    3701,
    3917,
    4133,
    4349,
    4565,
    4877,
    5189,
    5501,
    5813,
    6125,
    6437,
    6749,
    7061,
    7373,
    7685,
    7997,
    8309,
    8741,
    9173,
    9605,
    10373,
    11141,
    11909,
    12677,
    13445,
    14213,
]

_BASKET_HOST_PATTERN = re.compile(r"^basket-(\d+)\.")
_upstream_ranges_cache: list[tuple[int, int, str]] | None = None
_upstream_ranges_loaded = False


def calc_numb_basket(short_id: int, *, use_upstream: bool = True) -> str:
    if use_upstream:
        basket = _calc_from_upstreams(short_id)
        if basket is not None:
            return basket

    basket = bisect(BASKET_ENDS, short_id) + 1
    return f"{basket:02d}"


def extract_basket_ranges(payload: Any) -> list[tuple[int, int, str]]:
    ranges: list[tuple[int, int, str]] = []

    def visit(value: Any) -> None:
        if isinstance(value, dict):
            host = value.get("host")
            volume_from = _first_int(
                value,
                "vol_range_from",
                "volRangeFrom",
                "volume_from",
                "volumeFrom",
                "from",
            )
            volume_to = _first_int(
                value,
                "vol_range_to",
                "volRangeTo",
                "volume_to",
                "volumeTo",
                "to",
            )
            basket = _basket_from_host(host)
            if basket and volume_from is not None and volume_to is not None:
                ranges.append((volume_from, volume_to, basket))

            for child in value.values():
                visit(child)
            return

        if isinstance(value, list):
            for item in value:
                visit(item)

    visit(payload)
    return sorted(set(ranges), key=lambda item: (item[0], item[1], item[2]))


def _calc_from_upstreams(short_id: int) -> str | None:
    for volume_from, volume_to, basket in _get_upstream_ranges():
        if volume_from <= short_id <= volume_to:
            return basket
    return None


def _get_upstream_ranges() -> list[tuple[int, int, str]]:
    global _upstream_ranges_cache, _upstream_ranges_loaded

    if _upstream_ranges_loaded:
        return _upstream_ranges_cache or []

    _upstream_ranges_loaded = True
    try:
        request = urllib.request.Request(
            UPSTREAMS_URL,
            headers={"User-Agent": "Mozilla/5.0"},
        )
        with urllib.request.urlopen(request, timeout=UPSTREAMS_TIMEOUT_SECONDS) as response:
            payload = json.load(response)
        _upstream_ranges_cache = extract_basket_ranges(payload)
    except Exception:
        _upstream_ranges_cache = []

    return _upstream_ranges_cache


def _first_int(source: dict[str, Any], *keys: str) -> int | None:
    for key in keys:
        value = source.get(key)
        if value is None:
            continue
        try:
            return int(value)
        except (TypeError, ValueError):
            continue
    return None


def _basket_from_host(host: Any) -> str | None:
    if not isinstance(host, str):
        return None

    match = _BASKET_HOST_PATTERN.match(host.strip())
    if not match:
        return None

    return f"{int(match.group(1)):02d}"
