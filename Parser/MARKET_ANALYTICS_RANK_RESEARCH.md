# Market Analytics Rank And Recommendation Research

## Decision

- Fix the current seller-facing UX in `Market Analytics` first.
- Add parser rank snapshots next.
- Do not start a serious AI recommendation model until repeated rank, price, feedback, review, and quantity snapshots exist over time.

The current product parser already gathers market product rows from WB search result fetches. Its canonical `products.jsonl` is a deduplicated product artifact, so it is not a truthful rank snapshot after price-range traversal and dedupe.

## Current Parser Facts

- Product artifacts already preserve product ids, root ids, source category and subcategory, source query, region destination, brand, seller, prices, rating, feedback count, quantity, images, and observed WB subject fields.
- Review artifacts already preserve review timestamps, review text/pros/cons, root/product attribution fields, and observed seller replies.
- The current product run contains `Обувь для девочек` and `Обувь для мальчиков`.
- The current parser ingestion CLI stages only artifact prefixes with `--limit`; it does not select a balanced staging subset by `source_subcategory`.
- Prefix staging such as `--limit 100` or `--limit 1000` stays inside the girls footwear block. A bounded prefix that crosses into boys footwear must reach past the girls block, for example `--limit 90000`.

## Search Rank Snapshot Design

### Snapshot boundary

Treat rank as result-context data, not as a product attribute:

- one declared query/category result context;
- one region/destination and sort/filter context;
- one ordered page set;
- one observed product position before product dedupe.

The next parser artifact should be `product_rank_snapshots.jsonl`. It must not infer rank from canonical product rows.

### Proposed row contract

Each rank row should carry:

- `schema_version`;
- `parser_run_id`;
- `observed_at_utc`;
- `marketplace`;
- `source_category`;
- `source_subcategory`;
- `query`;
- `source_region_dest`;
- `sort`;
- request/filter context fingerprint;
- `page`;
- `position_on_page`;
- `absolute_position`;
- `wb_product_id`;
- `wb_root_id`;
- fetch status or gap metadata when a requested page is unavailable.

The first implementation should keep the first 1000 results for a single stable result context. Price-split traversal can still feed broad product collection, but it must not be merged into a fake top-1000 rank.

### Linkage and future UI

- Link rank rows to product rows by `parser_run_id + wb_product_id`, with root id available for grouping.
- Later expose rank with its query/category/date context in Market Analytics.
- Only show rank trend after repeated snapshots exist.

Official WB seller analytics already exposes search-position reporting for a seller's own products and search texts. That is useful context, but Market Analytics still needs its own competitive result snapshots for marketplace products. Source: [WB Analytics API documentation](https://dev.wildberries.ru/en/openapi/analytics).

## Hot Product Signal Model

### Signal taxonomy

| Layer | Examples |
| --- | --- |
| Raw observed signals | rank snapshots, prices, discount, rating, feedback count, review timestamps, observed quantity, query/category appearances |
| Derived signals | rank delta, rank stability, feedback velocity, fresh-review velocity, price stability, quantity delta, repeated top-query appearances |
| Score | rule-based demand and trajectory scores before ML |
| Confidence | snapshot count, time coverage, fetch coverage, context coverage |
| Explanation | seller-readable reasons tied to observed signals |

### Recommendation semantics

- `Сильные товары сейчас` should explain strong current visibility and engagement.
- `Перспективные товары` should explain improving trajectory with enough repeated evidence.
- A hot product must not mean only `много отзывов`.
- Explanations should mention concrete evidence such as high rank, repeated rank improvement, fresh review flow, feedback growth, price stability, or quantity decline when those signals are observed reliably.

## Data Requirements

| Data | Current state | Next action |
| --- | --- | --- |
| Product ids, root ids, title, category, brand, seller, images | present | retain historical snapshots |
| Price and discount fields | present per product run | build price time series |
| Rating and feedback count | present per product run | build deltas across runs |
| Quantity-like field | present as observed total quantity | validate semantics before scoring stock demand |
| Review timestamps and content | present in review artifacts | keep collection history and internal coverage caveats |
| Rank page and absolute position | missing from canonical product artifact | add dedicated rank snapshot output |
| Stable query/category/region result context | partial | persist full rank request context |
| Sponsored/ad position | not established | research separately before use |
| Training labels or proxy targets | missing | define only after repeated history exists |

## Delivery Stages

1. Add the rank snapshot contract and preserve ordered first-1000 result positions for a stable query/category context.
2. Run repeated manual or scheduled snapshots and compare rank, price, feedback, review freshness, and quantity over time.
3. Add a rule-based hot-product score with explanation and confidence.
4. Prepare an ML dataset with time-aware/category-aware splits and leakage checks.
5. Add a model service only after the data and targets are mature.
