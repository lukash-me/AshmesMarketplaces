from __future__ import annotations

import unittest
from copy import deepcopy
from datetime import datetime, timezone

from fastapi.testclient import TestClient

from app.main import app


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def product_payload(index: int = 0, **overrides) -> dict:
    feedback = max(0, 2600 - index * 90)
    payload = {
        "productKey": f"wildberries:{202825367 + index}",
        "wbProductId": 202825367 + index,
        "wbRootId": 119691805 + index,
        "name": f"Бра светильник настенный {index}",
        "brandName": "Семь огней",
        "sellerName": "СЕМЬ ОГНЕЙ",
        "sourceCategory": "Товары для дома",
        "sourceSubcategory": "Светильники бра",
        "price": 720 + index * 8,
        "priceWithoutDiscount": 1220 + index * 10,
        "walletPrice": 700 + index * 8,
        "rating": max(3.8, 4.95 - index * 0.025),
        "feedbackCount": feedback,
        "parsedReviewCount": max(0, feedback // 3),
        "parsedReplyCount": max(0, feedback // 4),
        "position": index + 1,
        "positionState": "observed",
        "observedRangeLimit": 100,
        "totalQuantity": 40 if index == 0 else 12 + index,
        "snapshotAtUtc": "2026-05-25T00:00:00Z",
    }
    payload.update(overrides)
    return payload


def fixture_products(count: int = 24) -> list[dict]:
    return [product_payload(index) for index in range(count)]


def hot_products_payload(products: list[dict], options: dict | None = None) -> dict:
    return {
        "requestId": "req-hot-1",
        "generatedAtUtc": now_iso(),
        "marketplace": "wildberries",
        "scope": {
            "sourceCategory": "Товары для дома",
            "sourceSubcategories": ["Светильники бра"],
            "parserRunId": "wb_products_test",
            "rankRunId": "wb_rank_test",
            "reviewRunIds": ["wb_reviews_test"],
        },
        "products": products,
        "options": options
        if options is not None
        else {
            "maxRecommendations": 10,
            "algorithm": None,
        },
    }


class RecommendationContractTests(unittest.TestCase):
    def setUp(self) -> None:
        self.client = TestClient(app)

    def post_hot_products(self, products: list[dict], options: dict | None = None):
        return self.client.post(
            "/api/v1/recommendations/hot-products",
            json=hot_products_payload(products, options),
        )

    def test_hot_products_empty_snapshot_returns_not_enough_data(self) -> None:
        response = self.post_hot_products([])

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["requestId"], "req-hot-1")
        self.assertEqual(payload["status"], "not_enough_data")
        self.assertEqual(payload["recommendations"], [])
        self.assertEqual(payload["algorithm"], "rule_based_hot_products_v1")
        self.assertEqual(payload["algorithmVersion"], "1.0.0")
        self.assertEqual(payload["modelVersion"], "none")

    def test_hot_products_below_min_products_returns_not_enough_data(self) -> None:
        response = self.post_hot_products(fixture_products(6))

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["status"], "not_enough_data")
        self.assertEqual(payload["recommendations"], [])
        self.assertTrue(payload["warnings"])

    def test_hot_products_below_five_eligible_rows_returns_not_enough_data(self) -> None:
        products = fixture_products(20)
        for index in range(4, len(products)):
            products[index]["productKey"] = None
            products[index]["wbProductId"] = None
            products[index]["sourceCategory"] = None

        response = self.post_hot_products(products, {"minProductsForScoring": 5})

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["status"], "not_enough_data")
        self.assertIn("eligible", payload["warnings"][0])

    def test_hot_products_sufficient_snapshot_returns_completed_candidates(self) -> None:
        response = self.post_hot_products(fixture_products(24))

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        recommendations = payload["recommendations"]
        self.assertEqual(payload["status"], "completed")
        self.assertEqual(payload["algorithm"], "rule_based_hot_products_v1")
        self.assertEqual(payload["algorithmVersion"], "1.0.0")
        self.assertLessEqual(len(recommendations), 10)
        self.assertGreater(len(recommendations), 0)
        self.assertEqual(
            [item["score"] for item in recommendations],
            sorted([item["score"] for item in recommendations], reverse=True),
        )
        for item in recommendations:
            self.assertGreaterEqual(item["score"], 0)
            self.assertLessEqual(item["score"], 100)
            self.assertGreaterEqual(item["confidence"], 0)
            self.assertLessEqual(item["confidence"], 1)
            self.assertGreaterEqual(item["score"], 60)
            self.assertGreaterEqual(item["confidence"], 0.45)

    def test_unknown_algorithm_returns_unsupported(self) -> None:
        response = self.post_hot_products(
            fixture_products(24),
            {"algorithm": "unknown_algorithm", "maxRecommendations": 10},
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["status"], "unsupported")
        self.assertEqual(payload["algorithm"], "unknown_algorithm")
        self.assertEqual(payload["recommendations"], [])
        self.assertTrue(payload["warnings"])

    def test_min_confidence_filters_low_confidence_recommendations(self) -> None:
        products = fixture_products(24)
        default_response = self.post_hot_products(products, {"maxRecommendations": 20})
        strict_response = self.post_hot_products(products, {"maxRecommendations": 20, "minConfidence": 0.95})

        self.assertEqual(default_response.status_code, 200)
        self.assertEqual(strict_response.status_code, 200)
        default_items = default_response.json()["recommendations"]
        strict_items = strict_response.json()["recommendations"]
        self.assertGreater(len(default_items), len(strict_items))
        for item in strict_items:
            self.assertGreaterEqual(item["confidence"], 0.95)

    def test_input_snapshot_hash_is_deterministic(self) -> None:
        products = fixture_products(24)
        first = self.post_hot_products(deepcopy(products)).json()["recommendations"][0]
        second = self.post_hot_products(deepcopy(products)).json()["recommendations"][0]

        self.assertEqual(first["productKey"], second["productKey"])
        self.assertEqual(first["inputSnapshotHash"], second["inputSnapshotHash"])
        self.assertEqual(first["recommendationKey"], second["recommendationKey"])

    def test_debug_is_null_by_default_and_present_when_requested(self) -> None:
        default_payload = self.post_hot_products(fixture_products(24)).json()
        debug_payload = self.post_hot_products(
            fixture_products(24),
            {"maxRecommendations": 1, "includeDebug": True},
        ).json()

        self.assertIsNone(default_payload["recommendations"][0]["debug"])
        self.assertIsNotNone(debug_payload["recommendations"][0]["debug"])
        self.assertNotIn("products", debug_payload["recommendations"][0]["debug"])
        self.assertNotIn("raw", debug_payload["recommendations"][0]["debug"])

    def test_factor_labels_are_seller_friendly_and_reason_is_conservative(self) -> None:
        payload = self.post_hot_products(fixture_products(24)).json()
        banned = [
            "parser",
            "staged",
            "root",
            "payload",
            "model",
            "доказывает",
            "гарантирует",
            "будет продаваться",
            "спрос",
            "прибыль",
            "прогноз",
        ]
        allowed_codes = {
            "high_position_weak_card",
            "high_position_weak_reviews",
            "bad_recent_reviews",
            "expensive_without_advantage",
            "fast_position_growth",
            "top_low_stock",
            "duplicate_cards",
            "repeated_review_complaint",
            "weak_visible_description",
            "weak_description",
            "missing_key_specs",
            "low_review_count_top_position",
            "good_reviews_weak_visibility",
        }

        for recommendation in payload["recommendations"]:
            text = f"{recommendation['title']} {recommendation['reason']}".lower()
            self.assertIn("подборка", text)
            self.assertIn("проверьте", text)
            for word in banned:
                self.assertNotIn(word, text)

            codes = {factor["code"] for factor in recommendation["factors"]}
            self.assertTrue(codes)
            self.assertTrue(codes.issubset(allowed_codes))
            for factor in recommendation["factors"]:
                factor_text = f"{factor['label']} {factor['value']}".lower()
                for word in banned:
                    self.assertNotIn(word, factor_text)

    def test_zero_review_rating_without_negative_signal_is_not_bad_recent_review(self) -> None:
        products = fixture_products(24)
        products[0]["reviewSignals"] = {
            "parsedReviewCount": 1,
            "parsedReplyCount": 0,
            "ratedReviewCount": 1,
            "averageRating": 0,
            "lowRatingReviewCount": 0,
            "negativeTextReviewCount": 0,
            "latestReviewRunId": "wb_reviews_test",
        }

        payload = self.post_hot_products(products, {"maxRecommendations": 24}).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        codes = {factor["code"] for factor in target["factors"]}

        self.assertNotIn("bad_recent_reviews", codes)

    def test_empty_positive_review_text_is_not_bad_recent_review(self) -> None:
        products = fixture_products(24)
        products[0]["reviewSignals"] = {
            "parsedReviewCount": 1,
            "parsedReplyCount": 0,
            "ratedReviewCount": 1,
            "averageRating": 4,
            "lowRatingReviewCount": 0,
            "negativeTextReviewCount": 0,
            "latestReviewRunId": "wb_reviews_test",
        }

        payload = self.post_hot_products(products, {"maxRecommendations": 24}).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        codes = {factor["code"] for factor in target["factors"]}

        self.assertNotIn("bad_recent_reviews", codes)

    def test_low_rating_review_signal_creates_bad_recent_review_factor(self) -> None:
        products = fixture_products(24)
        products[0]["reviewSignals"] = {
            "parsedReviewCount": 5,
            "parsedReplyCount": 0,
            "ratedReviewCount": 5,
            "averageRating": 2.6,
            "lowRatingReviewCount": 3,
            "negativeTextReviewCount": 0,
            "latestReviewRunId": "wb_reviews_test",
        }

        payload = self.post_hot_products(products, {"maxRecommendations": 24}).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        codes = {factor["code"] for factor in target["factors"]}

        self.assertIn("bad_recent_reviews", codes)

    def test_low_review_count_top_position_uses_peer_cluster_comparison(self) -> None:
        products = fixture_products(24)
        products[0].update({
            "feedbackCount": 9,
            "parsedReviewCount": 3,
            "position": 5,
            "price": 760,
            "walletPrice": 740,
            "rating": 4.8,
        })
        for index in range(1, 12):
            products[index].update({
                "feedbackCount": 140 + index * 10,
                "parsedReviewCount": 80 + index * 5,
                "price": 730 + index * 5,
                "walletPrice": 710 + index * 5,
                "position": 6 + index,
                "rating": 4.7,
            })

        payload = self.post_hot_products(
            products,
            {"maxRecommendations": 24, "minProductsForScoring": 5},
        ).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        factor = next(
            factor for factor in target["factors"]
            if factor["code"] == "low_review_count_top_position"
        )

        self.assertEqual(factor["value"]["reviewCount"], 9)
        self.assertGreaterEqual(factor["value"]["peerMedianReviewCount"], 100)
        self.assertGreaterEqual(factor["value"]["peerSampleSize"], 5)
        self.assertIn("против", factor["value"]["label"])

    def test_low_review_count_top_position_requires_enough_peers(self) -> None:
        products = fixture_products(6)
        products[0].update({
            "feedbackCount": 9,
            "parsedReviewCount": 3,
            "position": 5,
            "price": 760,
            "walletPrice": 740,
            "rating": 4.8,
        })
        for index in range(1, 5):
            products[index].update({
                "feedbackCount": 160,
                "parsedReviewCount": 90,
                "price": 730 + index * 5,
                "walletPrice": 710 + index * 5,
                "position": 6 + index,
                "rating": 4.7,
            })
        products[5]["price"] = 3000
        products[5]["walletPrice"] = 3000

        payload = self.post_hot_products(
            products,
            {"maxRecommendations": 6, "minProductsForScoring": 5},
        ).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        codes = {factor["code"] for factor in target["factors"]}

        self.assertNotIn("low_review_count_top_position", codes)

    def test_unidentified_products_are_not_recommended(self) -> None:
        products = fixture_products(24)
        products[0]["productKey"] = None
        products[0]["wbProductId"] = None
        products[0]["position"] = 1
        products[0]["rating"] = 5
        products[0]["feedbackCount"] = 99999

        payload = self.post_hot_products(products).json()
        ids = {item["wbProductId"] for item in payload["recommendations"]}
        self.assertNotIn(None, ids)
        self.assertNotIn("202825367", ids)

    def test_capped_quantity_does_not_create_high_stock_claim(self) -> None:
        payload = self.post_hot_products(fixture_products(24)).json()
        serialized = str(payload).lower()

        self.assertNotIn("высокий остаток", serialized)
        self.assertNotIn("high stock", serialized)
        self.assertNotIn("stock advantage", serialized)

    def test_product_advice_returns_request_id_and_no_debug_by_default(self) -> None:
        response = self.client.post(
            "/api/v1/recommendations/product-advice",
            json={
                "requestId": "req-advice-1",
                "generatedAtUtc": now_iso(),
                "product": product_payload(),
                "question": "Стоит ли смотреть карточку?",
            },
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["requestId"], "req-advice-1")
        self.assertEqual(payload["status"], "unsupported")
        self.assertEqual(payload["advice"]["summary"], None)
        self.assertEqual(payload["advice"]["recommendations"], [])
        self.assertIsNone(payload["advice"]["debug"])

    def test_product_advice_missing_identity_returns_not_enough_data(self) -> None:
        product = product_payload()
        product["productKey"] = None
        product["wbProductId"] = None
        product["wbRootId"] = None
        response = self.client.post(
            "/api/v1/recommendations/product-advice",
            json={
                "requestId": "req-advice-2",
                "generatedAtUtc": now_iso(),
                "product": product,
            },
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["requestId"], "req-advice-2")
        self.assertEqual(payload["status"], "not_enough_data")
        self.assertEqual(payload["advice"]["recommendations"], [])

    def test_workspace_product_analysis_returns_signals_and_similar_products(self) -> None:
        product = product_payload(
            0,
            position=8,
            rating=4.2,
            feedbackCount=12,
            totalQuantity=3,
            price=1900,
            walletPrice=1800,
        )
        candidates = fixture_products(8)
        response = self.client.post(
            "/api/v1/recommendations/workspace-product-analysis",
            json={
                "requestId": "req-workspace-analysis-1",
                "generatedAtUtc": now_iso(),
                "marketplace": "wildberries",
                "product": product,
                "history": {
                    "priceObservations": [
                        {"observedAtUtc": now_iso(), "value": 1800},
                        {"observedAtUtc": now_iso(), "value": 1600},
                    ],
                    "positionObservations": [
                        {"observedAtUtc": now_iso(), "value": 8},
                        {"observedAtUtc": now_iso(), "value": 14},
                    ],
                    "stockObservations": [
                        {"observedAtUtc": now_iso(), "value": 3},
                        {"observedAtUtc": now_iso(), "value": 9},
                    ],
                    "feedbackObservations": [
                        {"observedAtUtc": now_iso(), "value": 12},
                        {"observedAtUtc": now_iso(), "value": 10},
                    ],
                },
                "candidates": candidates,
                "options": {"maxSimilarProducts": 3},
            },
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["requestId"], "req-workspace-analysis-1")
        self.assertEqual(payload["status"], "completed")
        self.assertEqual(payload["algorithm"], "workspace_product_analysis_v1")
        self.assertGreater(len(payload["signals"]), 0)
        self.assertGreater(len(payload["similarProducts"]), 0)
        self.assertGreater(len(payload["similarProductGroups"]), 0)
        first_group = payload["similarProductGroups"][0]
        self.assertIn("key", first_group)
        self.assertIn("title", first_group)
        self.assertIn("items", first_group)
        self.assertGreater(len(first_group["items"]), 0)
        self.assertIn("productKey", first_group["items"][0])
        self.assertIn("facts", first_group["items"][0])
        group_titles = {group["key"]: group["title"] for group in payload["similarProductGroups"]}
        self.assertEqual(group_titles.get("price_disadvantage"), "Дешевле")
        self.assertEqual(group_titles.get("position_disadvantage"), "Выше в выдаче")
        self.assertNotIn("Цена хуже", group_titles.values())
        self.assertNotIn("Позиция хуже", group_titles.values())

        text = str(payload).lower()
        for word in ("demand", "profit", "sales", "forecast", "risk"):
            self.assertNotIn(word, text)

    def test_product_advice_job_generates_job_id_and_request_id_when_missing(self) -> None:
        response = self.client.post(
            "/api/v1/jobs/product-advice",
            json={
                "generatedAtUtc": now_iso(),
                "product": product_payload(),
            },
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertTrue(payload["jobId"].startswith("job_"))
        self.assertTrue(payload["requestId"].startswith("req_"))
        self.assertEqual(payload["status"], "completed")
        self.assertEqual(payload["result"]["requestId"], payload["requestId"])

        status_response = self.client.get(f"/api/v1/jobs/{payload['jobId']}")
        self.assertEqual(status_response.status_code, 200)
        self.assertEqual(status_response.json()["jobId"], payload["jobId"])

    def test_validation_error_uses_service_error_shape_and_request_id_when_available(self) -> None:
        response = self.client.post(
            "/api/v1/recommendations/hot-products",
            json={
                "requestId": "req-invalid-1",
                "marketplace": "wildberries",
                "scope": {},
                "products": [],
            },
        )

        self.assertEqual(response.status_code, 422)
        payload = response.json()
        self.assertEqual(payload["errorCode"], "validation_error")
        self.assertEqual(payload["requestId"], "req-invalid-1")
        self.assertIn("traceId", payload)
        self.assertIn("details", payload)
        self.assertIn("errors", payload["details"])


if __name__ == "__main__":
    unittest.main()
