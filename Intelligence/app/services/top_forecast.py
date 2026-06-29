from __future__ import annotations

import json
import math
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from uuid import uuid4

from app.core.errors import AppError
from app.models.common import RecommendationStatus
from app.models.recommendations import (
    MarketProductFeatureDto,
    TopForecastMetricsDto,
    TopForecastOptionsDto,
    TopForecastPredictionDto,
    TopForecastPredictRequest,
    TopForecastPredictResponse,
    TopForecastTrainRequest,
    TopForecastTrainResponse,
)

np = None
pd = None
accuracy_score = None
f1_score = None
mean_absolute_error = None
precision_score = None
recall_score = None
roc_auc_score = None
train_test_split = None


ALGORITHM = "catboost_top100_v1"
ALGORITHM_VERSION = "1.0.0"
MODEL_VERSION = "top_forecast_v1"

NUMERIC_FEATURES = [
    "price",
    "price_without_discount",
    "wallet_price",
    "discount_percent",
    "total_quantity",
    "rating",
    "feedback_count",
    "parsed_review_count",
    "positive_review_count",
    "review_sample_size",
    "parsed_reply_count",
    "image_count",
    "description_length",
    "characteristics_count",
    "seller_frequency",
    "brand_frequency",
    "root_frequency",
    "delivery_min_hours",
    "delivery_destination_count",
    "bad_review_count",
    "low_rating_review_count",
    "recent_two_weeks_count",
    "rating_missing",
    "feedback_missing",
    "delivery_missing",
    "description_missing",
    "characteristics_missing",
]

CATEGORICAL_FEATURES = [
    "source_category",
    "source_subcategory",
    "brand_name",
    "seller_name",
]

LOG_FEATURES = {
    "price",
    "price_without_discount",
    "wallet_price",
    "total_quantity",
    "feedback_count",
    "parsed_review_count",
    "positive_review_count",
    "review_sample_size",
    "parsed_reply_count",
    "seller_frequency",
    "brand_frequency",
    "root_frequency",
    "delivery_min_hours",
}

FEATURE_NAMES = NUMERIC_FEATURES + CATEGORICAL_FEATURES
LEAKAGE_FIELDS = {"position", "position_state", "observed_range_limit", "positionState", "observedRangeLimit"}


def _ensure_ml_stack(request_id: str | None = None) -> None:
    global np
    global pd
    global accuracy_score
    global f1_score
    global mean_absolute_error
    global precision_score
    global recall_score
    global roc_auc_score
    global train_test_split

    if np is not None and pd is not None and train_test_split is not None:
        return

    try:
        import numpy as numpy_module
        import pandas as pandas_module
        from sklearn.metrics import (
            accuracy_score as accuracy_score_fn,
            f1_score as f1_score_fn,
            mean_absolute_error as mean_absolute_error_fn,
            precision_score as precision_score_fn,
            recall_score as recall_score_fn,
            roc_auc_score as roc_auc_score_fn,
        )
        from sklearn.model_selection import train_test_split as train_test_split_fn
    except Exception as exc:  # pragma: no cover - environment failure path
        raise AppError(
            error_code="top_forecast_dependencies_unavailable",
            message="Top forecast dependencies are not installed in the Intelligence runtime.",
            status_code=503,
            request_id=request_id,
        ) from exc

    np = numpy_module
    pd = pandas_module
    accuracy_score = accuracy_score_fn
    f1_score = f1_score_fn
    mean_absolute_error = mean_absolute_error_fn
    precision_score = precision_score_fn
    recall_score = recall_score_fn
    roc_auc_score = roc_auc_score_fn
    train_test_split = train_test_split_fn


@dataclass(frozen=True)
class FeatureFrame:
    frame: pd.DataFrame
    product_keys: list[str | None]
    wb_product_ids: list[str | None]
    wb_root_ids: list[str | None]
    labels: np.ndarray | None
    position_targets: np.ndarray | None
    label_known: np.ndarray
    feature_coverage: np.ndarray


class TopForecastService:
    def __init__(self, settings) -> None:
        self._model_dir = Path(settings.top_forecast_model_dir)
        self._model_dir.mkdir(parents=True, exist_ok=True)

    def train(self, payload: TopForecastTrainRequest) -> TopForecastTrainResponse:
        options = payload.options or TopForecastOptionsDto()
        _ensure_ml_stack(payload.request_id)
        try:
            from catboost import CatBoostClassifier, CatBoostRegressor, Pool
        except Exception as exc:  # pragma: no cover - environment failure path
            raise AppError(
                error_code="catboost_unavailable",
                message="CatBoost is not installed in the Intelligence runtime.",
                status_code=503,
                request_id=payload.request_id,
            ) from exc

        feature_frame, clip_bounds = self._build_feature_frame(payload.products, training=True)
        if feature_frame.labels is None or feature_frame.position_targets is None:
            return self._not_enough_data(payload, options, ["No labelable products were supplied."])

        label_mask = feature_frame.label_known
        x_all = feature_frame.frame.loc[label_mask, FEATURE_NAMES].copy()
        y_all = feature_frame.labels[label_mask]
        positions_all = feature_frame.position_targets[label_mask]

        positive_count = int(y_all.sum())
        warnings: list[str] = []
        if len(x_all) < 50 or positive_count < 5 or positive_count == len(x_all):
            warnings.append(
                "Not enough balanced rank labels to train top-100 model. Need at least 50 labelable products and both classes."
            )
            return self._not_enough_data(payload, options, warnings, sample_size=len(x_all), positive_count=positive_count)

        train_idx, validation_idx, test_idx = self._split_indices(
            products=[payload.products[i] for i, known in enumerate(label_mask) if known],
            labels=y_all,
            options=options,
        )

        cat_indices = [FEATURE_NAMES.index(name) for name in CATEGORICAL_FEATURES]
        train_pool = Pool(x_all.iloc[train_idx], y_all[train_idx], cat_features=cat_indices)
        validation_pool = Pool(x_all.iloc[validation_idx], y_all[validation_idx], cat_features=cat_indices)

        classifier = CatBoostClassifier(
            loss_function="Logloss",
            eval_metric="AUC",
            iterations=options.iterations,
            depth=6,
            learning_rate=0.05,
            random_seed=options.random_seed,
            auto_class_weights="Balanced",
            verbose=False,
            allow_writing_files=False,
        )
        classifier.fit(train_pool, eval_set=validation_pool, use_best_model=True)

        position_target = np.log1p(np.clip(positions_all, 1, None))
        regressor = CatBoostRegressor(
            loss_function="MAE",
            iterations=max(50, min(options.iterations, 400)),
            depth=6,
            learning_rate=0.05,
            random_seed=options.random_seed,
            verbose=False,
            allow_writing_files=False,
        )
        regressor.fit(
            Pool(x_all.iloc[train_idx], position_target[train_idx], cat_features=cat_indices),
            eval_set=Pool(x_all.iloc[validation_idx], position_target[validation_idx], cat_features=cat_indices),
            use_best_model=True,
        )

        validation_probabilities = classifier.predict_proba(x_all.iloc[validation_idx])[:, 1]
        probability_temperature = self._fit_probability_temperature(
            validation_probabilities,
            y_all[validation_idx],
        )
        validation_calibrated = self._apply_probability_temperature(
            validation_probabilities,
            probability_temperature,
        )
        validation_predicted = (validation_calibrated >= options.min_probability).astype(int)
        validation_precision_at_threshold = float(
            precision_score(y_all[validation_idx], validation_predicted, zero_division=0)
        )
        model_confidence_factor = self._model_confidence_factor(validation_precision_at_threshold)

        metrics = [
            self._classification_metrics(
                "train",
                classifier,
                regressor,
                x_all,
                y_all,
                positions_all,
                train_idx,
                probability_temperature,
            ),
            self._classification_metrics(
                "validation",
                classifier,
                regressor,
                x_all,
                y_all,
                positions_all,
                validation_idx,
                probability_temperature,
            ),
            self._classification_metrics(
                "test",
                classifier,
                regressor,
                x_all,
                y_all,
                positions_all,
                test_idx,
                probability_temperature,
            ),
        ]

        trained_at = datetime.now(timezone.utc)
        artifact_id = f"top_forecast_{trained_at.strftime('%Y%m%d%H%M%S')}_{uuid4().hex[:10]}"
        artifact_dir = self._model_dir / artifact_id
        artifact_dir.mkdir(parents=True, exist_ok=True)
        classifier.save_model(str(artifact_dir / "classifier.cbm"))
        regressor.save_model(str(artifact_dir / "position_regressor.cbm"))

        metadata = {
            "modelArtifactId": artifact_id,
            "algorithm": ALGORITHM,
            "algorithmVersion": ALGORITHM_VERSION,
            "modelVersion": MODEL_VERSION,
            "trainedAtUtc": trained_at.isoformat(),
            "featureNames": FEATURE_NAMES,
            "categoricalFeatureNames": CATEGORICAL_FEATURES,
            "clipBounds": clip_bounds,
            "logFeatures": sorted(LOG_FEATURES),
            "leakageFieldsExcluded": sorted(LEAKAGE_FIELDS),
            "probabilityCalibration": {
                "method": "temperature_scaling",
                "temperature": probability_temperature,
                "validationPrecisionAtMinProbability": round(validation_precision_at_threshold, 4),
                "modelConfidenceFactor": round(model_confidence_factor, 4),
                "maxDisplayedConfidence": 0.95,
            },
            "metrics": [metric.model_dump(by_alias=True) for metric in metrics],
        }
        (artifact_dir / "metadata.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")

        return TopForecastTrainResponse(
            requestId=payload.request_id,
            status=RecommendationStatus.COMPLETED,
            algorithm=ALGORITHM,
            algorithmVersion=ALGORITHM_VERSION,
            modelVersion=MODEL_VERSION,
            modelArtifactId=artifact_id,
            trainedAtUtc=trained_at,
            featureNames=FEATURE_NAMES,
            categoricalFeatureNames=CATEGORICAL_FEATURES,
            metrics=metrics,
            sampleSize=len(x_all),
            trainingSampleSize=len(train_idx),
            validationSampleSize=len(validation_idx),
            testSampleSize=len(test_idx),
            positiveCount=positive_count,
            warnings=warnings,
        )

    def predict(self, payload: TopForecastPredictRequest) -> TopForecastPredictResponse:
        options = payload.options or TopForecastOptionsDto()
        _ensure_ml_stack(payload.request_id)
        try:
            from catboost import CatBoostClassifier, CatBoostRegressor, Pool
        except Exception as exc:  # pragma: no cover - environment failure path
            raise AppError(
                error_code="catboost_unavailable",
                message="CatBoost is not installed in the Intelligence runtime.",
                status_code=503,
                request_id=payload.request_id,
            ) from exc

        artifact_dir = self._model_dir / payload.model_artifact_id
        metadata_path = artifact_dir / "metadata.json"
        if not metadata_path.exists():
            raise AppError(
                error_code="top_forecast_model_not_found",
                message="Top forecast model artifact was not found.",
                status_code=404,
                details={"modelArtifactId": payload.model_artifact_id},
                request_id=payload.request_id,
            )

        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        feature_frame, _ = self._build_feature_frame(
            payload.products,
            training=False,
            clip_bounds=metadata.get("clipBounds") or {},
        )

        classifier = CatBoostClassifier()
        classifier.load_model(str(artifact_dir / "classifier.cbm"))
        regressor = CatBoostRegressor()
        regressor.load_model(str(artifact_dir / "position_regressor.cbm"))

        cat_indices = [FEATURE_NAMES.index(name) for name in CATEGORICAL_FEATURES]
        pool = Pool(feature_frame.frame[FEATURE_NAMES], cat_features=cat_indices)
        raw_probabilities = classifier.predict_proba(pool)[:, 1]
        calibration = metadata.get("probabilityCalibration") or {}
        probability_temperature = float(calibration.get("temperature") or 1.0)
        model_confidence_factor = float(calibration.get("modelConfidenceFactor") or 0.85)
        probabilities = self._apply_probability_temperature(raw_probabilities, probability_temperature)
        raw_positions = np.expm1(regressor.predict(pool))

        predictions: list[TopForecastPredictionDto] = []
        for index, probability in enumerate(probabilities):
            predicted_position = int(max(1, min(1000, round(float(raw_positions[index])))))
            confidence = self._prediction_confidence(
                float(probability),
                float(feature_frame.feature_coverage[index]),
                model_confidence_factor,
            )
            predictions.append(
                TopForecastPredictionDto(
                    productKey=feature_frame.product_keys[index],
                    wbProductId=feature_frame.wb_product_ids[index],
                    wbRootId=feature_frame.wb_root_ids[index],
                    predictedPosition=predicted_position,
                    top100Probability=round(float(probability), 4),
                    confidence=round(confidence, 4),
                    featureCoveragePercent=round(float(feature_frame.feature_coverage[index]), 2),
                    reasons=self._build_reasons(payload.products[index]),
                )
            )

        return TopForecastPredictResponse(
            requestId=payload.request_id,
            status=RecommendationStatus.COMPLETED,
            algorithm=ALGORITHM,
            algorithmVersion=ALGORITHM_VERSION,
            modelVersion=metadata.get("modelVersion") or MODEL_VERSION,
            modelArtifactId=payload.model_artifact_id,
            computedAtUtc=datetime.now(timezone.utc),
            predictions=predictions,
            warnings=[] if predictions else ["No products were supplied for prediction."],
        )

    def _not_enough_data(
        self,
        payload: TopForecastTrainRequest,
        options: TopForecastOptionsDto,
        warnings: list[str],
        *,
        sample_size: int = 0,
        positive_count: int = 0,
    ) -> TopForecastTrainResponse:
        return TopForecastTrainResponse(
            requestId=payload.request_id,
            status=RecommendationStatus.NOT_ENOUGH_DATA,
            algorithm=options.algorithm,
            algorithmVersion=ALGORITHM_VERSION,
            modelVersion="none",
            modelArtifactId=None,
            trainedAtUtc=datetime.now(timezone.utc),
            featureNames=FEATURE_NAMES,
            categoricalFeatureNames=CATEGORICAL_FEATURES,
            metrics=[],
            sampleSize=sample_size,
            trainingSampleSize=0,
            validationSampleSize=0,
            testSampleSize=0,
            positiveCount=positive_count,
            warnings=warnings,
        )

    def _build_feature_frame(
        self,
        products: list[MarketProductFeatureDto],
        *,
        training: bool,
        clip_bounds: dict[str, dict[str, float]] | None = None,
    ) -> tuple[FeatureFrame, dict[str, dict[str, float]]]:
        seller_frequency = self._frequency([p.seller_name for p in products])
        brand_frequency = self._frequency([p.brand_name for p in products])
        root_frequency = self._frequency([p.wb_root_id for p in products])

        rows: list[dict[str, object]] = []
        labels: list[int] = []
        position_targets: list[int] = []
        label_known: list[bool] = []
        coverage: list[float] = []
        product_keys: list[str | None] = []
        wb_product_ids: list[str | None] = []
        wb_root_ids: list[str | None] = []

        top_threshold = 100
        for product in products:
            current_price = self._number(product.wallet_price) or self._number(product.price)
            price_without_discount = self._number(product.price_without_discount)
            discount_percent = None
            if current_price is not None and price_without_discount and price_without_discount > 0:
                discount_percent = max(0.0, (price_without_discount - current_price) / price_without_discount * 100.0)

            description_length = len(product.description.strip()) if product.description else None
            characteristics_count = self._characteristics_count(product.characteristics)
            delivery_hours = self._delivery_min_hours(product)
            destination_count = (
                len(product.delivery_profile.destinations)
                if product.delivery_profile and product.delivery_profile.destinations
                else None
            )
            review_signals = product.review_signals

            row = {
                "price": current_price,
                "price_without_discount": price_without_discount,
                "wallet_price": self._number(product.wallet_price),
                "discount_percent": discount_percent,
                "total_quantity": self._number(product.total_quantity),
                "rating": self._number(product.rating),
                "feedback_count": self._number(product.feedback_count),
                "parsed_review_count": self._number(product.parsed_review_count),
                "positive_review_count": self._number(product.positive_review_count),
                "review_sample_size": self._number(product.review_sample_size),
                "parsed_reply_count": self._number(product.parsed_reply_count),
                "image_count": self._number(product.image_count),
                "description_length": description_length,
                "characteristics_count": characteristics_count,
                "seller_frequency": seller_frequency.get(product.seller_name or "", 0),
                "brand_frequency": brand_frequency.get(product.brand_name or "", 0),
                "root_frequency": root_frequency.get(product.wb_root_id or "", 0),
                "delivery_min_hours": delivery_hours,
                "delivery_destination_count": destination_count,
                "bad_review_count": self._number(review_signals.bad_review_count if review_signals else None),
                "low_rating_review_count": self._number(
                    review_signals.low_rating_review_count if review_signals else None
                ),
                "recent_two_weeks_count": self._number(review_signals.recent_two_weeks_count if review_signals else None),
                "rating_missing": 0 if product.rating is not None else 1,
                "feedback_missing": 0 if product.feedback_count is not None else 1,
                "delivery_missing": 0 if delivery_hours is not None else 1,
                "description_missing": 0 if description_length is not None else 1,
                "characteristics_missing": 0 if characteristics_count is not None else 1,
                "source_category": product.source_category or "__missing__",
                "source_subcategory": product.source_subcategory or "__missing__",
                "brand_name": product.brand_name or "__missing__",
                "seller_name": product.seller_name or "__missing__",
            }
            rows.append(row)

            known, target_position = self._label_position(product)
            label_known.append(known)
            labels.append(1 if known and target_position <= top_threshold else 0)
            position_targets.append(target_position if known else 1001)
            coverage.append(self._feature_coverage(product, delivery_hours, description_length, characteristics_count))
            product_keys.append(product.product_key)
            wb_product_ids.append(product.wb_product_id)
            wb_root_ids.append(product.wb_root_id)

        frame = pd.DataFrame(rows)
        for feature in NUMERIC_FEATURES:
            frame[feature] = pd.to_numeric(frame[feature], errors="coerce")
        for feature in CATEGORICAL_FEATURES:
            frame[feature] = frame[feature].fillna("__missing__").astype(str)

        resolved_clip_bounds = clip_bounds or {}
        if training:
            resolved_clip_bounds = self._compute_clip_bounds(frame)

        frame = self._apply_transforms(frame, resolved_clip_bounds)
        return (
            FeatureFrame(
                frame=frame,
                product_keys=product_keys,
                wb_product_ids=wb_product_ids,
                wb_root_ids=wb_root_ids,
                labels=np.array(labels, dtype=np.int32) if products else None,
                position_targets=np.array(position_targets, dtype=np.float32) if products else None,
                label_known=np.array(label_known, dtype=bool),
                feature_coverage=np.array(coverage, dtype=np.float32),
            ),
            resolved_clip_bounds,
        )

    @staticmethod
    def _frequency(values: list[str | None]) -> dict[str, int]:
        result: dict[str, int] = {}
        for value in values:
            key = value or ""
            result[key] = result.get(key, 0) + 1
        return result

    @staticmethod
    def _number(value) -> float | None:
        if value is None:
            return None
        try:
            number = float(value)
        except (TypeError, ValueError):
            return None
        if math.isnan(number) or math.isinf(number):
            return None
        return number

    @staticmethod
    def _characteristics_count(value) -> int | None:
        if value is None:
            return None
        if isinstance(value, dict):
            return len(value)
        if isinstance(value, list):
            return len(value)
        return None

    @staticmethod
    def _delivery_min_hours(product: MarketProductFeatureDto) -> float | None:
        if not product.delivery_profile or not product.delivery_profile.destinations:
            return None
        hours = [
            destination.delivery_hours
            for destination in product.delivery_profile.destinations
            if destination.delivery_hours is not None and destination.delivery_hours >= 0
        ]
        return float(min(hours)) if hours else None

    @staticmethod
    def _label_position(product: MarketProductFeatureDto) -> tuple[bool, int]:
        if product.position is not None and product.position > 0:
            return True, int(product.position)
        if product.position_state == "beyondObservedRange" and product.observed_range_limit:
            return True, int(product.observed_range_limit) + 1
        return False, 1001

    @staticmethod
    def _feature_coverage(
        product: MarketProductFeatureDto,
        delivery_hours: float | None,
        description_length: int | None,
        characteristics_count: int | None,
    ) -> float:
        values = [
            product.price or product.wallet_price,
            product.rating,
            product.feedback_count,
            product.total_quantity,
            product.image_count,
            description_length,
            characteristics_count,
            delivery_hours,
            product.brand_name,
            product.seller_name,
        ]
        filled = sum(1 for value in values if value not in (None, ""))
        return filled / len(values) * 100.0

    @staticmethod
    def _compute_clip_bounds(frame: pd.DataFrame) -> dict[str, dict[str, float]]:
        bounds: dict[str, dict[str, float]] = {}
        for feature in NUMERIC_FEATURES:
            series = frame[feature].dropna()
            if len(series) < 10:
                continue
            lower = float(series.quantile(0.01))
            upper = float(series.quantile(0.99))
            if lower <= upper:
                bounds[feature] = {"lower": lower, "upper": upper}
        return bounds

    @staticmethod
    def _apply_transforms(frame: pd.DataFrame, clip_bounds: dict[str, dict[str, float]]) -> pd.DataFrame:
        transformed = frame.copy()
        for feature, bounds in clip_bounds.items():
            if feature in transformed:
                transformed[feature] = transformed[feature].clip(bounds.get("lower"), bounds.get("upper"))
        for feature in LOG_FEATURES:
            if feature in transformed:
                transformed[feature] = np.log1p(transformed[feature].clip(lower=0))
        return transformed[FEATURE_NAMES]

    @staticmethod
    def _split_indices(
        products: list[MarketProductFeatureDto],
        labels: np.ndarray,
        options: TopForecastOptionsDto,
    ) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
        indices = np.arange(len(labels))
        groups = np.array([product.wb_root_id or product.wb_product_id or product.product_key or str(i) for i, product in enumerate(products)])
        group_frame = pd.DataFrame({"group": groups, "label": labels}).groupby("group", as_index=False)["label"].max()

        stratify = group_frame["label"] if group_frame["label"].nunique() > 1 else None
        train_groups, temp_groups = train_test_split(
            group_frame["group"],
            train_size=options.train_fraction,
            random_state=options.random_seed,
            stratify=stratify,
        )
        temp = group_frame[group_frame["group"].isin(temp_groups)]
        temp_stratify = temp["label"] if temp["label"].nunique() > 1 else None
        validation_share = options.validation_fraction / (options.validation_fraction + options.test_fraction)
        validation_groups, test_groups = train_test_split(
            temp["group"],
            train_size=validation_share,
            random_state=options.random_seed,
            stratify=temp_stratify,
        )

        def select(group_values) -> np.ndarray:
            group_set = set(group_values)
            return indices[np.isin(groups, list(group_set))]

        return select(train_groups), select(validation_groups), select(test_groups)

    @staticmethod
    def _classification_metrics(
        split: str,
        classifier,
        regressor,
        frame: pd.DataFrame,
        labels: np.ndarray,
        position_targets: np.ndarray,
        indices: np.ndarray,
        probability_temperature: float = 1.0,
    ) -> TopForecastMetricsDto:
        if len(indices) == 0:
            return TopForecastMetricsDto(split=split, sampleSize=0, positiveCount=0)
        x = frame.iloc[indices]
        y = labels[indices]
        probabilities = TopForecastService._apply_probability_temperature(
            classifier.predict_proba(x)[:, 1],
            probability_temperature,
        )
        predicted = (probabilities >= 0.5).astype(int)
        roc_auc = None
        if len(np.unique(y)) > 1:
            roc_auc = float(roc_auc_score(y, probabilities))
        predicted_position = np.expm1(regressor.predict(x))
        return TopForecastMetricsDto(
            split=split,
            sampleSize=int(len(indices)),
            positiveCount=int(y.sum()),
            accuracy=round(float(accuracy_score(y, predicted)), 4),
            precision=round(float(precision_score(y, predicted, zero_division=0)), 4),
            recall=round(float(recall_score(y, predicted, zero_division=0)), 4),
            f1=round(float(f1_score(y, predicted, zero_division=0)), 4),
            rocAuc=round(roc_auc, 4) if roc_auc is not None else None,
            positionMae=round(float(mean_absolute_error(position_targets[indices], predicted_position)), 2),
        )

    @staticmethod
    def _fit_probability_temperature(probabilities: np.ndarray, labels: np.ndarray) -> float:
        if len(probabilities) == 0 or len(np.unique(labels)) < 2:
            return 1.0

        candidates = np.array([1.0, 1.25, 1.5, 2.0, 3.0, 5.0, 8.0, 12.0], dtype=np.float64)
        best_temperature = 1.0
        best_loss = float("inf")
        for temperature in candidates:
            calibrated = TopForecastService._apply_probability_temperature(probabilities, float(temperature))
            clipped = np.clip(calibrated, 1e-6, 1 - 1e-6)
            loss = -float(np.mean(labels * np.log(clipped) + (1 - labels) * np.log(1 - clipped)))
            if loss < best_loss:
                best_loss = loss
                best_temperature = float(temperature)
        return best_temperature

    @staticmethod
    def _apply_probability_temperature(probabilities: np.ndarray, temperature: float) -> np.ndarray:
        safe_temperature = max(float(temperature or 1.0), 1.0)
        clipped = np.clip(probabilities, 1e-6, 1 - 1e-6)
        logits = np.log(clipped / (1 - clipped))
        return 1 / (1 + np.exp(-(logits / safe_temperature)))

    @staticmethod
    def _model_confidence_factor(validation_precision: float) -> float:
        if not math.isfinite(validation_precision) or validation_precision <= 0:
            return 0.65
        return max(0.65, min(0.90, validation_precision))

    @staticmethod
    def _prediction_confidence(
        probability: float,
        feature_coverage_percent: float,
        model_confidence_factor: float,
    ) -> float:
        _ = feature_coverage_percent, model_confidence_factor
        calibrated = max(0.0, min(1.0, probability))
        return min(0.95, calibrated)

    @staticmethod
    def _build_reasons(product: MarketProductFeatureDto) -> list[str]:
        reasons: list[str] = []
        if product.rating is not None:
            reasons.append(f"rating={product.rating}")
        if product.feedback_count is not None:
            reasons.append(f"feedbackCount={product.feedback_count}")
        if product.image_count is not None:
            reasons.append(f"imageCount={product.image_count}")
        if product.total_quantity is not None:
            reasons.append(f"stock={product.total_quantity}")
        return reasons[:4]
