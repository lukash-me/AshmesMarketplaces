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

## Commands

From the repository root:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\runner.py --smoke-only
& .\Parser\.venv\Scripts\python.exe .\Parser\runner.py
```

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
