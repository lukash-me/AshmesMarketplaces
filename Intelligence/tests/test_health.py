from __future__ import annotations

import unittest

from fastapi.testclient import TestClient

from app.main import app


class HealthTests(unittest.TestCase):
    def setUp(self) -> None:
        self.client = TestClient(app)

    def test_live_health(self) -> None:
        response = self.client.get("/health/live")

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["status"], "live")
        self.assertEqual(payload["serviceName"], "ashmes-intelligence")
        self.assertIn("checkedAtUtc", payload)

    def test_ready_health(self) -> None:
        response = self.client.get("/health/ready")

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["status"], "ready")
        self.assertEqual(payload["serviceVersion"], "0.1.0")

    def test_api_health(self) -> None:
        response = self.client.get("/api/v1/health")

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json()["status"], "ready")

    def test_metadata(self) -> None:
        response = self.client.get("/api/v1/intelligence/metadata")

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["serviceName"], "ashmes-intelligence")
        self.assertEqual(payload["contractVersion"], "2026-05-stage-6b")
        self.assertIn("rule_based_hot_products_v1", payload["supportedAlgorithms"])
        self.assertIn("POST /api/v1/recommendations/hot-products", payload["supportedEndpoints"])


if __name__ == "__main__":
    unittest.main()
