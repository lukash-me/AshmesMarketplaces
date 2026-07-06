from __future__ import annotations

import base64
import socket
import ssl
import time
from dataclasses import dataclass
from typing import Callable
from urllib.parse import urlsplit

try:
    from common_data import HEADERS
except ImportError:  # pragma: no cover
    HEADERS = {
        "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/147 Safari/537.36"
    }

try:
    from app.proxy_mapping import ProxyDefinition
except ImportError:  # pragma: no cover
    from proxy_mapping import ProxyDefinition


WB_HOST = "www.wildberries.ru"
WB_PORT = 443
SUCCESS_HTTP_STATUSES = {200, 301, 302}


SocketFactory = Callable[[tuple[str, int], float], socket.socket]
SslContextFactory = Callable[[], ssl.SSLContext]
Clock = Callable[[], float]


@dataclass(frozen=True)
class WbProxyPreflightResult:
    proxy_key: str
    connect_status: int
    http_status: int
    tls_established: bool
    server: str | None
    x_wbaas_token: str | None
    elapsed_ms: int
    error: str | None = None

    @property
    def is_success(self) -> bool:
        if self.http_status == 0:
            return self.connect_status == 200 and self.tls_established and not self.error
        return self.connect_status == 200 and self.http_status in SUCCESS_HTTP_STATUSES and not self.error

    @property
    def is_transport_success(self) -> bool:
        return self.connect_status == 200 and self.tls_established and not self.error

    def diagnostic_message(self) -> str:
        parts = [
            "WB preflight failed" if not self.is_success else "WB preflight ok",
            f"CONNECT={_status_text(self.connect_status)}",
            f"TLS={'ok' if self.tls_established else 'failed'}",
            f"HTTP={_status_text(self.http_status)}",
        ]
        if self.server:
            parts.append(f"server={self.server}")
        if self.x_wbaas_token:
            parts.append(f"x-wbaas-token={self.x_wbaas_token}")
        if self.error:
            parts.append(f"error={self.error}")
        return " ".join(parts)


def run_wb_proxy_preflight(
    proxy: ProxyDefinition,
    *,
    timeout_seconds: float = 10.0,
    socket_factory: SocketFactory = socket.create_connection,
    ssl_context_factory: SslContextFactory = ssl.create_default_context,
    monotonic: Clock = time.monotonic,
) -> WbProxyPreflightResult:
    return _run_transport_preflight(
        proxy,
        timeout_seconds=timeout_seconds,
        socket_factory=socket_factory,
        ssl_context_factory=ssl_context_factory,
        monotonic=monotonic,
    )


def _run_transport_preflight(
    proxy: ProxyDefinition,
    *,
    timeout_seconds: float,
    socket_factory: SocketFactory,
    ssl_context_factory: SslContextFactory,
    monotonic: Clock,
) -> WbProxyPreflightResult:
    started = monotonic()
    raw_socket = None
    tls_socket = None
    connect_status = 0
    try:
        proxy_host, proxy_port = _proxy_host_port(proxy)
        raw_socket = socket_factory((proxy_host, proxy_port), timeout_seconds)
        raw_socket.settimeout(timeout_seconds)
        raw_socket.sendall(_connect_request(proxy).encode("ascii"))
        connect_headers = _read_headers(raw_socket)
        connect_status, _ = _parse_status_and_headers(connect_headers)
        if connect_status != 200:
            return _result(proxy, started, monotonic, connect_status, 0, False, None, None, None)

        context = ssl_context_factory()
        tls_socket = context.wrap_socket(raw_socket, server_hostname=WB_HOST)
        tls_socket.settimeout(timeout_seconds)
        return _result(proxy, started, monotonic, connect_status, 0, True, None, None, None)
    except Exception as exception:
        return _result(proxy, started, monotonic, connect_status, 0, False, None, None, _safe_error(exception))
    finally:
        for candidate in (tls_socket, raw_socket):
            try:
                if candidate is not None:
                    candidate.close()
            except Exception:
                pass


def run_wb_authenticated_preflight(
    proxy: ProxyDefinition,
    *,
    cookies: dict[str, str] | None,
    user_agent: str | None = None,
    timeout_seconds: float = 10.0,
    socket_factory: SocketFactory = socket.create_connection,
    ssl_context_factory: SslContextFactory = ssl.create_default_context,
    monotonic: Clock = time.monotonic,
) -> WbProxyPreflightResult:
    return _run_wb_preflight(
        proxy,
        cookies=cookies,
        user_agent=user_agent,
        reject_http_error=True,
        timeout_seconds=timeout_seconds,
        socket_factory=socket_factory,
        ssl_context_factory=ssl_context_factory,
        monotonic=monotonic,
    )


def _run_wb_preflight(
    proxy: ProxyDefinition,
    *,
    cookies: dict[str, str] | None,
    user_agent: str | None,
    reject_http_error: bool,
    timeout_seconds: float,
    socket_factory: SocketFactory,
    ssl_context_factory: SslContextFactory,
    monotonic: Clock,
) -> WbProxyPreflightResult:
    started = monotonic()
    raw_socket = None
    tls_socket = None
    try:
        proxy_host, proxy_port = _proxy_host_port(proxy)
        raw_socket = socket_factory((proxy_host, proxy_port), timeout_seconds)
        raw_socket.settimeout(timeout_seconds)
        raw_socket.sendall(_connect_request(proxy).encode("ascii"))
        connect_headers = _read_headers(raw_socket)
        connect_status, _ = _parse_status_and_headers(connect_headers)
        if connect_status != 200:
            return _result(proxy, started, monotonic, connect_status, 0, False, None, None, None)

        context = ssl_context_factory()
        tls_socket = context.wrap_socket(raw_socket, server_hostname=WB_HOST)
        tls_socket.settimeout(timeout_seconds)
        tls_socket.sendall(_wb_get_request(cookies, user_agent=user_agent).encode("utf-8"))
        response_headers = _read_headers(tls_socket)
        http_status, headers = _parse_status_and_headers(response_headers)
        server = headers.get("server")
        x_wbaas_token = headers.get("x-wbaas-token")
        error = None
        if reject_http_error and http_status not in SUCCESS_HTTP_STATUSES:
            error = "Wildberries rejected authenticated session"
        return _result(proxy, started, monotonic, connect_status, http_status, True, server, x_wbaas_token, error)
    except Exception as exception:
        return _result(proxy, started, monotonic, 0, 0, False, None, None, _safe_error(exception))
    finally:
        for candidate in (tls_socket, raw_socket):
            try:
                if candidate is not None:
                    candidate.close()
            except Exception:
                pass


def _proxy_host_port(proxy: ProxyDefinition) -> tuple[str, int]:
    if proxy.type != "http-proxy" or not proxy.base_url:
        raise ValueError(f"Proxy {proxy.key} must be an HTTP proxy for WB preflight.")

    parsed = urlsplit(proxy.base_url)
    if not parsed.hostname or not parsed.port:
        raise ValueError(f"Proxy {proxy.key} has invalid baseUrl for WB preflight.")
    return parsed.hostname, int(parsed.port)


def _connect_request(proxy: ProxyDefinition) -> str:
    lines = [
        f"CONNECT {WB_HOST}:{WB_PORT} HTTP/1.1",
        f"Host: {WB_HOST}:{WB_PORT}",
        "Proxy-Connection: Keep-Alive",
    ]
    credentials = proxy.credentials or {}
    login = str(credentials.get("login") or credentials.get("username") or "").strip()
    password = str(credentials.get("password") or "").strip()
    if login or password:
        token = base64.b64encode(f"{login}:{password}".encode("utf-8")).decode("ascii")
        lines.append(f"Proxy-Authorization: Basic {token}")
    return "\r\n".join(lines) + "\r\n\r\n"


def _wb_get_request(cookies: dict[str, str] | None = None, *, user_agent: str | None = None) -> str:
    effective_user_agent = (user_agent or HEADERS.get("user-agent") or "").strip()
    lines = [
        "GET / HTTP/1.1",
        f"Host: {WB_HOST}",
        f"User-Agent: {effective_user_agent}",
        "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7",
    ]

    cookie_header = _cookie_header(cookies)
    if cookie_header:
        lines.append(f"Cookie: {cookie_header}")
    token = (cookies or {}).get("x_wbaas_token")
    if token:
        lines.append(f"x_wbaas_token: {token}")

    lines.append("Connection: close")
    return "\r\n".join(lines) + "\r\n\r\n"


def _cookie_header(cookies: dict[str, str] | None) -> str:
    if not cookies:
        return ""
    parts: list[str] = []
    for name, value in cookies.items():
        clean_name = str(name).replace("\r", "").replace("\n", "").strip()
        clean_value = str(value).replace("\r", "").replace("\n", "").strip()
        if clean_name and clean_value:
            parts.append(f"{clean_name}={clean_value}")
    return "; ".join(parts)


def _read_headers(sock: socket.socket, *, limit: int = 65536) -> str:
    data = b""
    while b"\r\n\r\n" not in data and len(data) < limit:
        chunk = sock.recv(4096)
        if not chunk:
            break
        data += chunk
    return data.decode("iso-8859-1", errors="replace")


def _parse_status_and_headers(raw_headers: str) -> tuple[int, dict[str, str]]:
    lines = raw_headers.splitlines()
    if not lines:
        return 0, {}

    status_parts = lines[0].split(" ", 2)
    status = int(status_parts[1]) if len(status_parts) > 1 and status_parts[1].isdigit() else 0
    headers: dict[str, str] = {}
    for line in lines[1:]:
        if not line or ":" not in line:
            continue
        key, value = line.split(":", 1)
        headers[key.strip().lower()] = value.strip()
    return status, headers


def _result(
    proxy: ProxyDefinition,
    started: float,
    monotonic: Clock,
    connect_status: int,
    http_status: int,
    tls_established: bool,
    server: str | None,
    x_wbaas_token: str | None,
    error: str | None,
) -> WbProxyPreflightResult:
    elapsed_ms = max(0, int(round((monotonic() - started) * 1000)))
    return WbProxyPreflightResult(
        proxy_key=proxy.key,
        connect_status=connect_status,
        http_status=http_status,
        tls_established=tls_established,
        server=server,
        x_wbaas_token=x_wbaas_token,
        elapsed_ms=elapsed_ms,
        error=error,
    )


def _status_text(status: int) -> str:
    return f"{status:03d}" if status <= 0 else str(status)


def _safe_error(exception: Exception) -> str:
    return str(exception).replace("\r", " ").replace("\n", " ")
