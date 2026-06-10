# Parser Safe Runner

This parser stage is an isolated, manual WB product-card data pipeline. It does not write to the backend database, does not run migrations, and does not start any scheduler or worker.

## Network Strategy

Run the parser from an environment where WB is reachable without the project dev stack depending on it:

- preferred: separate runner environment without VPN;
- acceptable: separate Windows profile or VM;
- conditional: VPN split tunneling for Python, Chrome/Selenium, ChromeDriver/SeleniumBase, and WB/static basket domains.

Backend and frontend development can continue with VPN enabled; parser execution is a separate manual operation.

## Configuration

Copy `Parser/config.example.env` to `Parser/.env` and keep the allowlist small. Full-category traversal is intentionally disabled by configuration validation.

Important safety defaults:

- `PARSER_SUBCATEGORY_ALLOWLIST` must contain explicit subcategories;
- `PARSER_MAX_CONCURRENT` is limited to `1` or `2`;
- `PARSER_BATCH_SIZE` is limited to `1..10`;
- catalog page and item caps default to `0`, which means no artificial cap inside the explicit allowlist;
- set positive `PARSER_MAX_CATALOG_PAGES_PER_SUBCATEGORY` and `PARSER_MAX_ITEMS_PER_SUBCATEGORY` values only for tiny debug runs.

The default example keeps the footwear validation scope. For the Market Analytics home-goods demo, use the tracked preset:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\runner.py `
  --config .\Parser\presets\home_goods_demo.env `
  --smoke-only
```

That preset selects:

- `Органайзеры для хранения вещей`;
- `Коврики для ванной`;
- `Светильники бра`.

The preset uses seller-facing parent category label `Товары для дома`. WB static menu currently exposes that parent through the menu name `Дом` and SEO label `Товары для дома`; the runner accepts either parent label during category discovery while writing the configured parent value into run metadata.

## Commands

From the repository root:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\runner.py --smoke-only
& .\Parser\.venv\Scripts\python.exe .\Parser\runner.py
```

Start the home-goods demo with a tiny run before removing caps:

```powershell
$env:PARSER_MAX_CATALOG_PAGES_PER_SUBCATEGORY = "1"
$env:PARSER_MAX_ITEMS_PER_SUBCATEGORY = "50"
& .\Parser\.venv\Scripts\python.exe .\Parser\runner.py `
  --config .\Parser\presets\home_goods_demo.env
Remove-Item Env:PARSER_MAX_CATALOG_PAGES_PER_SUBCATEGORY
Remove-Item Env:PARSER_MAX_ITEMS_PER_SUBCATEGORY
```

For a bounded demo-quality run use `8` catalog pages and `750` items per selected subcategory, then inspect the manifest, canonical JSONL, image availability, price/rating/feedback coverage, duplicate count, and retries before any full run.

From `Parser/`:

```powershell
.\.venv\Scripts\python.exe .\runner.py --smoke-only
.\.venv\Scripts\python.exe .\runner.py
```

Do not use bare `python .\runner.py` unless that Python environment has the parser dependencies installed. To prepare a different environment:

```powershell
python -m pip install -r requirements.txt
```

The runner writes one folder per run:

```text
Parser/output/runs/<parser_run_id>/
  manifest.json
  products.jsonl
  products.csv
  products.xlsx
  errors.jsonl
  runner.log
```

`products.jsonl` is the canonical machine-readable contract. CSV and XLSX are derived from the same canonical rows.

## Search Rank Snapshot Runner

`rank_runner.py` is a separate manual path for observed WB search-result positions. It does not infer rank from `products.jsonl`, does not use price splitting, and does not write product-card exports.

Start with one context and one page:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\rank_runner.py `
  --config .\Parser\presets\home_goods_search_rank_demo.json `
  --context-id home_storage_organizers `
  --top-n 100 `
  --max-pages 1
```

Run the home-goods top-1000 search-rank demo after the one-page smoke is reviewed:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\rank_runner.py `
  --config .\Parser\presets\home_goods_search_rank_demo.json
```

Each rank run writes:

```text
Parser/output/runs/<wb_rank_run_id>/
  manifest.json
  product_rank_snapshots.jsonl
  rank_page_fetches.jsonl
  errors.jsonl
  runner.log
```

`product_rank_snapshots.jsonl` is one row per observed product position in one fixed search context. `rank_page_fetches.jsonl` records succeeded, empty, failed, or stopped pages so missing pages are explicit instead of becoming fake ranks.

## Market Analytics Refresh Pipeline

`market_refresh_runner.py` orchestrates rank, product, and review parser child runs through one manual command. It does not write to PostgreSQL and does not replace standalone parser runners.

Dry-run the home-goods smoke pipeline:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\market_refresh_runner.py `
  --config .\Parser\presets\market_refresh_home_goods_demo.json `
  --mode smoke `
  --dry-run
```

Run the smoke pipeline:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\market_refresh_runner.py `
  --config .\Parser\presets\market_refresh_home_goods_demo.json `
  --mode smoke
```

Regular manual refresh should use `--mode bounded` after smoke output quality is reviewed. The pipeline writes a coordinating manifest and links child parser run directories:

```text
Parser/output/pipelines/<market_refresh_run_id>/
  pipeline_manifest.json
  pipeline.log

Parser/output/runs/<wb_rank_run_id>/
Parser/output/runs/<wb_products_run_id>/
Parser/output/runs/<wb_reviews_run_id>/
```

Use `--resume-run-dir <pipeline-dir>` to skip succeeded child steps and retry failed or skipped dependency steps. Use `--force-step rank|products|reviews` only when a child step must be rerun intentionally.

For a larger selected-niche opportunity run, use the dedicated preset. Start with smoke, then bounded, and run full only after manifests and coverage look sane:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\market_refresh_runner.py `
  --config .\Parser\presets\market_refresh_selected_niches_100k.json `
  --mode smoke `
  --dry-run

& .\Parser\.venv\Scripts\python.exe .\Parser\market_refresh_runner.py `
  --config .\Parser\presets\market_refresh_selected_niches_100k.json `
  --mode bounded

& .\Parser\.venv\Scripts\python.exe .\Parser\market_refresh_runner.py `
  --config .\Parser\presets\market_refresh_selected_niches_100k.json `
  --mode full
```

## Future Backend Handoff

Future ingestion should read canonical JSONL into raw/staging first. The reviewed upsert key should be `marketplace + wb_product_id`, preserving `parser_run_id`, `parsed_at_utc`, source category/query, and source region for traceability. Product, brand, category, seller, image, and app-managed field ownership mappings need a separate reviewed backend schema/ingestion task.

## Reviews And Review Replies Runner

`reviews_runner.py` is a separate manual slice over an existing product parser run. It fetches public WB review payloads by product `wb_root_id`, filters canonical review rows to selected input `wb_product_id` values observed in each feedback row `nmId`, and keeps `review_attribution_mode=root_payload` so downstream code does not treat root payload reviews as exact variant ownership.

Start with small product limits:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\reviews_runner.py `
  --products-run-dir .\Parser\output\runs\wb_products_20260521_003158_b1cf9c3 `
  --limit-products 10 `
  --smoke-only

& .\Parser\.venv\Scripts\python.exe .\Parser\reviews_runner.py `
  --products-run-dir .\Parser\output\runs\wb_products_20260521_003158_b1cf9c3 `
  --limit-products 10
```

For the home-goods demo run reviews per selected subcategory so one artifact prefix does not dominate the sample:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\reviews_runner.py `
  --products-run-dir .\Parser\output\runs\<home_goods_products_run_id> `
  --source-subcategory "Органайзеры для хранения вещей" `
  --limit-products 10 `
  --smoke-only

& .\Parser\.venv\Scripts\python.exe .\Parser\reviews_runner.py `
  --products-run-dir .\Parser\output\runs\<home_goods_products_run_id> `
  --source-subcategory "Органайзеры для хранения вещей" `
  --limit-products 100
```

Repeat the subcategory-specific review smoke and bounded run for `Коврики для ванной` and `Светильники бра` after the product manifest and sample rows look presentable.

Resume a review run with its existing scope:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\reviews_runner.py `
  --resume-run-dir .\Parser\output\runs\<wb_reviews_run_id>
```

Each review run writes:

```text
Parser/output/runs/<wb_reviews_run_id>/
  manifest.json
  reviews.jsonl
  review_replies.jsonl
  review_fetch_results.jsonl
  errors.jsonl
  runner.log
  raw/root_feedbacks/<wb_root_id>.json.gz
```

`reviews.jsonl` and `review_replies.jsonl` are the normalized contracts. Root WB payload retention is configurable with `PARSER_REVIEWS_RETAIN_RAW_PAYLOADS` or `--no-retain-raw-payloads`; retained raw payloads stay compressed because the public WB review payload shape is not a stable backend contract.

The reviews runner uses bounded low concurrency, request delay jitter, retry, and exponential backoff. Keep the first validation runs at `10`, then `100`, then a reviewed `1000` selected products before broader batches. Review public endpoint caps and error logs before treating the output as complete review history.
