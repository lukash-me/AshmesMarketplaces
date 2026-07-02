HEADERS = {
    "accept": "*/*",
    "accept-language": "ru-RU,ru;q=0.9,en;q=0.8",
    "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36",
    "x-requested-with": "XMLHttpRequest",
}


def headers_with_wbaas_token(cookies: dict | None) -> dict:
    headers = dict(HEADERS)
    token = (cookies or {}).get("x_wbaas_token")
    if token:
        headers["x_wbaas_token"] = str(token)
    return headers
