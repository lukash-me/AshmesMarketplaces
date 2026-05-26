# Ashmes Intelligence Service

Python HTTP service for AshmesMarketplaces intelligence and recommendation calculations.

Stage 6B adds the first deterministic rule-based hot-products algorithm. It is not ML, does not generate guaranteed business claims, does not write to PostgreSQL, and is not integrated with the .NET backend or frontend yet.

## Boundary

- Python computes and explains candidate outputs.
- .NET owns authentication, workspace/user context, frontend-facing APIs, persistence, validation before saving, and PostgreSQL writes.
- Parser execution, scheduling, and background orchestration are out of scope for this service stage.

## Setup

From `E:\AshmesMarketplaces\Intelligence`:

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```

Using an existing Python environment is also acceptable:

```powershell
python -m pip install -r requirements.txt
```

## Run

```powershell
python -m uvicorn app.main:app --reload --host 127.0.0.1 --port 8020
```

Default local URLs:

- `GET http://127.0.0.1:8020/health/live`
- `GET http://127.0.0.1:8020/health/ready`
- `GET http://127.0.0.1:8020/api/v1/health`
- `GET http://127.0.0.1:8020/api/v1/intelligence/metadata`

## Test

```powershell
python -m unittest discover tests
```

## Configuration

Environment variables:

- `INTELLIGENCE_SERVICE_NAME`, default `ashmes-intelligence`
- `INTELLIGENCE_ENV`, default `development`
- `INTELLIGENCE_LOG_LEVEL`, default `INFO`
- `INTELLIGENCE_API_PREFIX`, default `/api/v1`
- `INTELLIGENCE_ENABLE_DEBUG`, default `false`

Debug fields stay `null` by default. Set `INTELLIGENCE_ENABLE_DEBUG=true` or request `options.includeDebug=true` only for local diagnosis. Debug output must not include full raw product snapshots.

## Hot Products Algorithm

Endpoint:

- `POST /api/v1/recommendations/hot-products`

Default algorithm:

- `algorithm`: `rule_based_hot_products_v1`
- `algorithmVersion`: `1.0.0`
- `modelVersion`: `none`

This algorithm is deterministic and rule-based. It scores seller-analysis candidates from the supplied market snapshot only. It does not claim profit, demand growth, revenue, margin, marketplace endorsement, or future sales.

Default thresholds:

- `minScore = 60`
- `minConfidence = 0.45`
- `minProductsForScoring = 20`
- at least `5` eligible products after invalid-row filtering

Factor weights:

- Position: `0.30`
- Rating: `0.20`
- Reviews: `0.20`
- Price: `0.15`
- Stock: `0.05`
- Data completeness: `0.10`

Input signals:

- position and observed range;
- rating;
- feedback/review counts;
- category/subcategory-relative price;
- stock-like quantity;
- product identity and snapshot timestamp.

Price is compared within `sourceSubcategory` where possible. If the subcategory group is too small, the request-level distribution is used with lower confidence. The algorithm does not reward products simply for being globally cheap.

`totalQuantity >= 40` is treated as available-but-capped/ambiguous. It may improve confidence slightly because stock exists, but it does not create a high-stock claim.

## API Examples

Health check:

```powershell
Invoke-RestMethod http://127.0.0.1:8020/health/live
```

Bulk hot-products request shape:

```powershell
$body = @{
  requestId = "req-hot-demo"
  generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
  marketplace = "wildberries"
  scope = @{
    sourceCategory = "Товары для дома"
    sourceSubcategories = @("Светильники бра")
    parserRunId = "wb_products_demo"
    rankRunId = "wb_rank_demo"
    reviewRunIds = @()
  }
  products = @(
    @{
      productKey = "wildberries:202825367"
      wbProductId = 202825367
      wbRootId = 119691805
      name = "Бра светильник настенный"
      sourceCategory = "Товары для дома"
      sourceSubcategory = "Светильники бра"
      walletPrice = 709
      rating = 4.9
      feedbackCount = 2121
      position = 1
      positionState = "observed"
      observedRangeLimit = 100
      totalQuantity = 40
      snapshotAtUtc = "2026-05-25T00:00:00Z"
    }
  )
  options = @{
    maxRecommendations = 10
    minConfidence = 0.45
    algorithm = "rule_based_hot_products_v1"
  }
} | ConvertTo-Json -Depth 8

Invoke-RestMethod `
  -Method Post `
  -Uri http://127.0.0.1:8020/api/v1/recommendations/hot-products `
  -Body $body `
  -ContentType "application/json"
```

Sample recommendation:

```json
{
  "status": "completed",
  "algorithm": "rule_based_hot_products_v1",
  "algorithmVersion": "1.0.0",
  "modelVersion": "none",
  "recommendations": [
    {
      "score": 92.45,
      "confidence": 0.801,
      "title": "Карточка выглядит перспективной для анализа",
      "reason": "У карточки есть несколько рыночных признаков: позиция, рейтинг, отзывы, цена и доступность выглядят достаточно сильными для дальнейшей проверки. Результат требует проверить маржинальность, поставщика и конкуренцию.",
      "factors": [
        {"code": "position", "label": "Позиция", "value": "#1", "weight": 0.3, "direction": "positive"},
        {"code": "rating", "label": "Рейтинг", "value": 4.9, "weight": 0.2, "direction": "positive"},
        {"code": "reviews", "label": "Отзывы", "value": 2121, "weight": 0.2, "direction": "positive"},
        {"code": "price", "label": "Цена", "value": "в основном диапазоне группы", "weight": 0.15, "direction": "positive"},
        {"code": "stock", "label": "Остаток", "value": "есть, значение может быть ограничено", "weight": 0.05, "direction": "neutral"},
        {"code": "completeness", "label": "Полнота данных", "value": "6/6", "weight": 0.1, "direction": "positive"}
      ],
      "debug": null
    }
  ]
}
```

Not enough data example:

```json
{
  "status": "not_enough_data",
  "recommendations": [],
  "warnings": [
    "Product snapshot contains 6 products; at least 20 are required for rule-based comparison."
  ]
}
```

Unsupported algorithm example:

```json
{
  "status": "unsupported",
  "algorithm": "experimental_hot_products",
  "recommendations": [],
  "warnings": [
    "Algorithm 'experimental_hot_products' is not supported by this service."
  ]
}
```

## Product Advice And Jobs

Current single-product advice remains a contract scaffold:

- `POST /api/v1/recommendations/product-advice`
  - missing product identity: `status = not_enough_data`
  - product identity present: `status = unsupported`
- `POST /api/v1/jobs/product-advice`
  - service-generated `jobId`;
  - in-memory state only;
  - immediate completion with the same contract-only product-advice result.

The in-memory job store is local/development-only. It is not durable, is not shared across service instances, and must be replaced before production background processing.

If `requestId` is omitted for `POST /api/v1/jobs/product-advice`, the service generates a local request id. Direct recommendation endpoints require `requestId`.

## Error Shape

Validation and service errors return:

```json
{
  "errorCode": "validation_error",
  "message": "Request payload validation failed.",
  "details": {},
  "requestId": "req-example",
  "traceId": "trace"
}
```

For invalid JSON or payloads that cannot be parsed enough to read `requestId`, `requestId` may be `null`.

## Limitations

- No guaranteed profitability.
- No demand, revenue, margin, or growth claims.
- No official marketplace endorsement.
- No ML model yet.
- No PostgreSQL writes from Python.
- No .NET persistence integration yet.
- No frontend UI integration yet.

Recommended Stage 6C: add .NET caller and persistence integration. The backend should prepare snapshots, call Python, validate returned candidates, store accepted recommendations with hash/explanation/freshness fields, and expose saved recommendations to the UI.
