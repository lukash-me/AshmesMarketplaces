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


def delivery_destination(
    region_key: str,
    region_name: str,
    hours: int | None,
    *,
    source_type: str = "wb_warehouse",
    quantity: int = 30,
    city: str | None = None,
) -> dict:
    return {
        "regionKey": region_key,
        "regionName": region_name,
        "destinationCity": city or region_name,
        "destinationAddress": f"{city or region_name}, контрольный ПВЗ",
        "visibleDeliveryLabel": None if hours is None else ("Послезавтра" if hours <= 48 else "19 июня"),
        "visibleDeliveryDate": None if hours is None else ("2026-06-16T00:00:00Z" if hours <= 48 else "2026-06-19T00:00:00Z"),
        "deliveryHours": hours,
        "deliverySourceType": source_type,
        "totalQuantityObserved": quantity,
        "observedAtUtc": "2026-06-14T12:00:00Z",
    }


def delivery_profile(destinations: list[dict]) -> dict:
    return {"destinations": destinations}


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

    def test_hot_products_builds_clusters_once_with_max_size_30(self) -> None:
        products = [
            product_payload(
                index,
                price=1000,
                walletPrice=1000,
                priceWithoutDiscount=1200,
                position=index + 1,
                observedRangeLimit=120,
                totalQuantity=20,
            )
            for index in range(65)
        ]

        response = self.post_hot_products(products, {"maxRecommendations": 65, "includeDebug": True})

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        first_debug = payload["recommendations"][0]["debug"]
        diagnostics = first_debug["clusterDiagnosticsSummary"]

        self.assertEqual(diagnostics["totalProducts"], 65)
        self.assertEqual(diagnostics["eligibleProducts"], 65)
        self.assertEqual(diagnostics["totalClusters"], 3)
        self.assertEqual(diagnostics["maxClusterSize"], 30)
        self.assertEqual(diagnostics["clusterSizes"], [30, 30, 5])
        self.assertEqual(diagnostics["duplicateAssignments"], [])
        assigned = diagnostics["productsAssignedToClusters"]
        assigned_keys = [product_key for product_keys in assigned.values() for product_key in product_keys]
        self.assertEqual(len(assigned_keys), 65)
        self.assertEqual(len(set(assigned_keys)), 65)
        self.assertTrue(all(debugged["debug"]["clusterSize"] <= 30 for debugged in payload["recommendations"]))

    def test_hot_products_single_cluster_for_30_similar_products(self) -> None:
        products = [
            product_payload(
                index,
                price=1000,
                walletPrice=1000,
                priceWithoutDiscount=1200,
                position=index + 1,
                observedRangeLimit=100,
            )
            for index in range(30)
        ]

        response = self.post_hot_products(products, {"maxRecommendations": 30, "includeDebug": True})

        self.assertEqual(response.status_code, 200)
        diagnostics = response.json()["recommendations"][0]["debug"]["clusterDiagnosticsSummary"]
        self.assertEqual(diagnostics["totalClusters"], 1)
        self.assertEqual(diagnostics["clusterSizes"], [30])

    def test_hot_products_splits_21_products_when_similarity_groups_differ(self) -> None:
        products = [
            product_payload(
                index,
                sourceSubcategory="cluster-a" if index < 10 else "cluster-b",
                price=1000 if index < 10 else 4000,
                walletPrice=1000 if index < 10 else 4000,
                priceWithoutDiscount=1200 if index < 10 else 4500,
                position=index + 1,
            )
            for index in range(21)
        ]

        response = self.post_hot_products(products, {"maxRecommendations": 21, "includeDebug": True})

        self.assertEqual(response.status_code, 200)
        diagnostics = response.json()["recommendations"][0]["debug"]["clusterDiagnosticsSummary"]
        self.assertEqual(diagnostics["totalClusters"], 2)
        self.assertEqual(diagnostics["clusterSizes"], [10, 11])
        self.assertTrue(all(size <= 30 for size in diagnostics["clusterSizes"]))

    def test_hot_products_reports_skipped_products_with_reasons(self) -> None:
        products = fixture_products(24)
        products[23]["productKey"] = None
        products[23]["wbProductId"] = None
        products[23]["sourceCategory"] = None

        response = self.post_hot_products(products, {"maxRecommendations": 23, "includeDebug": True})

        self.assertEqual(response.status_code, 200)
        diagnostics = response.json()["recommendations"][0]["debug"]["clusterDiagnosticsSummary"]
        self.assertEqual(diagnostics["totalProducts"], 24)
        self.assertEqual(diagnostics["eligibleProducts"], 23)
        self.assertEqual(len(diagnostics["skippedProducts"]), 1)
        self.assertIn("reason", diagnostics["skippedProducts"][0])

    def test_delivery_peer_median_uses_assigned_cluster_not_all_products(self) -> None:
        products = []
        for index in range(31):
            products.append(product_payload(
                index,
                price=1000,
                walletPrice=1000,
                priceWithoutDiscount=1200,
                position=index + 1,
                observedRangeLimit=120,
                deliveryProfile=delivery_profile([
                    delivery_destination("central", "Р¦РµРЅС‚СЂР°Р»СЊРЅС‹Р№ СЂРµРіРёРѕРЅ", 24, city="РњРѕСЃРєРІР°")
                ]),
            ))
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination("central", "Р¦РµРЅС‚СЂР°Р»СЊРЅС‹Р№ СЂРµРіРёРѕРЅ", 72, city="РњРѕСЃРєРІР°", source_type="seller_warehouse")
        ])
        products[30]["deliveryProfile"] = delivery_profile([
            delivery_destination("central", "Р¦РµРЅС‚СЂР°Р»СЊРЅС‹Р№ СЂРµРіРёРѕРЅ", 120, city="РњРѕСЃРєРІР°")
        ])

        response = self.post_hot_products(products, {"maxRecommendations": 31, "includeDebug": True})

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        factor = next(factor for factor in target["factors"] if factor["code"] == "top_slow_central_delivery")

        self.assertEqual(factor["value"]["peerMedianDeliveryHours"], 24)
        self.assertEqual(factor["value"]["peerSampleSize"], 29)
        self.assertNotEqual(factor["value"]["peerMedianDeliveryHours"], 120)

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
            "seller_stock_slow_central_delivery",
            "top_low_stock_slow_central_delivery",
            "top_slow_cluster_region_delivery",
            "top_slow_central_delivery",
            "peers_slow_region_delivery",
            "faster_than_peers_region_delivery",
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
            "reviewWindowSize": 1,
            "recentTwoWeeksCount": 1,
            "latestReviewRunId": "wb_reviews_test",
            "sentimentVersion": 2,
            "negativeReviewEvidence": [],
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
            "reviewWindowSize": 1,
            "recentTwoWeeksCount": 1,
            "latestReviewRunId": "wb_reviews_test",
            "sentimentVersion": 2,
            "negativeReviewEvidence": [],
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
            "badReviewCount": 3,
            "reviewWindowSize": 5,
            "recentTwoWeeksCount": 5,
            "latestReviewRunId": "wb_reviews_test",
            "sentimentVersion": 2,
            "reviewScope": "product",
            "negativeReviewEvidence": [
                {
                    "reviewIdOnMp": "bad-rating-1",
                    "sourceWbProductId": str(products[0]["wbProductId"]),
                    "rating": 1,
                    "createdAtOnMp": "2026-06-12T10:00:00Z",
                    "snippet": "",
                    "reasonCodes": ["low_rating"],
                    "score": 0.85,
                }
            ],
        }

        payload = self.post_hot_products(products, {"maxRecommendations": 24}).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        factors_by_code = {factor["code"]: factor for factor in target["factors"]}

        self.assertIn("bad_recent_reviews", factors_by_code)
        self.assertEqual(factors_by_code["bad_recent_reviews"]["value"]["reviewWindowSize"], 5)
        self.assertEqual(factors_by_code["bad_recent_reviews"]["value"]["lowRatingReviews"], 3)

    def test_bad_recent_reviews_requires_review_window(self) -> None:
        products = fixture_products(24)
        products[0]["reviewSignals"] = {
            "parsedReviewCount": 20,
            "parsedReplyCount": 0,
            "ratedReviewCount": 20,
            "averageRating": 2.0,
            "lowRatingReviewCount": 8,
            "negativeTextReviewCount": 0,
            "badReviewCount": 8,
            "reviewWindowSize": 0,
            "recentTwoWeeksCount": 0,
            "latestReviewRunId": "wb_reviews_test",
            "sentimentVersion": 2,
            "reviewScope": "product",
            "negativeReviewEvidence": [],
        }

        payload = self.post_hot_products(products, {"maxRecommendations": 24}).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        codes = {factor["code"] for factor in target["factors"]}

        self.assertNotIn("bad_recent_reviews", codes)

    def test_negative_text_review_signal_without_low_rating_is_ignored(self) -> None:
        products = fixture_products(24)
        products[0]["reviewSignals"] = {
            "parsedReviewCount": 10,
            "parsedReplyCount": 0,
            "ratedReviewCount": 8,
            "averageRating": 4.2,
            "lowRatingReviewCount": 0,
            "negativeTextReviewCount": 2,
            "badReviewCount": 2,
            "reviewWindowSize": 10,
            "recentTwoWeeksCount": 5,
            "latestReviewRunId": "wb_reviews_test",
            "sentimentVersion": 2,
            "reviewScope": "product",
            "negativeReviewEvidence": [
                {
                    "reviewIdOnMp": "bad-text-1",
                    "sourceWbProductId": str(products[0]["wbProductId"]),
                    "rating": 5,
                    "createdAtOnMp": "2026-06-12T10:00:00Z",
                    "snippet": "???????? ????? ????",
                    "reasonCodes": ["ignored_text_signal"],
                    "score": 0.3,
                }
            ],
        }

        payload = self.post_hot_products(products, {"maxRecommendations": 24}).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        codes = {factor["code"] for factor in target["factors"]}

        self.assertNotIn("bad_recent_reviews", codes)

    def test_bad_recent_reviews_uses_unique_bad_review_count(self) -> None:
        products = fixture_products(24)
        products[0]["reviewSignals"] = {
            "parsedReviewCount": 9,
            "parsedReplyCount": 0,
            "ratedReviewCount": 9,
            "averageRating": 4.1,
            "lowRatingReviewCount": 2,
            "negativeTextReviewCount": 0,
            "badReviewCount": 2,
            "reviewWindowSize": 9,
            "recentTwoWeeksCount": 9,
            "latestReviewRunId": "wb_reviews_test",
            "sentimentVersion": 2,
            "reviewScope": "product",
            "negativeReviewEvidence": [
                {
                    "reviewIdOnMp": "same-review",
                    "sourceWbProductId": str(products[0]["wbProductId"]),
                    "rating": 1,
                    "createdAtOnMp": "2026-06-12T10:00:00Z",
                    "snippet": "Сломался",
                    "reasonCodes": ["low_rating"],
                    "score": 1.0,
                },
                {
                    "reviewIdOnMp": "other-review",
                    "sourceWbProductId": str(products[0]["wbProductId"]),
                    "rating": 1,
                    "createdAtOnMp": "2026-06-11T10:00:00Z",
                    "snippet": "",
                    "reasonCodes": ["low_rating"],
                    "score": 0.85,
                },
            ],
        }

        payload = self.post_hot_products(products, {"maxRecommendations": 24}).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        factor = next(
            factor for factor in target["factors"]
            if factor["code"] == "bad_recent_reviews"
        )

        self.assertEqual(factor["value"]["badReviewCount"], 2)
        self.assertIn("2 оценки 3 и ниже", factor["value"]["label"])

    def test_product_scope_ignores_evidence_from_other_variation(self) -> None:
        products = fixture_products(24)
        products[0]["reviewSignals"] = {
            "parsedReviewCount": 10,
            "parsedReplyCount": 0,
            "ratedReviewCount": 10,
            "averageRating": 4.8,
            "lowRatingReviewCount": 1,
            "negativeTextReviewCount": 0,
            "badReviewCount": 1,
            "reviewWindowSize": 10,
            "recentTwoWeeksCount": 10,
            "latestReviewRunId": "wb_reviews_test",
            "sentimentVersion": 2,
            "reviewScope": "product",
            "negativeReviewEvidence": [
                {
                    "reviewIdOnMp": "sibling-review",
                    "sourceWbProductId": "999999999",
                    "rating": 2,
                    "createdAtOnMp": "2026-06-12T10:00:00Z",
                    "snippet": "",
                    "reasonCodes": ["low_rating"],
                    "score": 0.85,
                }
            ],
        }

        payload = self.post_hot_products(products, {"maxRecommendations": 24}).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        codes = {factor["code"] for factor in target["factors"]}

        self.assertNotIn("bad_recent_reviews", codes)

    def test_root_scope_labels_shared_variation_reviews(self) -> None:
        products = fixture_products(24)
        products[0]["reviewSignals"] = {
            "parsedReviewCount": 10,
            "parsedReplyCount": 0,
            "ratedReviewCount": 10,
            "averageRating": 4.3,
            "lowRatingReviewCount": 1,
            "negativeTextReviewCount": 0,
            "badReviewCount": 1,
            "reviewWindowSize": 10,
            "recentTwoWeeksCount": 10,
            "latestReviewRunId": "wb_reviews_test",
            "sentimentVersion": 2,
            "reviewScope": "root",
            "negativeReviewEvidence": [
                {
                    "reviewIdOnMp": "sibling-review",
                    "sourceWbProductId": "999999999",
                    "rating": 2,
                    "createdAtOnMp": "2026-06-12T10:00:00Z",
                    "snippet": "",
                    "reasonCodes": ["low_rating"],
                    "score": 0.85,
                }
            ],
        }

        payload = self.post_hot_products(products, {"maxRecommendations": 24}).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        factor = next(
            factor for factor in target["factors"]
            if factor["code"] == "bad_recent_reviews"
        )

        self.assertEqual(factor["value"]["reviewScope"], "root")
        self.assertIn("общей карточке", factor["value"]["label"])

    def test_legacy_negative_text_signal_without_sentiment_version_is_ignored(self) -> None:
        products = fixture_products(24)
        products[0]["reviewSignals"] = {
            "parsedReviewCount": 10,
            "parsedReplyCount": 0,
            "ratedReviewCount": 10,
            "averageRating": 4.8,
            "lowRatingReviewCount": 0,
            "negativeTextReviewCount": 2,
            "reviewWindowSize": 10,
            "recentTwoWeeksCount": 10,
            "latestReviewRunId": "wb_reviews_test",
        }

        payload = self.post_hot_products(products, {"maxRecommendations": 24}).json()
        target = next(
            item for item in payload["recommendations"]
            if item["wbProductId"] == str(products[0]["wbProductId"])
        )
        codes = {factor["code"] for factor in target["factors"]}

        self.assertNotIn("bad_recent_reviews", codes)

    def test_seller_stock_slow_central_delivery_creates_factor(self) -> None:
        products = fixture_products(24)
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination(
                "central",
                "Центральный регион",
                96,
                source_type="seller_warehouse",
                city="Москва",
            )
        ])

        response = self.post_hot_products(products, {"maxRecommendations": 24})
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        factor = next(factor for factor in target["factors"] if factor["code"] == "seller_stock_slow_central_delivery")

        self.assertEqual(factor["direction"], "negative")
        self.assertEqual(factor["value"]["regionName"], "Центральный регион")
        self.assertEqual(factor["value"]["deliveryHours"], 96)
        self.assertEqual(factor["value"]["deliverySourceType"], "seller_warehouse")
        self.assertEqual(factor["value"]["label"], "Долгая доставка в Центральный регион со склада продавца: 4 д")

    def test_top_low_stock_slow_central_delivery_creates_factor(self) -> None:
        products = fixture_products(24)
        products[0]["totalQuantity"] = 4
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination("central", "Центральный регион", 96, city="Москва", quantity=4)
        ])

        response = self.post_hot_products(products, {"maxRecommendations": 24})
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        codes = {factor["code"] for factor in target["factors"]}

        self.assertIn("top_low_stock_slow_central_delivery", codes)
        self.assertNotIn("top_slow_central_delivery", codes)

    def test_top_slow_central_delivery_requires_peer_comparison(self) -> None:
        products = fixture_products(24)
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination("central", "Центральный регион", 72, city="Москва", source_type="seller_warehouse")
        ])

        response = self.post_hot_products(products, {"maxRecommendations": 24})
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        codes = {factor["code"] for factor in target["factors"]}

        self.assertIn("seller_stock_slow_central_delivery", codes)
        self.assertNotIn("top_slow_central_delivery", codes)

    def test_top_slow_central_delivery_requires_peer_median_at_least_24h_faster(self) -> None:
        products = fixture_products(24)
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination("central", "Центральный регион", 72, city="Москва", source_type="seller_warehouse")
        ])
        for index in range(1, 8):
            products[index]["price"] = products[0]["price"] + index
            products[index]["walletPrice"] = products[0]["walletPrice"] + index
            products[index]["deliveryProfile"] = delivery_profile([
                delivery_destination("central", "Центральный регион", 60, city="Москва")
            ])

        response = self.post_hot_products(products, {"maxRecommendations": 24})
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        codes = {factor["code"] for factor in target["factors"]}

        self.assertIn("seller_stock_slow_central_delivery", codes)
        self.assertNotIn("top_slow_central_delivery", codes)

    def test_top_slow_central_delivery_compares_against_peer_median(self) -> None:
        products = fixture_products(24)
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination("central", "Центральный регион", 72, city="Москва", source_type="seller_warehouse")
        ])
        for index in range(1, 8):
            products[index]["price"] = products[0]["price"] + index
            products[index]["walletPrice"] = products[0]["walletPrice"] + index
            products[index]["deliveryProfile"] = delivery_profile([
                delivery_destination("central", "Центральный регион", 48, city="Москва")
            ])

        response = self.post_hot_products(products, {"maxRecommendations": 24})
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        factor = next(factor for factor in target["factors"] if factor["code"] == "top_slow_central_delivery")

        self.assertEqual(factor["value"]["regionName"], "Центральный регион")
        self.assertEqual(factor["value"]["deliveryHours"], 72)
        self.assertEqual(factor["value"]["peerMedianDeliveryHours"], 48)
        self.assertEqual(factor["value"]["peerSampleSize"], 7)
        self.assertEqual(
            factor["value"]["label"],
            "Товар в топе, но доставка в Центральный регион дольше похожих: 3 д со склада продавца против 2 д у похожих",
        )

    def test_faster_than_peers_region_delivery_creates_factor_with_peer_sample(self) -> None:
        products = fixture_products(24)
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination("central", "Центральный регион", 24, city="Москва")
        ])
        for index in range(1, 8):
            products[index]["price"] = products[0]["price"] + index
            products[index]["walletPrice"] = products[0]["walletPrice"] + index
            products[index]["deliveryProfile"] = delivery_profile([
                delivery_destination("central", "Центральный регион", 96, city="Москва")
            ])

        response = self.post_hot_products(products, {"maxRecommendations": 24})
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        factor = next(factor for factor in target["factors"] if factor["code"] == "faster_than_peers_region_delivery")

        self.assertEqual(factor["direction"], "positive")
        self.assertEqual(factor["value"]["regionName"], "Центральный регион")
        self.assertEqual(factor["value"]["peerSampleSize"], 7)
        self.assertEqual(factor["value"]["peerMedianDeliveryHours"], 96)
        self.assertEqual(
            factor["value"]["label"],
            "Доставляется быстрее похожих: Центральный регион: 1 д со склада WB против медианы похожих 4 д",
        )

    def test_peers_slow_region_delivery_label_uses_days_not_hours(self) -> None:
        products = fixture_products(24)
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination("siberia", "Сибирь", 72, city="Новосибирск")
        ])
        for index in range(1, 8):
            products[index]["price"] = products[0]["price"] + index
            products[index]["walletPrice"] = products[0]["walletPrice"] + index
            products[index]["deliveryProfile"] = delivery_profile([
                delivery_destination("siberia", "Сибирь", 78, city="Новосибирск")
            ])

        response = self.post_hot_products(products, {"maxRecommendations": 24})
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        factor = next(factor for factor in target["factors"] if factor["code"] == "peers_slow_region_delivery")

        self.assertEqual(factor["value"]["label"], "Похожие доставляются долго: Сибирь: медиана около 4 д")

    def test_top_slow_cluster_region_delivery_requires_peers_to_be_faster(self) -> None:
        products = fixture_products(24)
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination("south", "Юг", 96, city="Краснодар", source_type="seller_warehouse")
        ])
        for index in range(1, 8):
            products[index]["price"] = products[0]["price"] + index
            products[index]["walletPrice"] = products[0]["walletPrice"] + index
            products[index]["deliveryProfile"] = delivery_profile([
                delivery_destination("south", "Юг", 120, city="Краснодар")
            ])

        response = self.post_hot_products(products, {"maxRecommendations": 24})
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        codes = {factor["code"] for factor in target["factors"]}

        self.assertNotIn("top_slow_cluster_region_delivery", codes)

    def test_cluster_region_slow_delivery_uses_broader_regional_factors(self) -> None:
        products = fixture_products(24)
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination("south", "Юг", 96, city="Краснодар", source_type="seller_warehouse")
        ])
        for index in range(1, 8):
            products[index]["price"] = products[0]["price"] + index
            products[index]["walletPrice"] = products[0]["walletPrice"] + index
            products[index]["deliveryProfile"] = delivery_profile([
                delivery_destination("south", "Юг", 24, city="Краснодар")
            ])

        response = self.post_hot_products(products, {"maxRecommendations": 24})
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        factors = {factor["code"]: factor for factor in target["factors"]}

        self.assertNotIn("top_slow_cluster_region_delivery", factors)
        self.assertIn("seller_stock_slow_central_delivery", factors)
        self.assertIn("top_slow_central_delivery", factors)
        self.assertEqual(factors["seller_stock_slow_central_delivery"]["value"]["regionName"], "Юг")
        self.assertEqual(factors["seller_stock_slow_central_delivery"]["value"]["peerMedianDeliveryHours"], 24)
        self.assertEqual(factors["top_slow_central_delivery"]["value"]["regionName"], "Юг")
        self.assertEqual(factors["top_slow_central_delivery"]["value"]["peerMedianDeliveryHours"], 24)

    def test_logistics_factors_require_visible_delivery_hours(self) -> None:
        products = fixture_products(24)
        products[0]["deliveryProfile"] = delivery_profile([
            delivery_destination(
                "central",
                "Центральный регион",
                None,
                source_type="seller_warehouse",
                city="Москва",
            )
        ])

        response = self.post_hot_products(products, {"maxRecommendations": 24})
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        target = next(item for item in payload["recommendations"] if item["wbProductId"] == str(products[0]["wbProductId"]))
        codes = {factor["code"] for factor in target["factors"]}

        self.assertNotIn("seller_stock_slow_central_delivery", codes)
        self.assertNotIn("top_slow_central_delivery", codes)

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

    def test_workspace_product_analysis_returns_delivery_comparison_groups(self) -> None:
        product = product_payload(
            0,
            position=8,
            totalQuantity=12,
            deliveryProfile=delivery_profile([
                delivery_destination("central", "Центральный регион", 96, city="Москва")
            ]),
        )
        candidates = fixture_products(8)
        candidates[0]["deliveryProfile"] = delivery_profile([
            delivery_destination("central", "Центральный регион", 24, city="Москва")
        ])
        response = self.client.post(
            "/api/v1/recommendations/workspace-product-analysis",
            json={
                "requestId": "req-workspace-analysis-delivery-1",
                "generatedAtUtc": now_iso(),
                "marketplace": "wildberries",
                "product": product,
                "history": {
                    "priceObservations": [],
                    "positionObservations": [],
                    "stockObservations": [],
                    "feedbackObservations": [],
                },
                "candidates": candidates,
                "options": {"maxSimilarProducts": 5},
            },
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        group_titles = {group["key"]: group["title"] for group in payload["similarProductGroups"]}
        self.assertIn("similar_faster_region_delivery", group_titles)
        group = next(group for group in payload["similarProductGroups"] if group["key"] == "similar_faster_region_delivery")
        self.assertTrue(any("Москва" in " ".join(item["tags"]) for item in group["items"]))

    def test_workspace_product_analysis_groups_duplicate_cards_by_model_size_and_color(self) -> None:
        product = product_payload(
            0,
            productKey="workspace:986542381",
            wbProductId=986542381,
            wbRootId=1695553755,
            name="Коврик для ванной Мечта 40x60 см бежевый",
            sourceCategory="Товары для дома",
            sourceSubcategory="Коврики для ванной",
            walletPrice=540,
            price=552,
            totalQuantity=15,
        )
        candidates = [
            product_payload(
                1,
                productKey="parser:903892357",
                wbProductId=903892357,
                wbRootId=1154363376,
                name="Коврик для ванной Мечта 40х60 см бежевый",
                sourceCategory="Товары для дома",
                sourceSubcategory="Коврики для ванной",
                walletPrice=503,
                price=514,
                totalQuantity=15,
            ),
            product_payload(
                2,
                productKey="parser:1003841038",
                wbProductId=1003841038,
                wbRootId=1838092259,
                name="Коврик «Мечта», 40×60 см, бежевый",
                sourceCategory="Товары для дома",
                sourceSubcategory="Коврики для ванной",
                walletPrice=544,
                price=556,
                totalQuantity=50,
            ),
            product_payload(
                3,
                productKey="parser:generic-beige",
                wbProductId=1128361866,
                name="Коврик для ванной 40х60 см бежевый",
                sourceCategory="Товары для дома",
                sourceSubcategory="Коврики для ванной",
                walletPrice=541,
                price=552,
                totalQuantity=15,
            ),
            product_payload(
                4,
                productKey="parser:bukli-beige",
                wbProductId=940227640,
                name="Коврик для ванной Букли длинные бежевый 40х60 см",
                sourceCategory="Товары для дома",
                sourceSubcategory="Коврики для ванной",
                walletPrice=543,
                price=554,
                totalQuantity=15,
            ),
        ]

        response = self.client.post(
            "/api/v1/recommendations/workspace-product-analysis",
            json={
                "requestId": "req-workspace-analysis-duplicates-1",
                "generatedAtUtc": now_iso(),
                "marketplace": "wildberries",
                "product": product,
                "history": {
                    "priceObservations": [],
                    "positionObservations": [],
                    "stockObservations": [],
                    "feedbackObservations": [],
                },
                "candidates": candidates,
                "options": {"maxSimilarProducts": 10},
            },
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        group = next(group for group in payload["similarProductGroups"] if group["key"] == "duplicate_cards")
        duplicate_keys = {item["productKey"] for item in group["items"]}
        self.assertEqual(duplicate_keys, {"parser:903892357", "parser:1003841038"})
        self.assertTrue(all("Одинаковая карточка" in item["tags"] for item in group["items"]))

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
