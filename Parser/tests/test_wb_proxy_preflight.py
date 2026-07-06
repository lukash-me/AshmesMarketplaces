from __future__ import annotations

import sys
from pathlib import Path


PARSER_DIR = Path(__file__).resolve().parents[1]
if str(PARSER_DIR) not in sys.path:
    sys.path.insert(0, str(PARSER_DIR))

from app.proxy_mapping import ProxyDefinition  # noqa: E402
from app.wb_proxy_preflight import run_wb_authenticated_preflight, run_wb_proxy_preflight  # noqa: E402


class _FakeSocket:
    def __init__(self, response: bytes) -> None:
        self._response = response
        self.sent = b""
        self.closed = False

    def settimeout(self, timeout: float) -> None:
        self.timeout = timeout

    def sendall(self, data: bytes) -> None:
        self.sent += data

    def recv(self, size: int) -> bytes:
        if not self._response:
            return b""
        chunk = self._response[:size]
        self._response = self._response[size:]
        return chunk

    def close(self) -> None:
        self.closed = True


class _FakeSslContext:
    def __init__(self, response: bytes) -> None:
        self.response = response
        self.server_names: list[str] = []
        self.last_socket: _FakeSocket | None = None

    def wrap_socket(self, socket: _FakeSocket, *, server_hostname: str) -> _FakeSocket:
        self.server_names.append(server_hostname)
        self.last_socket = _FakeSocket(self.response)
        return self.last_socket


def _proxy() -> ProxyDefinition:
    return ProxyDefinition(
        key="proxy-1",
        type="http-proxy",
        base_url="http://10.0.0.1:19118",
        credentials={"username": "login", "password": "secret-password"},
    )


def test_wb_proxy_preflight_checks_connect_and_tls_without_http_get() -> None:
    raw_socket = _FakeSocket(b"HTTP/1.0 200 Connection established\r\n\r\n")
    ssl_context = _FakeSslContext(
        b"HTTP/1.1 200 OK\r\nServer: nginx\r\nContent-Length: 0\r\n\r\n"
    )

    result = run_wb_proxy_preflight(
        _proxy(),
        socket_factory=lambda address, timeout: raw_socket,
        ssl_context_factory=lambda: ssl_context,
        monotonic=lambda: 10.0,
    )

    assert result.is_success is True
    assert result.connect_status == 200
    assert result.http_status == 0
    assert result.tls_established is True
    assert result.server is None
    assert result.x_wbaas_token is None
    assert ssl_context.server_names == ["www.wildberries.ru"]
    assert ssl_context.last_socket is not None
    assert ssl_context.last_socket.sent == b""
    assert "Proxy-Authorization: Basic" in raw_socket.sent.decode("ascii")
    assert "secret-password" not in result.diagnostic_message()


def test_wb_proxy_preflight_fails_on_proxy_auth_407_without_tls() -> None:
    raw_socket = _FakeSocket(b"HTTP/1.0 407 Proxy Authentication Required\r\n\r\n")
    ssl_context = _FakeSslContext(
        b"HTTP/1.1 200 OK\r\nServer: nginx\r\nContent-Length: 0\r\n\r\n"
    )

    result = run_wb_proxy_preflight(
        _proxy(),
        socket_factory=lambda address, timeout: raw_socket,
        ssl_context_factory=lambda: ssl_context,
        monotonic=lambda: 5.0,
    )

    assert result.is_success is False
    assert result.connect_status == 407
    assert result.http_status == 0
    assert "CONNECT=407 TLS=failed HTTP=000" in result.diagnostic_message()
    assert ssl_context.server_names == []


def test_authenticated_wb_preflight_sends_browser_cookies_and_token_header() -> None:
    raw_socket = _FakeSocket(b"HTTP/1.0 200 Connection established\r\n\r\n")
    ssl_context = _FakeSslContext(
        b"HTTP/1.1 200 OK\r\nServer: nginx\r\nContent-Length: 0\r\n\r\n"
    )

    result = run_wb_authenticated_preflight(
        _proxy(),
        cookies={"x_wbaas_token": "token-1", "session": "cookie-1"},
        user_agent="Mozilla/5.0 Fresh Browser UA",
        socket_factory=lambda address, timeout: raw_socket,
        ssl_context_factory=lambda: ssl_context,
        monotonic=lambda: 8.0,
    )

    assert result.is_success is True
    assert ssl_context.server_names == ["www.wildberries.ru"]
    assert ssl_context.last_socket is not None
    sent = ssl_context.last_socket.sent.decode("ascii")
    assert "secret-password" not in result.diagnostic_message()
    assert "Cookie: x_wbaas_token=token-1; session=cookie-1" in sent
    assert "x_wbaas_token: token-1" in sent
    assert "User-Agent: Mozilla/5.0 Fresh Browser UA" in sent
    assert "secret-password" not in sent


def test_wb_proxy_preflight_reports_http_000_on_timeout() -> None:
    result = run_wb_proxy_preflight(
        _proxy(),
        socket_factory=lambda address, timeout: (_ for _ in ()).throw(TimeoutError("timed out")),
        monotonic=lambda: 3.0,
    )

    assert result.is_success is False
    assert result.connect_status == 0
    assert result.http_status == 0
    assert "CONNECT=000 TLS=failed HTTP=000" in result.diagnostic_message()
    assert "timed out" in result.diagnostic_message()
