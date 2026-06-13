from typing import List

import requests
from loguru import logger


headers = {
    "sec-ch-ua-platform": '"Windows"',
    "Referer": "https://www.wildberries.ru/",
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/147.0.0.0 Safari/537.36"
    ),
    "sec-ch-ua": '"Google Chrome";v="147", "Not.A/Brand";v="8", "Chromium";v="147"',
    "sec-ch-ua-mobile": "?0",
}


HOME_GOODS_FALLBACK_CATEGORIES = [
    {
        "id": 62514,
        "name": "Органайзеры для хранения вещей",
        "searchQuery": "menu_redirect_subject_v2_62514 органайзеры для хранения",
        "parent": "Товары для дома",
    },
    {
        "id": 261,
        "name": "Коврики для ванной",
        "searchQuery": "menu_v3_261 коврики для ванной",
        "parent": "Товары для дома",
    },
    {
        "id": 130194,
        "name": "Светильники бра",
        "searchQuery": "menu_v3_130194 бра",
        "parent": "Товары для дома",
    },
]


class CategoriesParser:
    URL = "https://static-basket-01.wbbasket.ru/vol0/data/main-menu-ru-ru-v3.json"

    def __init__(self, cookies: dict = None) -> None:
        self.cookies = cookies
        self.result = []

    def fetch(self, categories: List = []) -> list[dict]:
        response = requests.get(self.URL, headers=headers, cookies=self.cookies, timeout=20)
        response.raise_for_status()
        return response.json()

    def is_leaf(self, node: dict) -> bool:
        return not node.get("childs")

    @staticmethod
    def matches_target(node: dict, target_names: set[str] | None) -> bool:
        if not target_names:
            return False

        labels = {
            str(node.get("name") or "").casefold(),
            str(node.get("seo") or "").casefold(),
        }
        return bool(labels & target_names)

    def dfs(self, node: dict, target_names: set | None = None, in_target_category: bool = False) -> None:
        current_in_target = in_target_category or self.matches_target(node, target_names)
        childs = node.get("childs")

        if not childs:
            if current_in_target and node.get("searchQuery"):
                self.result.append(
                    {
                        "id": node.get("id"),
                        "name": node.get("seo") or node.get("name"),
                        "searchQuery": node.get("searchQuery"),
                    }
                )
            return

        for child in childs:
            self.dfs(child, target_names, current_in_target)

    def _append_fallback_categories(self, target_set: set[str] | None) -> None:
        if not target_set or self.result:
            return

        fallback_rows = [
            {
                "id": item["id"],
                "name": item["name"],
                "searchQuery": item["searchQuery"],
            }
            for item in HOME_GOODS_FALLBACK_CATEGORIES
            if str(item["parent"]).casefold() in target_set
        ]
        if fallback_rows:
            logger.warning("Static menu returned no matching categories; using home goods fallback categories.")
            self.result.extend(fallback_rows)

    def parse(self, target_names: List[str] | None = None):
        data = self.fetch()
        target_set = {str(name).casefold() for name in target_names} if target_names else None

        for node in data:
            if not target_names:
                self.dfs(node, target_set, in_target_category=True)
            else:
                self.dfs(node, target_set, in_target_category=False)

        self._append_fallback_categories(target_set)
        logger.info("Получены категории")
        logger.debug(self.result)

        return self.result


if __name__ == "__main__":
    CategoriesParser().parse(["Обувь"])
