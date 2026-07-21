from __future__ import annotations

import asyncio
import os
import random
import re
import threading
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Awaitable, Callable


SleepFunc = Callable[[float], Awaitable[None]]
MonotonicFunc = Callable[[], float]
WallClockFunc = Callable[[], float]


def _safe_proxy_key(proxy_key: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", proxy_key).strip("._") or "direct"


def _read_persisted_next_allowed(state_dir: Path | None, proxy_key: str) -> float | None:
    if state_dir is None:
        return None
    path = state_dir / f"{_safe_proxy_key(proxy_key)}.next_allowed"
    try:
        if not path.exists():
            return None
        return float(path.read_text(encoding="utf-8").strip())
    except (OSError, ValueError):
        return None


def _write_persisted_next_allowed(state_dir: Path | None, proxy_key: str, value: float) -> None:
    if state_dir is None:
        return
    state_dir.mkdir(parents=True, exist_ok=True)
    path = state_dir / f"{_safe_proxy_key(proxy_key)}.next_allowed"
    tmp_path = path.with_suffix(".tmp")
    tmp_path.write_text(f"{float(value):.6f}", encoding="utf-8")
    tmp_path.replace(path)


@dataclass
class AsyncProxyRateLimiter:
    min_gap_seconds: float
    jitter_seconds: float = 0.0
    monotonic: MonotonicFunc = time.monotonic
    wall_clock: WallClockFunc = time.time
    sleep: SleepFunc = asyncio.sleep
    state_dir: Path | None = None
    _locks: dict[str, asyncio.Lock] = field(default_factory=dict)
    _next_allowed_at: dict[str, float] = field(default_factory=dict)

    async def wait(self, proxy_key: str) -> None:
        key = (proxy_key or "direct").strip() or "direct"
        lock = self._locks.setdefault(key, asyncio.Lock())
        async with lock:
            persisted_next_allowed = _read_persisted_next_allowed(self.state_dir, key)
            if persisted_next_allowed is not None:
                persisted_wait = max(0.0, persisted_next_allowed - float(self.wall_clock()))
                if persisted_wait > 0:
                    await self.sleep(persisted_wait)

            now = float(self.monotonic())
            next_allowed = float(self._next_allowed_at.get(key, now))
            wait_seconds = max(0.0, next_allowed - now)
            if wait_seconds > 0:
                await self.sleep(wait_seconds)
                now = float(self.monotonic())

            jitter = random.uniform(0.0, max(0.0, float(self.jitter_seconds)))
            next_allowed_value = max(0.0, float(self.min_gap_seconds)) + jitter
            self._next_allowed_at[key] = now + next_allowed_value
            _write_persisted_next_allowed(self.state_dir, key, float(self.wall_clock()) + next_allowed_value)


@dataclass
class SyncProxyRateLimiter:
    min_gap_seconds: float
    jitter_seconds: float = 0.0
    monotonic: MonotonicFunc = time.monotonic
    wall_clock: WallClockFunc = time.time
    sleep: Callable[[float], None] = time.sleep
    state_dir: Path | None = None
    _locks: dict[str, threading.Lock] = field(default_factory=dict)
    _next_allowed_at: dict[str, float] = field(default_factory=dict)
    _guard: threading.Lock = field(default_factory=threading.Lock)

    def wait(self, proxy_key: str | None) -> None:
        key = (proxy_key or "direct").strip() or "direct"
        with self._lock_for(key):
            persisted_next_allowed = _read_persisted_next_allowed(self.state_dir, key)
            if persisted_next_allowed is not None:
                persisted_wait = max(0.0, persisted_next_allowed - float(self.wall_clock()))
                if persisted_wait > 0:
                    self.sleep(persisted_wait)

            now = float(self.monotonic())
            next_allowed = float(self._next_allowed_at.get(key, now))
            wait_seconds = max(0.0, next_allowed - now)
            if wait_seconds > 0:
                self.sleep(wait_seconds)
                now = float(self.monotonic())

            jitter = random.uniform(0.0, max(0.0, float(self.jitter_seconds)))
            next_allowed_value = max(0.0, float(self.min_gap_seconds)) + jitter
            self._next_allowed_at[key] = now + next_allowed_value
            _write_persisted_next_allowed(self.state_dir, key, float(self.wall_clock()) + next_allowed_value)

    def defer(self, proxy_key: str | None, seconds: float) -> None:
        key = (proxy_key or "direct").strip() or "direct"
        delay = max(0.0, float(seconds))
        if delay <= 0:
            return

        with self._lock_for(key):
            now = float(self.monotonic())
            wall_now = float(self.wall_clock())
            self._next_allowed_at[key] = max(
                float(self._next_allowed_at.get(key, now)),
                now + delay,
            )
            current_persisted = _read_persisted_next_allowed(self.state_dir, key)
            next_wall_clock = max(float(current_persisted or wall_now), wall_now + delay)
            _write_persisted_next_allowed(self.state_dir, key, next_wall_clock)

    def _lock_for(self, key: str) -> threading.Lock:
        with self._guard:
            lock = self._locks.get(key)
            if lock is None:
                lock = threading.Lock()
                self._locks[key] = lock
            return lock


_GLOBAL_LIMITER: AsyncProxyRateLimiter | None = None
_GLOBAL_SYNC_LIMITER: SyncProxyRateLimiter | None = None
_GLOBAL_FILTER_SYNC_LIMITER: SyncProxyRateLimiter | None = None


def _env_float(name: str, default: float) -> float:
    value = os.environ.get(name)
    if not value or not value.strip():
        return default
    try:
        return float(value)
    except ValueError:
        return default


def _env_state_dir() -> Path | None:
    value = os.environ.get("PARSER_RATE_LIMIT_STATE_DIR")
    if not value or not value.strip():
        return None
    return Path(value.strip())


def global_proxy_rate_limiter() -> AsyncProxyRateLimiter:
    global _GLOBAL_LIMITER
    if _GLOBAL_LIMITER is None:
        _GLOBAL_LIMITER = AsyncProxyRateLimiter(
            min_gap_seconds=_env_float("PARSER_PROXY_MIN_REQUEST_GAP_SECONDS", 2.0),
            jitter_seconds=_env_float("PARSER_PROXY_REQUEST_JITTER_SECONDS", 1.0),
            state_dir=_env_state_dir(),
        )
    return _GLOBAL_LIMITER


def global_sync_proxy_rate_limiter() -> SyncProxyRateLimiter:
    global _GLOBAL_SYNC_LIMITER
    if _GLOBAL_SYNC_LIMITER is None:
        _GLOBAL_SYNC_LIMITER = SyncProxyRateLimiter(
            min_gap_seconds=_env_float("PARSER_PROXY_MIN_REQUEST_GAP_SECONDS", 2.0),
            jitter_seconds=_env_float("PARSER_PROXY_REQUEST_JITTER_SECONDS", 1.0),
            state_dir=_env_state_dir(),
        )
    return _GLOBAL_SYNC_LIMITER


def global_filter_sync_proxy_rate_limiter() -> SyncProxyRateLimiter:
    global _GLOBAL_FILTER_SYNC_LIMITER
    if _GLOBAL_FILTER_SYNC_LIMITER is None:
        _GLOBAL_FILTER_SYNC_LIMITER = SyncProxyRateLimiter(
            min_gap_seconds=_env_float("PARSER_FILTER_MIN_REQUEST_GAP_SECONDS", 15.0),
            jitter_seconds=_env_float("PARSER_FILTER_JITTER_SECONDS", 5.0),
            state_dir=_env_state_dir(),
        )
    return _GLOBAL_FILTER_SYNC_LIMITER


def reset_global_proxy_rate_limiter() -> None:
    global _GLOBAL_LIMITER
    global _GLOBAL_SYNC_LIMITER
    global _GLOBAL_FILTER_SYNC_LIMITER
    _GLOBAL_LIMITER = None
    _GLOBAL_SYNC_LIMITER = None
    _GLOBAL_FILTER_SYNC_LIMITER = None
