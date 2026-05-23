# AshmesMarketplaces Backlog

## Purpose And Guardrails

This backlog is the operational roadmap. It does not override architecture decisions, API contracts, database conventions, or migration policy.

Primary source-of-truth docs:

- `docs/AI_CONTEXT.md`
- `docs/ARCHITECTURE.md`
- `docs/DATABASE.md`
- `docs/DECISIONS.md`
- `docs/ENTITY_CONVENTIONS.md`
- `docs/API_GUIDELINES.md`
- `docs/API_IMPLEMENTATION_PATTERN.md`

Requirements and visual references live under `Documents/`. They are product context and UX inspiration, not source code. Competitor references such as MPStats, Moneyplace, MarketGuru, Mayak, TrueStats, TradingView, CoinMarketCap, CoinGecko, and Coinglass are workflow/density references only.

Hard constraints:

- No migrations except in dedicated reviewed schema tasks.
- Do not change existing API contracts/routes unless explicitly scoped.
- Do not invent enum values, fake analytics, fake signals, fake demand/growth numbers, or effective permissions.
- Parser, ML/Intelligence, Docker, infrastructure, authorization hardening, and live marketplace automation are separate reviewed stages.
- Keep frontend feature-oriented; do not mirror every backend entity as top-level navigation by default.
- Use `Frontend/src/styles/tokens.css` as the visual theme source of truth.

## Critical Current Assessment

Strongest parts:

- Backend foundation is broad: Domain entities, EF Core mappings, PostgreSQL runtime setup, initial migration, CRUD API coverage, Auth/Security Foundation, and Development Seed Foundation exist.
- Frontend foundation is real: Vue 3/Vite protected shell, auth flow, shared primitives, obsidian theme tokens, and multiple production-style read-only vertical slices exist.
- Architecture discipline is strong: ADRs, entity conventions, API pattern docs, migration policy, UTC policy, JSONB policy, and no-fake-analytics policy are documented.

Weakest parts:

- The product still lacks a real operator landing screen: `Overview` remains a placeholder.
- Backend APIs are mostly CRUD/storage. Analytical aggregation, dashboards, comparisons, exports, workflow commands, and real signal systems are absent.
- CRUD endpoints are still anonymous for compatibility. Workspace authorization and permission enforcement are not implemented.
- Parser backend ingestion has a raw/staging code path, dedicated parser ingestion migrations, product/review/rank staging CLI support, and a read-only parser staging observability API/UI slice. Market Analytics now presents parser staging products as a seller-facing market research area, while staged rank still needs a parser product read model and UI exposure. Intelligence is not integrated with backend/API/frontend.
- Testing, observability, performance strategy, export strategy, and production deployment are not mature.

Implemented:

- Backend CRUD for catalog, products, operations, advertising, finance, users/workspaces, access, rules, and recommendations.
- Auth endpoints: login, refresh, logout, me.
- Development-only seed with deterministic local accounts and demo records.
- Frontend auth, protected shell, shared UI primitives, and read-only slices for Products, Orders, Reviews, Campaigns, Logistics, Expenses, Recommendations, Access Settings, and Market Analytics over parser staging rows.
- Existing slice doctrine: dense operational tables, compact filters, pagination, URL query sync where useful, loading/error/empty states, selected row state, keyboard row opening, and detail drawers.
- Parser product-card foundation: safe manual WB runner, explicit subcategory allowlist, network smoke checks, rate-limit-aware execution knobs, canonical JSONL output, derived CSV/XLSX exports, manifest, structured run/error capture, home-goods demo preset, and `price_split|direct` fetch modes.
- Parser reviews/replies slice: manual runner over existing `products.jsonl`, public WB root feedback payload fetch by `wb_root_id`, bounded low concurrency, canonical review/reply JSONL, compressed raw root payload retention, fetch audit, structured errors/logs, and resume.
- Parser search-rank slice: manual WB search-rank runner writes `product_rank_snapshots.jsonl`, `rank_page_fetches.jsonl`, and manifest for observed query-context positions without price splitting or product enrichment.
- Parser orchestration slice: manual Market Analytics refresh runner executes rank -> products -> reviews, writes a pipeline manifest/log, streams progress to console, supports smoke/bounded/full modes, resume, skips, force-step, and dry-run, and performs no DB writes by default.
- Backend parser ingestion foundation: ingestion entities/configurations, reviewed migrations, and a manual CLI/application service for parser run validation, raw file/run registration, product/review/reply staging, rank snapshot/page-fetch staging, review root fetch completeness metadata, import audit/errors, and selective existing-product promotion rules without scheduler coupling.
- Parser staging observability slice: staging-only `/api/v1/parser/*` read endpoints plus `/parser/products` and `/parser/reviews` frontend routes with lineage, observed review replies, and honest partial/capped/root-attribution review warnings.

Partially implemented:

- Products: backend CRUD and frontend list/detail exist; create/edit/delete/media mutation and real analytics are absent.
- Orders: backend CRUD and read-only frontend list/detail exist; fulfillment workflows and real order analytics are absent.
- Reviews: backend CRUD and read-only frontend list/detail with linked domain replies exist; parser review/reply output and separate parser staging observability pages exist, but current parser reviews/replies must stay raw/staging because they are partial/capped/root-attributed; reply mutation, autoreply workflow, and AI assistance are absent.
- Campaigns: backend CRUD and read-only frontend list/detail with linked metrics exist; operations, bid workflows, automation, and real campaign analytics are absent.
- Logistics: backend CRUD and read-only frontend list/detail with warehouse context exist; stock risk semantics, forecasting, geography, and parser-backed analysis are absent.
- Expenses: backend CRUD and read-only frontend list/detail with category context exist; profitability, ABC analysis, reporting, and accounting workflows are absent.
- Recommendations: backend CRUD/storage and read-only frontend list/detail exist; ML generation, score interpretation, apply/accept/reject workflows, and target name hydration are absent.
- Access: backend access CRUD and read-only membership surface exist; authorization enforcement, permission policy matrix, and user/role/workspace mutations are absent.
- Parser: product-card, review/reply, search-rank, and manual refresh pipeline output contracts exist; backend raw/staging ingestion code, reviewed migrations, fresh home-goods product/review/rank staging, and Market Analytics read UI/API exist; rank API/UI exposure, public review pagination/full-history research, reviewed domain upsert mapping, and scheduler execution are absent.
- Products heat/signal presentation exists, but it is not real business intelligence.

Still missing:

- Overview dashboard and real charting workflows.
- Market/category/seller/brand intelligence workflows.
- Catalog/reference selector UX for marketplace, brand, category, warehouse, user, workspace, and role contexts.
- Product-card SEO/content/variant workflows.
- Review operator and autoreply workflows.
- Campaign bid, budget, schedule, stop-rule, and automation workflows.
- Pricing automation and marketplace action workflows.
- Stock risk, warehouse/geography, logistics cost, profitability, and ABC analysis.
- Applied parser ingestion schema/runtime verification and ML/recommendation service integration.
- Reports/export workflows.
- Workspace authorization and production-grade security hardening.

## Near-Term Roadmap

### 1. Market Analytics Fresh Staging Verification

Priority: critical
Scope: medium-to-large
Layers: backend, parser evidence, docs

Backend ingestion code exists for existing parser artifacts: manual validation, run/file registration, import audit/errors, product staging, review root-fetch staging, review/reply staging, constrained product promotion rules, a dedicated parser ingestion migration, and read-only parser staging API/Market Analytics UI.

Next approved DB-backed task should:

- stage the latest bounded home-goods product parser run into PostgreSQL with idempotency and audit verification;
- stage the matching home-goods review parser runs into PostgreSQL and verify review visibility from Market Analytics;
- verify stale footwear demo rows no longer dominate the seller-facing demo database;
- keep product domain creation blocked until seller-managed fields such as real `SkuSeller` and status policy are decided;
- verify review staging preserves parser run lineage, root fetch cap evidence, fallback reply identity risk, and root-level attribution without importing domain `Reviews` or `ReviewReplies`.

Before any domain review ingestion, research the public WB review payload cap/pagination behavior: current root payloads can report more feedback rows than the endpoint returns, and the interrupted run is only a partial snapshot. Keep scheduler, broad domain parsing, ML enrichment, and all-category traversal out of this stage.

### 2. Search Rank Read Model And Market Analytics Position Column

Priority: critical
Scope: medium-to-large
Layers: backend, frontend

Stage 1 rank DB ingestion is implemented. `validate-ranks` and `stage-ranks` ingest real `product_rank_snapshots.jsonl` and `rank_page_fetches.jsonl` artifacts into `ParserRankSnapshotRows` and `ParserRankPageFetches` with parser run/file lineage, import audit, and idempotency. Rank must stay context-scoped and must not be inferred from `products.jsonl`.

Next reviewed rank stages should:

- extend the parser products read model with a route-stable nullable rank summary derived only from staged rank snapshots;
- add the Market Analytics `Позиция` column with query/category/page/observed timestamp context and `—` where no real snapshot exists;
- keep page fetch gaps explicit through `ParserRankPageFetches` instead of creating fake positions;
- add opt-in local/demo `market_refresh_runner.py` auto-staging through the existing ingestion CLI after the read model is stable;
- add rank trend semantics only after repeated snapshots are staged and reviewed.

Do not add fake hot-product badges, fake positions, inferred rank, rank trends, demand/growth/risk language, or universal rank claims.

### 3. Overview Dashboard On Real Existing Data

Priority: critical
Scope: large
Layers: frontend, backend later, UX

Build the real `Overview` route after real-data pipeline contracts are stable enough to avoid fake dashboard semantics. Start with existing persisted data where semantics are truthful, then add reviewed backend aggregation endpoints after dashboard contracts are stable.

Initial dashboard should show only truthful semantics from existing data, for example:

- product counts/status distribution;
- order count and revenue proxy only if order fields support it clearly;
- review volume/rating distribution only from persisted review fields;
- ad spend/campaign metrics only from persisted campaign metric fields;
- logistics stock/storage/cost summaries only from persisted logistics fields;
- expense totals only from persisted expense fields;
- recommendation record counts/scores as stored outputs, not live ML insight.

Do not show fake deltas, growth, demand, risk, or signal language.

### 4. Catalog And Reference UX

Priority: high
Scope: medium
Layers: frontend, UX

Backend catalog/reference APIs exist, but many frontend filters still expose raw UUIDs. Add lightweight reference loading and selectors where it materially improves workflows.

Best order:

1. Product filters: marketplace, brand, category selectors/name hydration.
2. Workflow filters: warehouse, user, workspace, role context where existing APIs support it.
3. Lightweight catalog/reference pages only if navigation needs them.

Avoid a generic admin/reference framework.

### 5. Analytics Aggregation Endpoints

Priority: high
Scope: large
Layers: backend, frontend

CRUD list endpoints are not enough for scalable dashboards and trends. Add resource-specific aggregation endpoints only after frontend use cases are stable.

Candidate endpoints:

- product status summary;
- order revenue/count timeline;
- review rating/volume timeline;
- campaign spend/click/impression summary;
- logistics stock/cost summary;
- expense summary;
- recommendation score/type summary.

Keep endpoint surface under `/api/v1`. Do not introduce a generic analytics framework prematurely.

### 6. Shared Table, Filtering, And Dense UX Polish

Priority: high
Scope: medium
Layers: frontend, UX

`DataTable` already supports sortable headers, sticky header, row click, selected row, keyboard row opening, horizontal overflow, and scoped cell slots.

Next useful improvements:

- column visibility;
- density modes and stable row heights;
- reusable numeric/date/currency cell helpers;
- persistent table preferences;
- stronger shared filter primitives and searchable selector patterns.

Do this after Overview/Catalog pressure reveals which primitives are worth sharing.

### 7. Real Signal And Recommendation Semantics

Priority: high
Scope: medium-to-large
Layers: docs, backend, frontend, parser, ML

Current product heat is presentation-only. Before using fire/ember language as business intelligence, define:

- signal taxonomy;
- score ranges;
- input data sources;
- freshness/confidence;
- explanation and snapshot fields;
- UI language for stored vs generated recommendations.

The next honest recommendation path should be rule-based first, using repeated rank snapshots, feedback/review deltas, price changes, stock-like quantity changes after semantic validation, and explanation fields. No UI should claim demand, growth, risk, or actionability unless backed by real backend/API data.

## Later Product Workflows

### Reviews And Autoreply

Priority: high
Scope: large
Layers: frontend, backend, parser, ML later

Current reviews are read-only. Future work should define reply commands, templates/scripts, trigger/minus words, human approval, immutable sent replies, and safe AI suggestions only after ML integration is reviewed.

### Campaign And Pricing Operations

Priority: high/medium
Scope: large
Layers: frontend, backend, parser

Campaigns are read-only. Future work should cover metrics timelines, budget/bid workflows, rule set display, schedules, stop criteria, price rules, competitor price inputs, stock-aware adjustments, and operator approval before marketplace actions.

### Logistics, Finance, And Reporting

Priority: high/medium
Scope: large
Layers: frontend, backend, infra

Current logistics and expenses are read-only. Future work should add reviewed stock risk semantics, geography/demand analysis, profitability/ABC analysis, CSV export first, and XLSX only after format requirements are clear.

### Parser Integration

Priority: high
Scope: large
Layers: parser, backend, infra, docs

`Parser/` now has safe manual WB product-card, review/reply, search-rank, and Market Analytics refresh pipeline runners with canonical output contracts. Backend ingestion code covers raw/staging entities, manual import orchestration, source lineage, audit/error capture, product staging, review root-fetch cap evidence, review/reply staging, rank snapshot/page-fetch staging, dedicated reviewed migrations, parser staging read API/UI for products/reviews, and constrained product promotion boundaries. The next parser/backend stages are the rank parser product read model, Market Analytics `Позиция` column, and opt-in refresh pipeline auto-staging, while public review payload cap/pagination and root-level attribution remain blockers for domain review import or scheduled execution.

### ML/Recommendation Runtime

Priority: medium
Scope: large
Layers: ML, backend, frontend, docs

`Intelligence/` exists, but no runtime integration exists. Define Python/ML service responsibilities, request/response contracts, output schemas, explanation/snapshot JSON, confidence scoring, update cadence, async generation, storage path, and frontend feedback loop.

## Security, Authorization, And Access

Priority: critical
Scope: large
Layers: backend, frontend, infra

Auth exists, but CRUD APIs remain anonymous and workspace permissions are not enforced. Access Settings is intentionally read-only and must not be mistaken for authorization hardening.

Future order:

1. Define permission policy matrix.
2. Protect CRUD and analytics APIs incrementally.
3. Enforce workspace membership, global role, workspace role, resource ownership/workspace, and permissions.
4. Reflect current user capabilities in frontend route/action guards.
5. Add authorization tests and smoke tests.
6. Review token/session security, revocation, rate limiting, lockout, password reset, and audit logging.

Public registration, OAuth, and 2FA remain separate tasks.

## Engineering Hardening

### Testing

Priority: high  
Scope: large

Add focused automated tests for:

- domain invariants;
- EF metadata;
- application services;
- API/auth refresh/session behavior;
- seed idempotency;
- frontend query/filter/table logic;
- protected routes;
- Playwright smoke tests for core workflows.

### Observability

Priority: high  
Scope: medium

Add request correlation IDs, structured logging rules, sensitive-data redaction, health/readiness endpoints, frontend error capture, and parser/ML logging strategy.

Never log passwords, raw tokens, marketplace credentials, or sensitive seller data.

### Performance And Scalability

Priority: medium  
Scope: large

Add pagination hardening, endpoint-specific query profiling, reviewed indexes through migrations only, server-side aggregation, caching strategy, async ingestion strategy, large-table frontend virtualization if proven necessary, and export limits.

### Deployment And Infrastructure

Priority: medium  
Scope: large

Define production topology, secrets handling, migration application process, static frontend hosting, API environment configuration, backups, health checks, rollback process, and local/prod separation.

Local dev scripts must not auto-apply migrations, auto-enable seed, delete volumes, or kill untracked port processes.

## Recommended Next Stages

1. Expose staged rank through the parser product read model and add the Market Analytics `Позиция` column with truthful context.
2. Add opt-in local/demo refresh pipeline auto-staging through the existing ingestion CLI; do not add a scheduler/background worker.
3. Keep review/reply data raw/staging-only while validating cap/pagination/full-history behavior and locking review attribution assumptions before domain import.
4. Build Overview over truthful persisted real data after parser/backend pipeline contracts settle.
5. Add catalog/reference selectors and name hydration for high-friction UUID filters.
6. Specify reviewed analytics aggregation endpoints and real signal semantics.
7. Harden authorization after read-only access surface and policy matrix are clear.
8. Plan ML runtime as a separate reviewed stage.
