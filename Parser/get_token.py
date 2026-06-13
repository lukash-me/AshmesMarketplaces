import time

from loguru import logger
from seleniumbase import Driver


USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
    "AppleWebKit/537.36 (KHTML, like Gecko) "
    "Chrome/147.0.0.0 Safari/537.36"
)
URL = "https://www.wildberries.ru/"
COOKIE_NEED = "x_wbaas_token"


class WebdriverCookies:
    def __init__(self, user_agent: str = None, url: str = None, cookie_need: str = None):
        self.user_agent = user_agent or USER_AGENT
        self.url = url or URL
        self.cookie_need = cookie_need or COOKIE_NEED

    def get_token(self) -> str | None:
        driver = None

        try:
            driver = Driver(
                uc=True,
                headed=True,
                agent=self.user_agent,
            )
            driver.open(self.url)

            for _ in range(6):
                cookies = driver.execute_cdp_cmd("Network.getAllCookies", {})
                logger.debug(cookies)
                for cookie in cookies.get("cookies", []):
                    if cookie.get("name") == self.cookie_need:
                        logger.success("Куки получены")
                        return cookie.get("value")
                time.sleep(5)

            return None
        finally:
            if driver is not None:
                driver.quit()


def get_token() -> str | None:
    return WebdriverCookies().get_token()


if __name__ == "__main__":
    token = WebdriverCookies().get_token()
    logger.info(token)
