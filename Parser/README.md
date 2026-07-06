# Parser Safe Runner

Before changing parser behavior or running parser smoke, read
`Parser/ARCHITECTURE_WORKLOG.md`. It records architecture decisions, repeated
failure causes, encoding rules, proxy/niche names and monitoring invariants.

## Current production flow

Production parsing is a one-cycle worker process. The Docker service starts
`Parser/app/cycle_runner.py` with
`Parser/presets/production/market_refresh_selected_niches_batched.prod.json`.
The old root-level runners are kept as compatibility modules while the new
package layout makes the pipeline shape explicit:

- `Parser/app/` contains the market-refresh entrypoint and shared pipeline
  context objects;
- `Parser/clients/` exposes Wildberries HTTP client wrappers;
- `Parser/pipelines/products/`, `ranks/`, `details/`, `reviews/` and
  `logistics/` contain stage entrypoints and adapters;
- `Parser/presets/production/`, `local/` and `test/` separate runtime presets;
- `Parser/contracts/`, `exporters/` and `manifests/` are reserved for shared
  artifact contracts as runner code is split further;
- `Parser/legacy/` documents old entrypoints that are not part of the primary
  production path.

The production flow is:

1. flush and poll unfinished local outbox batches from previous parser cycles;
2. discover product cards for the configured niches;
3. process cards in batches of about 100 unique WB products;
4. enrich each batch with rank, details, reviews and logistics evidence;
5. save completed batch payloads to local durable outbox when queue delivery is enabled;
6. write JSONL manifests and artifacts under `Parser/output/runs/`;
7. send completed batch payloads to the backend server queue when
   `PARSER_BATCH_QUEUE_URL` is configured;
8. flush/poll outbox again, cleanup server-confirmed payloads and write a cycle
   report;
9. exit. The next launch is controlled by Docker restart policy, cron or another scheduler.

The parser process does not serve HTTP traffic. API, analytics worker and parser
remain separate processes.

For smoke checks, limit one cycle without changing presets:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\app\cycle_runner.py `
  --config .\Parser\presets\production\market_refresh_selected_niches_batched.prod.json `
  --mode batched_full_enrichment `
  --smoke-source-subcategory "Коврики для ванной" `
  --smoke-max-batches 1
```

`Parser/app/market_refresh.py` remains the lower-level pipeline entrypoint used
by the cycle runner and by legacy manual diagnostics.

## Proxy management and niche scheduling

Production proxy settings are stored in the backend database and are managed
from the admin UI (`Parser monitoring` -> `Proxies`). The parser supervisor
loads runtime assignments from:

```text
GET /api/v1/parser/runtime/proxy-assignments
```

Use `PARSER_BATCH_QUEUE_URL` or `PARSER_RUNTIME_PROXY_ASSIGNMENTS_URL` to point
the supervisor to the API, and `PARSER_API_KEY` when the backend is configured
to require `X-Parser-Api-Key`.

The tracked `Parser/presets/production/proxy_mapping.json` remains only a safe
development fallback without credentials. `proxy_mapping.local.json` is not a
production source of truth anymore and should not be used in normal runbooks.
After deployment, add proxies manually in the UI and assign WB leaf niches from
the WB category dropdown. Proxies without a niche are visible in the UI but are
excluded from runtime launches.

During batched processing every complete-card batch records the selected
`proxyKey` in the pipeline manifest and passes proxy transport to child steps
through environment variables. Passwords are not written to logs or manifests.
If a niche is disabled in the mapping, or if its proxy is in cooldown after an
error, that batch is deferred and the rest of the parser cycle continues. This
keeps a broken niche/proxy from stopping unrelated niches.

## Durable batch outbox

The batched enrichment pipeline persists each completed batch locally before the
batch is handed to the backend queue. Docker production enables this mode with:

```powershell
$env:PARSER_BATCH_QUEUE_URL = "http://localhost:5000/api/v1/parser"
$env:PARSER_OUTBOX_DIR = "E:\AshmesMarketplaces\Parser\output\outbox"
$env:PARSER_INSTANCE_ID = "parser-home-goods-01"
```

When enabled, the parser:

1. saves batch metadata in `outbox.sqlite3`;
2. saves the full batch payload as JSON under `payloads/`;
3. sends `POST /api/v1/parser/batches`;
4. polls `/api/v1/parser/batches/{externalBatchId}/status`;
5. deletes the payload file only after the server reports `completed`.

On restart the parser first retries and polls active local batches, then starts
new marketplace collection. The `(parserInstanceId, externalBatchId)` key keeps
server submission idempotent.

This parser stage does not run migrations and does not serve user traffic.
Production Docker delivery to the backend is performed through the durable batch
queue. The old `--stage-to-db` path remains only for manual development
diagnostics.

## Network Strategy

Run the parser from an environment where WB is reachable without the project dev stack depending on it:

- preferred: separate runner environment without VPN;
- acceptable: separate Windows profile or VM;
- conditional: VPN split tunneling for Python, Playwright Chromium, and WB/static basket domains.

Backend and frontend development can continue with VPN enabled; parser execution is a separate manual operation.

## Browser Sessions

WB browser cookies are acquired through Playwright persistent contexts. Sessions
are scoped by `proxyKey`: every proxy has its own browser profile, cookie jar,
`x_wbaas_token`, cooldown and error state. Credentials are received from the
backend runtime proxy assignments endpoint. Do not store production proxy
credentials in parser config files.

Local setup:

```powershell
& .\Parser\.venv\Scripts\python.exe -m pip install -r .\Parser\requirements.txt
& .\Parser\.venv\Scripts\python.exe -m playwright install chromium
```

Useful diagnostics:

- `PARSER_BROWSER_HEADED=1` opens the browser locally;
- `PARSER_SESSION_TTL_MINUTES=60` controls token cache TTL;
- `PARSER_FORCE_REFRESH_TOKEN=1` refreshes the current proxy session.

By default profiles are stored under `Parser/output/browser_profiles/{proxyKey}`
and session metadata under `Parser/output/browser_sessions/{proxyKey}.json`.

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
& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\products\runner.py `
  --config .\Parser\presets\home_goods_demo.env `
  --smoke-only
```

That preset selects:

- `Органайзеры для хранения вещей`;
- `Коврики для ванной`;
- `Светильники бра`.

The preset uses seller-facing parent category label `Товары для дома`. WB static
menu currently exposes that parent through the menu name `Дом`; the runner
accepts either parent label during category discovery while writing the
configured parent value into run metadata.

## Commands

From the repository root:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\products\runner.py --smoke-only
& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\products\runner.py
```

Start the home-goods demo with a tiny run before removing caps:

```powershell
$env:PARSER_MAX_CATALOG_PAGES_PER_SUBCATEGORY = "1"
$env:PARSER_MAX_ITEMS_PER_SUBCATEGORY = "50"
& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\products\runner.py `
  --config .\Parser\presets\home_goods_demo.env
Remove-Item Env:PARSER_MAX_CATALOG_PAGES_PER_SUBCATEGORY
Remove-Item Env:PARSER_MAX_ITEMS_PER_SUBCATEGORY
```

For a bounded demo-quality run use `8` catalog pages and `750` items per selected subcategory, then inspect the manifest, canonical JSONL, image availability, price/rating/feedback coverage, duplicate count, and retries before any full run.

From `Parser/`:

```powershell
.\.venv\Scripts\python.exe .\pipelines\products\runner.py --smoke-only
.\.venv\Scripts\python.exe .\pipelines\products\runner.py
```

Do not use bare `python .\pipelines\products\runner.py` unless that Python environment has the parser dependencies installed. To prepare a different environment:

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

`pipelines/ranks/runner.py` is a separate manual path for observed WB search-result positions. It does not infer rank from `products.jsonl`, does not use price splitting, and does not write product-card exports.

Start with one context and one page:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\ranks\runner.py `
  --config .\Parser\presets\home_goods_search_rank_demo.json `
  --context-id home_storage_organizers `
  --top-n 100 `
  --max-pages 1
```

Run the home-goods top-1000 search-rank demo after the one-page smoke is reviewed:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\ranks\runner.py `
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

`app/market_refresh.py` orchestrates rank, product, and review parser child runs through one manual command. It does not write to PostgreSQL and does not replace standalone parser runners.

Dry-run the home-goods smoke pipeline:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\app\market_refresh.py `
  --config .\Parser\presets\market_refresh_home_goods_demo.json `
  --mode smoke `
  --dry-run
```

Run the smoke pipeline:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\app\market_refresh.py `
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
& .\Parser\.venv\Scripts\python.exe .\Parser\app\market_refresh.py `
  --config .\Parser\presets\market_refresh_selected_niches_100k.json `
  --mode smoke `
  --dry-run

& .\Parser\.venv\Scripts\python.exe .\Parser\app\market_refresh.py `
  --config .\Parser\presets\market_refresh_selected_niches_100k.json `
  --mode bounded

& .\Parser\.venv\Scripts\python.exe .\Parser\app\market_refresh.py `
  --config .\Parser\presets\market_refresh_selected_niches_100k.json `
  --mode full
```

## Future Backend Handoff

Future ingestion should read canonical JSONL into raw/staging first. The reviewed upsert key should be `marketplace + wb_product_id`, preserving `parser_run_id`, `parsed_at_utc`, source category/query, and source region for traceability. Product, brand, category, seller, image, and app-managed field ownership mappings need a separate reviewed backend schema/ingestion task.

## Reviews And Review Replies Runner

`pipelines/reviews/runner.py` is a separate manual slice over an existing product parser run. It fetches public WB review payloads by product `wb_root_id`, filters canonical review rows to selected input `wb_product_id` values observed in each feedback row `nmId`, and keeps `review_attribution_mode=root_payload` so downstream code does not treat root payload reviews as exact variant ownership.

Start with small product limits:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\reviews\runner.py `
  --products-run-dir .\Parser\output\runs\wb_products_20260521_003158_b1cf9c3 `
  --limit-products 10 `
  --smoke-only

& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\reviews\runner.py `
  --products-run-dir .\Parser\output\runs\wb_products_20260521_003158_b1cf9c3 `
  --limit-products 10
```

For the home-goods demo run reviews per selected subcategory so one artifact prefix does not dominate the sample:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\reviews\runner.py `
  --products-run-dir .\Parser\output\runs\<home_goods_products_run_id> `
  --source-subcategory "Органайзеры для хранения вещей" `
  --limit-products 10 `
  --smoke-only

& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\reviews\runner.py `
  --products-run-dir .\Parser\output\runs\<home_goods_products_run_id> `
  --source-subcategory "Органайзеры для хранения вещей" `
  --limit-products 100
```

Repeat the subcategory-specific review smoke and bounded run for `Коврики для ванной` and `Светильники бра` after the product manifest and sample rows look presentable.

Resume a review run with its existing scope:

```powershell
& .\Parser\.venv\Scripts\python.exe .\Parser\pipelines\reviews\runner.py `
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
