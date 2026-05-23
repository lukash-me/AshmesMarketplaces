import requests
from typing import List
from loguru import logger

headers = {
    'sec-ch-ua-platform': '"Windows"',
    'Referer': 'https://www.wildberries.ru/',
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36',
    'sec-ch-ua': '"Google Chrome";v="147", "Not.A/Brand";v="8", "Chromium";v="147"',
    'sec-ch-ua-mobile': '?0',
}

class CategoriesParser:

    URL = "https://static-basket-01.wbbasket.ru/vol0/data/main-menu-ru-ru-v3.json"

    def __init__(self, cookies: dict = None) -> List:
        self.cookies = cookies
        self.result = []


    def fetch(self, categories: List = []):
        response = requests.get(self.URL)
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


    def dfs(self, node: dict, target_names: set = None, in_target_category: bool = False):

        current_in_target = in_target_category or (
                self.matches_target(node, target_names)
        )

        childs = node.get("childs")

        if not childs:
            if current_in_target and node.get("searchQuery"):
                self.result.append({
                    "id": node.get("id"),
                    "name": node.get("seo"),
                    "searchQuery": node.get("searchQuery")
                })
            return

        for child in childs:
            self.dfs(child, target_names, current_in_target)


    # Если не задавать target_names, будут получены адреса для всех возможных категорий маркетплейса
    # Задавать необходимые категории в формате ["Кат_1", "Кат_2"]
    def parse(self, target_names: List[str] = None):
        data = self.fetch()

        target_set = {str(name).casefold() for name in target_names} if target_names else None

        for node in data:
            if not target_names:
                self.dfs(node, target_set, in_target_category=True)
            else:
                self.dfs(node, target_set, in_target_category=False)

        logger.info("Получены категории")
        logger.debug(self.result)

        return self.result


if __name__ == "__main__":
    CategoriesParser().parse(["Обувь"])
