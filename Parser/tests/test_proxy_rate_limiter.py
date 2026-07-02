from __future__ import annotations

import asyncio
import sys
from pathlib import Path

PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from app.proxy_rate_limiter import (
    AsyncProxyRateLimiter,
    SyncProxyRateLimiter,
    global_filter_sync_proxy_rate_limiter,
    reset_global_proxy_rate_limiter,
)


def test_rate_limiter_serializes_requests_for_same_proxy() -> None:
    now = 100.0
    sleeps: list[float] = []

    def monotonic() -> float:
        return now

    async def sleep(seconds: float) -> None:
        nonlocal now
        sleeps.append(seconds)
        now += seconds

    limiter = AsyncProxyRateLimiter(min_gap_seconds=2.0, jitter_seconds=0.0, monotonic=monotonic, sleep=sleep)

    async def run() -> None:
        await limiter.wait("proxy-1")
        await limiter.wait("proxy-1")
        await limiter.wait("proxy-1")

    asyncio.run(run())

    assert sleeps == [2.0, 2.0]


def test_rate_limiter_does_not_block_different_proxy() -> None:
    now = 100.0
    sleeps: list[float] = []

    def monotonic() -> float:
        return now

    async def sleep(seconds: float) -> None:
        sleeps.append(seconds)

    limiter = AsyncProxyRateLimiter(min_gap_seconds=2.0, jitter_seconds=0.0, monotonic=monotonic, sleep=sleep)

    async def run() -> None:
        await limiter.wait("proxy-1")
        await limiter.wait("proxy-2")

    asyncio.run(run())

    assert sleeps == []


def test_sync_rate_limiter_serializes_requests_for_same_proxy() -> None:
    now = 100.0
    sleeps: list[float] = []

    def monotonic() -> float:
        return now

    def sleep(seconds: float) -> None:
        nonlocal now
        sleeps.append(seconds)
        now += seconds

    limiter = SyncProxyRateLimiter(min_gap_seconds=3.0, jitter_seconds=0.0, monotonic=monotonic, sleep=sleep)

    limiter.wait("proxy-1")
    limiter.wait("proxy-1")

    assert sleeps == [3.0]


def test_filter_rate_limiter_uses_filter_env(monkeypatch) -> None:
    reset_global_proxy_rate_limiter()
    monkeypatch.setenv("PARSER_FILTER_MIN_REQUEST_GAP_SECONDS", "17")
    monkeypatch.setenv("PARSER_FILTER_JITTER_SECONDS", "0")

    limiter = global_filter_sync_proxy_rate_limiter()

    assert limiter.min_gap_seconds == 17
    assert limiter.jitter_seconds == 0
    reset_global_proxy_rate_limiter()


def test_sync_rate_limiter_uses_persisted_proxy_state(tmp_path) -> None:
    now = 100.0
    wall_now = 1000.0
    sleeps: list[float] = []

    def monotonic() -> float:
        return now

    def wall_clock() -> float:
        return wall_now

    def sleep(seconds: float) -> None:
        nonlocal now, wall_now
        sleeps.append(seconds)
        now += seconds
        wall_now += seconds

    first = SyncProxyRateLimiter(
        min_gap_seconds=30.0,
        jitter_seconds=0.0,
        monotonic=monotonic,
        wall_clock=wall_clock,
        sleep=sleep,
        state_dir=tmp_path,
    )
    first.wait("proxy-1")

    second = SyncProxyRateLimiter(
        min_gap_seconds=30.0,
        jitter_seconds=0.0,
        monotonic=monotonic,
        wall_clock=wall_clock,
        sleep=sleep,
        state_dir=tmp_path,
    )
    second.wait("proxy-1")
    second.wait("proxy-2")

    assert sleeps == [30.0]
