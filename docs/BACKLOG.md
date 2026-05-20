# AshmesMarketplaces Backlog

## Source of Truth

This backlog is a product and engineering planning document. It does not override architecture decisions, API contracts, database conventions, or migration policy.

Primary project source of truth:

- `docs/AI_CONTEXT.md`
- `docs/ARCHITECTURE.md`
- `docs/DATABASE.md`
- `docs/DECISIONS.md`
- `docs/ENTITY_CONVENTIONS.md`
- `docs/API_GUIDELINES.md`
- `docs/API_IMPLEMENTATION_PATTERN.md`

Requirements and product context:

- `Documents/Раздел_Первый_Лукашенко_ИСб_22_2_о.docx`
- `Documents/Раздел_Второй_Лукашенко_ИСб_22_2_о.docx`
- `Documents/Практика_Неделя_1_Парсер.docx`
- `Documents/Отчет_о_проделанной_работе_AshmesMarketplaces.docx`

Competitor and workflow references:

- `Documents/Examples/Снимок экрана 2026-05-14 001940.png` - dense product table.
- `Documents/Examples/Снимок экрана 2026-05-14 001957.png` - dashboard cards, trend charts, product-type distribution.
- `Documents/Examples/Снимок экрана 2026-05-14 002025.png` - table column visibility and sorting tools.
- `Documents/Examples/Снимок экрана 2026-05-14 002042.png` - marketplace selector and bulk row tools.
- `Documents/Examples/Снимок экрана 2026-05-14 002059.png` - comparative analysis table.
- `Documents/Examples/Снимок экрана 2026-05-14 002125.png` - sellers table and numeric filters.
- `Documents/Examples/Снимок экрана 2026-05-14 002139.png` - seller detail analytics and chart tabs.
- `Documents/Examples/Снимок экрана 2026-05-14 002152.png` - sales distribution by price segment.
- `Documents/Examples/Снимок экрана 2026-05-14 002218.png` - reviews and autoreply workflows.
- `Documents/Examples/Снимок экрана 2026-05-14 002233.png` - autoreply scripts editor.
- `Documents/Examples/Снимок экрана 2026-05-14 002248.png` - reply template blocks and question workflow.
- `Documents/Examples/Снимок экрана 2026-05-14 002306.png` - niches/categories tables and visual deltas.
- `Documents/Examples/Снимок экрана 2026-05-14 002322.png` - brands table and filter modal.
- `Documents/Examples/Снимок экрана 2026-05-14 002337.png` - campaign settings and bid comparison.
- MPStats, Moneyplace, MarketGuru, Маяк, TrueStats, CoinMarketCap, CoinGecko, Coinglass, and TradingView are UX/workflow/density references only, not pixel-perfect copy targets.

Accepted UI patterns:

- Dark obsidian workstation visual direction.
- Semi-transparent layered surfaces.
- Restrained ember/fire accents as signal semantics, not decorative flames.
- Compact analytics UI and dense readable tables.
- Premium fintech/market intelligence feel.
- Feature-oriented frontend structure; frontend is not a backend entity mirror.
- Semantic theme tokens in `Frontend/src/styles/tokens.css`.
- Products page is the current reference screen for density, row scan speed, filters, URL sync, and read-only detail drawer.

Architecture constraints:

- Do not create migrations except in dedicated reviewed schema tasks.
- EF Core Fluent API remains the persistence source of truth.
- Do not invent enum values or fake analytics numbers.
- Current Products heat is presentation-only until backed by real analytics data.
- Existing API contracts/routes stay stable unless a reviewed API task changes them.
- Parser, ML/Intelligence, Docker, and infrastructure are separate reviewed stages.
- No repository/unit-of-work abstraction, CQRS boilerplate, generic CRUD framework, public registration, OAuth, 2FA, production seed, parser integration, ML inference, or background jobs without explicit scope.

## Current Product State

Strongest current parts:

- Backend foundation is broad: Domain entities, EF Core mappings, PostgreSQL runtime setup, `InitialCreate`, CRUD API coverage, Auth/Security Foundation, and Development Seed Foundation exist.
- Frontend foundation is real: Vue 3/Vite protected shell, auth flow, semantic obsidian theme tokens, and polished Products vertical slice exist.
- Architecture discipline is strong: ADRs, entity conventions, API implementation pattern, migration policy, UTC policy, JSONB policy, and no-fake-analytics policy are documented.

Weakest current parts:

- Frontend coverage is still incomplete. `Overview`, `Expenses`, `Recommendations`, and `Access` are placeholders.
- Backend APIs are mostly CRUD. Analytical aggregation, dashboards, comparisons, exports, workflow commands, and real signal systems are absent.
- CRUD endpoints are anonymous. Workspace authorization and permission enforcement are not implemented.
- Parser and Intelligence are not integrated with backend/API/frontend.
- Testing, observability, performance strategy, and production deployment are future work.

Implemented:

- Backend CRUD for catalog, products, operations, advertising, finance, users/workspaces, access, rules, and recommendations.
- Auth endpoints: login, refresh, logout, me.
- Development-only seed with deterministic local accounts and demo records.
- Frontend auth, protected shell, shared UI primitives, Products list/detail, Orders list/detail, Reviews list/detail with lazy linked review replies, Campaigns list/detail with lazy linked campaign metrics, and Logistics list/detail with drawer-only linked warehouse detail.

Partially implemented:

- Products: backend CRUD and frontend list/detail exist; frontend create/edit/delete/media mutation and real analytics are absent.
- Orders: backend CRUD and read-only frontend list/detail exist; frontend mutations, fulfillment workflows, and real order analytics are absent.
- Reviews: backend CRUD and read-only frontend list/detail exist, including lazy linked review replies; reply mutations, autoreply workflows, parser ingestion, and AI assistance are absent.
- Recommendations: backend CRUD storage exists; recommendation generation, scoring interpretation, and frontend workflows are absent.
- Access: roles/permissions CRUD and auth current-user data exist; enforcement and management UI are absent.
- Campaigns: backend CRUD and read-only frontend list/detail exist, including lazy linked campaign metrics; mutations, automation, bid workflows, and real analytics are absent.
- Logistics: backend CRUD and read-only frontend list/detail exist, including drawer-only linked warehouse detail; mutations, stock risk semantics, forecasting, geography, and parser-backed logistics analysis are absent.
- Expenses: backend CRUD exists; frontend section is a placeholder.
- Heat/signal system: visual scaffolding exists on Products; real business meaning is not implemented.

Missing:

- Market/category/seller/brand intelligence workflows.
- Real dashboard and charting workflows.
- Product-card SEO/content/variant workflows.
- Review operator and autoreply workflows.
- Campaign bid, budget, schedule, and stop-rule workflows.
- Pricing automation and marketplace action workflows.
- Stock risk, warehouse/geography, logistics cost, and profitability analysis.
- Parser ingestion pipeline and ML recommendation service integration.
- Reports/export workflows.
- Workspace authorization and production-grade security hardening.

Backend capabilities not reflected in frontend:

- Standalone catalog/reference views for `Marketplaces`, `Brands`, `Categories`, `Warehouses`.
- `Expenses`, `ExpenseCategories`.
- `Users`, `Workspaces`, `UserWorkspaces`.
- `Roles`, `PermissionCategories`, `Permissions`, `RolePermissions`, `RoleSubroles`.
- `Rules`, `RuleSets`, `RuleSetRules`.
- `Recommendations`, `RecommendationProducts`, `RecommendationCategories`.

Frontend placeholders:

- `Frontend/src/pages/OverviewPage.vue`
- `Frontend/src/pages/ExpensesPage.vue`
- `Frontend/src/pages/RecommendationsPage.vue`
- `Frontend/src/pages/AccessSettingsPage.vue`

Recommended next implementation stages:

1. Continue replacing remaining placeholders with read-only/list-first frontend vertical slices over existing APIs.
2. Add a real Overview dashboard using existing data first, then reviewed aggregation endpoints.
3. Improve dense table/filter primitives for marketplace intelligence workflows.
4. Add auth/workspace authorization hardening.
5. Plan parser ingestion and ML/recommendation integration as separate reviewed stages.
6. Add tests, observability, performance, and deployment hardening.

## Backlog Structure

Each item below uses:

- `Priority`: critical, high, medium, low.
- `Approximate Scope`: small, medium, large.
- `Affected Layers`: frontend, backend, infra, ML, parser, UX, docs.

## Epic 1: Source of Truth, Architecture Guardrails, Product Backlog Governance

### Task 1.1 - Maintain backlog as the planning index

Priority: high  
Approximate Scope: small  
Affected Layers: docs

Problem:

The project has multiple documentation sources and a broad target product scope. Without a backlog index, future work can drift into ad hoc implementation.

Current State:

Architecture docs exist and are strong. No dedicated backlog file existed before this document.

Target State:

`docs/BACKLOG.md` remains the product backlog index and references source-of-truth docs, requirements, competitor references, accepted UI patterns, and hard constraints.

Dependencies:

- Existing docs under `docs/`.
- Project requirement documents under `Documents/`.

Suggested Implementation Order:

1. Keep this backlog aligned after each logical product stage.
2. Update `docs/AI_CONTEXT.md` only when implemented scope changes.
3. Update ADRs only for real architecture decisions.

Notes / References:

- `docs/DECISIONS.md`
- `docs/AI_CONTEXT.md`
- `Documents/Раздел_Первый_Лукашенко_ИСб_22_2_о.docx`

### Task 1.2 - Lock architecture constraints into task templates

Priority: high  
Approximate Scope: small  
Affected Layers: docs, backend, frontend

Problem:

Future tasks can accidentally violate migration, API, enum, theme, parser, or ML boundaries.

Current State:

Constraints are documented across several files, but they are not repeated in backlog task templates.

Target State:

Every future implementation task starts by checking migration policy, API stability, theme token policy, no-fake-analytics rule, and parser/ML isolation.

Dependencies:

- `docs/API_IMPLEMENTATION_PATTERN.md`
- `docs/ENTITY_CONVENTIONS.md`
- `docs/DECISIONS.md`

Suggested Implementation Order:

1. Use this backlog section as the task intake checklist.
2. Add new constraints only after they become decisions.

Notes / References:

- Do not add migrations or route/API changes as incidental work.
- Competitor references are workflow references only.

## Epic 2: Frontend Placeholder Replacement and Vertical Slice Coverage

### Task 2.1 - Replace placeholder frontend sections with real list/detail pages

Priority: critical  
Approximate Scope: large  
Affected Layers: frontend, backend, UX

Problem:

Most seller-facing sections are placeholders even though backend CRUD APIs exist.

Current State:

Products, Orders, Reviews, Campaigns, and Logistics have real read-only frontend integration. Overview, Expenses, Recommendations, and Access settings render lightweight placeholder states.

Target State:

Each placeholder route has a real list-first page with backend integration, pagination, sorting where supported, loading/error/empty states, URL query sync where useful, and read-only detail inspection. Mutations should be added only when explicitly scoped.

Dependencies:

- Existing CRUD APIs.
- Shared frontend HTTP client and auth flow.
- Shared `DataTable`, `KpiGrid`, `PageHeader`, `EmptyState`, `LoadingState`, `Button`, `Input`, `Badge`.

Suggested Implementation Order:

1. Orders list/detail. Completed.
2. Reviews and review replies list/detail. Completed as read-only inspection.
3. Campaigns and campaign metrics list/detail. Completed as read-only inspection.
4. Logistics list/detail. Completed as read-only inspection with drawer-only warehouse detail.
5. Expenses and expense categories list/detail.
6. Recommendations list/detail.
7. Access settings list views for roles, permissions, users, workspaces.

Notes / References:

- Use Products as the implementation benchmark.
- Do not mirror every backend entity as top-level navigation.
- Preserve feature-oriented frontend structure.

### Task 2.2 - Add catalog/reference frontend coverage

Priority: high  
Approximate Scope: medium  
Affected Layers: frontend, backend, UX

Problem:

Catalog APIs for marketplaces, brands, categories, and warehouses exist, but the frontend uses raw IDs and has no operator-friendly catalog management or selection.

Current State:

Products filters expose raw UUID inputs for marketplace, brand, and category.

Target State:

Add catalog selectors and lightweight catalog views so users can filter by names and inspect marketplaces, brands, categories, and warehouses without copying UUIDs.

Dependencies:

- Catalog CRUD APIs.
- Existing Products filters.

Suggested Implementation Order:

1. Add API clients/types for catalog resources.
2. Replace raw Product filter UUID inputs with searchable selectors.
3. Add catalog reference pages if navigation needs them.

Notes / References:

- Marketplace selector reference: `Снимок экрана 2026-05-14 002042.png`.
- Categories/brands references: `Снимок экрана 2026-05-14 002306.png`, `Снимок экрана 2026-05-14 002322.png`.

## Epic 3: Overview Dashboard and Analytics Workflows

### Task 3.1 - Build Overview dashboard on real data

Priority: critical  
Approximate Scope: large  
Affected Layers: frontend, backend, UX

Problem:

The Overview route is currently a placeholder, so the product lacks an operator landing screen.

Current State:

Overview shows KPI placeholders with `Pending analytics API` captions.

Target State:

Overview shows a workspace snapshot using real backend data: revenue proxy, order count, review volume, ad spend, stock risk, expenses, product status distribution, and recommendation/signal summary. Initial version may aggregate client-side from existing paged APIs only if data volume is small and clearly marked as MVP.

Dependencies:

- Existing Orders, Reviews, CampaignMetrics, Logistics, Expenses, Products, Recommendations APIs.
- Future aggregation APIs for scalable dashboards.

Suggested Implementation Order:

1. Define dashboard KPI set and date range behavior.
2. Build frontend dashboard from existing APIs for dev/demo scenarios.
3. Add backend aggregation endpoints only after frontend KPI contracts are stable.
4. Replace MVP captions with exact data semantics.

Notes / References:

- Dashboard reference: `Снимок экрана 2026-05-14 001957.png`.
- Do not show fake growth/demand numbers.

### Task 3.2 - Add analytics aggregation endpoints

Priority: high  
Approximate Scope: large  
Affected Layers: backend, frontend

Problem:

CRUD list endpoints are not enough for dashboard charts, timelines, distributions, or large data volumes.

Current State:

Backend exposes CRUD and list filters. No dedicated analytics endpoints exist.

Target State:

Add reviewed, resource-specific analytics endpoints for dashboard and vertical slices, such as product status summary, order revenue timeline, review rating timeline, campaign spend timeline, logistics stock summary, expense summary, and recommendation score summary.

Dependencies:

- Stable dashboard and vertical-slice UI needs.
- API guidelines for DTOs, ProblemDetails, cancellation, allowlisted filters.

Suggested Implementation Order:

1. Specify endpoint contracts from frontend use cases.
2. Add one vertical analytics slice at a time.
3. Smoke-test with PostgreSQL and development seed data.

Notes / References:

- Do not introduce generic analytics framework prematurely.
- Keep endpoint surface under `/api/v1`.

### Task 3.3 - Add charting strategy

Priority: medium  
Approximate Scope: medium  
Affected Layers: frontend, UX

Problem:

Dashboards need trend charts, distributions, and comparison visuals, but no charting approach is established.

Current State:

No chart library or custom chart system is implemented.

Target State:

Choose a minimal chart strategy after the first dashboard requirements are stable. Charts must fit the obsidian theme, be compact, readable, and accessible.

Dependencies:

- Dashboard KPI and aggregation endpoint decisions.

Suggested Implementation Order:

1. Build initial simple visualizations with CSS/SVG only if sufficient.
2. If a chart library is needed, make it a dedicated reviewed dependency task.
3. Define chart tokens through semantic CSS variables.

Notes / References:

- References: Moneyplace dashboard, TradingView density, CoinMarketCap/CoinGecko market tables.
- Do not add packages incidentally during unrelated UI work.

## Epic 4: Dense Tables, Filtering UX, and Operator Workstation Polish

### Task 4.1 - Improve shared table system

Priority: high  
Approximate Scope: medium  
Affected Layers: frontend, UX

Problem:

Marketplace intelligence workflows need dense, configurable, scan-oriented tables across products, categories, sellers, brands, reviews, campaigns, and finance.

Current State:

`DataTable` supports sortable headers, sticky header, row click, selected row, horizontal overflow, and scoped cell slots. Column visibility, density modes, saved settings, and advanced numeric presentation are absent.

Target State:

Shared table supports column visibility, compact/comfortable density, numeric alignment, sticky headers, row actions, selected rows, persistent table preferences, stronger empty/loading/error states, and keyboard-friendly interaction.

Dependencies:

- Existing `DataTable`.
- Products table as reference implementation.

Suggested Implementation Order:

1. Add column visibility model.
2. Add density modes and stable row heights.
3. Add reusable numeric/date/currency cell helpers.
4. Add table settings persistence.

Notes / References:

- Table references: `Снимок экрана 2026-05-14 001940.png`, `002025.png`, `002042.png`.
- Do not copy visual styling literally; adapt density to obsidian workstation.

### Task 4.2 - Improve filtering UX

Priority: high  
Approximate Scope: medium  
Affected Layers: frontend, UX

Problem:

Raw ID fields and simple status selects do not support serious analytics workflows.

Current State:

Products has search, status, raw marketplace/brand/category UUID filters, chips, reset, and URL sync.

Target State:

Filtering supports searchable selectors, date ranges, numeric ranges, marketplace tabs/selectors, saved presets, active chips, quick reset, and URL sync. Filter surfaces remain compact and readable.

Dependencies:

- Catalog selector APIs.
- Shared filter components.

Suggested Implementation Order:

1. Build reusable date range and numeric range controls.
2. Add catalog selectors.
3. Add filter presets after multiple pages share patterns.

Notes / References:

- Filter modal references: sellers/brands screenshots `002125.png`, `002322.png`.
- Marketplace selector reference: `002042.png`.

### Task 4.3 - Polish visual hierarchy and signal language

Priority: high  
Approximate Scope: medium  
Affected Layers: frontend, UX

Problem:

The visual system exists but only Products has been polished deeply.

Current State:

Semantic tokens and obsidian styling exist. Other routes are placeholders.

Target State:

All analytical pages use consistent hierarchy: compact headers, restrained KPI bands, dense tables, signal badges, drawer/detail surfaces, and clear hover/focus states. Ember/fire accents indicate signal strength only.

Dependencies:

- `Frontend/src/styles/tokens.css`.
- Shared UI primitives.

Suggested Implementation Order:

1. Extend Products patterns to Orders/Reviews/Campaigns.
2. Audit hardcoded colors and replace with tokens.
3. Add signal vocabulary only when backed by data semantics.

Notes / References:

- Do not add flame images, animated fire, fake urgency, or decorative glow.

## Epic 5: Products, Categories, Brands, Sellers, and Market Intelligence

### Task 5.1 - Expand Products workflow

Priority: high  
Approximate Scope: large  
Affected Layers: frontend, backend, UX

Problem:

Products is currently read/list/detail oriented, but target requirements include product-card management and analysis.

Current State:

Products list/detail is integrated with backend. Product create/update/delete APIs exist, but frontend mutations and media mutation endpoints are absent. Product history is read-only via API but not surfaced as a real analytics workflow.

Target State:

Products supports scoped create/edit/delete workflows, product history visualization, media management when backend endpoints exist, SEO/card quality inspection, and comparison entrypoints.

Dependencies:

- Existing Products CRUD API.
- Product history API.
- Future standalone media mutation endpoints if needed.

Suggested Implementation Order:

1. Add product history panel in detail drawer.
2. Add product create/edit forms only after UX fields are reviewed.
3. Add media mutation APIs as a separate backend task if needed.
4. Add compare action to connect with market intelligence.

Notes / References:

- Product table/reference workflow: `001940.png`, `002042.png`, `002059.png`.
- Do not invent demand/growth/recommendation metrics.

### Task 5.2 - Add category, brand, and seller intelligence views

Priority: high  
Approximate Scope: large  
Affected Layers: frontend, backend, parser, UX

Problem:

Requirements call for market, competitor, category, brand, and seller analytics. Backend currently has catalog entities but no seller/niche analytical model or UI.

Current State:

Categories and brands exist as backend resources. Sellers, niches, competitor positions, and market dynamics are not modeled as first-class analytical workflows.

Target State:

Add views for categories, brands, sellers, and niches with dense tables, comparison, period selection, marketplace selection, distribution charts, and drilldowns. Data should initially use existing catalog/product records where truthful, then expand through parser-backed ingestion.

Dependencies:

- Catalog/frontend coverage.
- Parser ingestion design.
- Future schema/API review for seller/niche/market facts if current model is insufficient.

Suggested Implementation Order:

1. Build category/brand list views over existing APIs.
2. Define seller/niche analytical data model separately.
3. Add parser-backed market facts through reviewed backend/schema work.
4. Add comparison workflows.

Notes / References:

- References: `002125.png`, `002139.png`, `002152.png`, `002306.png`, `002322.png`.
- CoinMarketCap/CoinGecko references apply to market ranking density, not marketplace domain semantics.

### Task 5.3 - Add product and competitor comparison workflow

Priority: high  
Approximate Scope: large  
Affected Layers: frontend, backend, parser, UX

Problem:

Requirements include group comparison of articles by price, sales, categories, and positions.

Current State:

No comparison page or comparison API exists.

Target State:

Users can select multiple products/articles and compare price, sales, stock, rating, reviews, revenue, keywords, and category positions across periods. Missing metrics must be hidden or marked unavailable rather than faked.

Dependencies:

- Product table row selection/bulk actions.
- Parser/market facts ingestion.
- Analytics aggregation endpoints.

Suggested Implementation Order:

1. Add frontend selection and comparison shell.
2. Support comparison over fields already available.
3. Add parser-backed metrics in later stages.

Notes / References:

- Reference: `Снимок экрана 2026-05-14 002059.png`.
- TradingView reference applies to comparison density and time-range thinking.

## Epic 6: Reviews and Autoreply Workflows

### Task 6.1 - Implement reviews operator workflow

Priority: high  
Approximate Scope: large  
Affected Layers: frontend, backend, UX

Problem:

Reviews are important operational data. The first read-only frontend slice exists, but the broader operator workflow is not complete.

Current State:

Backend CRUD exists for `Reviews` and `ReviewReplies`. A read-only frontend list/detail slice exists with filters, URL sync, dense table, detail drawer, and lazy linked replies. Reply mutations, autoreply scripts, product hydration, parser ingestion, and status dashboards are absent.

Target State:

Reviews page evolves from read-only inspection into a fuller operator workflow: review queues, richer reply status workflows, product context, reviewed reply actions, and operational controls where backend semantics support them.

Dependencies:

- Reviews and ReviewReplies APIs.
- Products API for product context.

Suggested Implementation Order:

1. Build read-only review queue with filters and product context. Completed.
2. Add review reply detail. Partially completed as linked read-only replies in the review drawer.
3. Add reply creation/editing only after backend workflow semantics are reviewed.

Notes / References:

- References: `002218.png`, `002248.png`.

### Task 6.2 - Plan autoreply scripts and templates

Priority: medium  
Approximate Scope: large  
Affected Layers: frontend, backend, ML, UX

Problem:

Target requirements include auto-responder scripts and training on reply examples, but no domain/API model exists for scripts.

Current State:

`ReviewReply` stores replies. Script templates, conditions, trigger words, safety review, and AI suggestions are absent.

Target State:

Define an autoreply workflow with scripts, template blocks, rating filters, trigger/minus words, delayed sending, human approval, and immutable sent replies. AI suggestions can be added only after ML integration is reviewed.

Dependencies:

- Review workflow.
- Future schema/API design.
- Future ML integration.

Suggested Implementation Order:

1. Document script workflow and data model.
2. Add backend model/API as a reviewed schema task.
3. Add frontend script editor.
4. Add AI-assisted suggestions later.

Notes / References:

- References: `002218.png`, `002233.png`, `002248.png`.
- Avoid enabling automated marketplace actions without safety constraints.

## Epic 7: Advertising and Pricing Operations

### Task 7.1 - Implement campaign operations workflow

Priority: high  
Approximate Scope: large  
Affected Layers: frontend, backend, UX

Problem:

Campaign APIs exist, but users cannot operate or analyze campaigns in the frontend.

Current State:

`Campaigns` and `CampaignMetrics` CRUD APIs exist. Campaigns page has a read-only frontend list/detail slice with filters, URL sync, dense table, detail drawer, and lazy linked campaign metrics. Mutations, automation, bid workflows, and real campaign analytics are absent.

Target State:

Campaigns page supports list/detail, budget/status/type fields, campaign metrics timeline, spend/click/impression summaries, product context, and campaign journal placeholders for future automation.

Dependencies:

- Campaigns and CampaignMetrics APIs.
- Products API.
- Future aggregation endpoints for campaign charts.

Suggested Implementation Order:

1. Build campaign list/detail. Completed as read-only inspection.
2. Add metrics timeline and spend summaries after reviewed analytics semantics exist.
3. Add campaign settings/read-only rule set display.
4. Add mutations and automation only when scoped.

Notes / References:

- Reference: `Снимок экрана 2026-05-14 002337.png`.

### Task 7.2 - Plan bid, schedule, and stop-rule automation

Priority: medium  
Approximate Scope: large  
Affected Layers: backend, frontend, UX

Problem:

Requirements include ad scheduling, auto-stop, budget control, and competitive bidding.

Current State:

`Campaign.TimeToImpression` JSONB and rule set relationships exist, but no reviewed automation semantics or operator UI exists.

Target State:

Define campaign automation commands and rule semantics for schedules, stop criteria, budget thresholds, and bid recommendations. Expose them only after backend behavior is reviewed.

Dependencies:

- Campaign workflow.
- Rules/RuleSets API.
- Future analytics metrics.

Suggested Implementation Order:

1. Surface existing rule set links read-only.
2. Define automation rule schema.
3. Add reviewed API commands.
4. Add operator UI and audit trail.

Notes / References:

- Do not automate live marketplace behavior without explicit safety and audit design.

### Task 7.3 - Plan pricing and sales automation

Priority: medium  
Approximate Scope: large  
Affected Layers: backend, frontend, parser, UX

Problem:

Requirements include maintaining price relative to competitors, price ranges, multiple marketplaces, and marketplace promotions.

Current State:

Product price history exists, but current product root does not provide a complete pricing automation workflow.

Target State:

Define pricing rules, competitor price inputs, price range controls, stock-aware adjustments, and action/promotion participation workflows.

Dependencies:

- Product history.
- Parser market price ingestion.
- Rules/RuleSets.
- Authorization hardening.

Suggested Implementation Order:

1. Add read-only price history visualization.
2. Define price rule model and API.
3. Add operator approval workflow.
4. Add marketplace action integration later.

Notes / References:

- MarketGuru/Moneyplace references apply to workflow density, not exact implementation.

## Epic 8: Finance, Logistics, Stock, and Reporting

### Task 8.1 - Implement logistics and stock workflow

Priority: high  
Approximate Scope: large  
Affected Layers: frontend, backend, UX

Problem:

Requirements include stock control, critical stock warnings, warehouse/geography analysis, and logistics cost monitoring.

Current State:

`Logistics` and `Warehouses` CRUD APIs exist. Logistics page has a read-only frontend list/detail slice with filters, URL sync, dense table, detail drawer, and drawer-only linked warehouse detail. Mutations, stock risk semantics, forecasting, geography, and parser-backed logistics analysis are absent.

Target State:

Logistics page shows stock levels, in-transit amounts, warehouse context, storage/logistics costs, and date-based filtering from real backend fields. Future versions may add reviewed stock risk semantics, geography, and demand-based distribution.

Dependencies:

- Logistics and Warehouses APIs.
- Products API.
- Future aggregation endpoints.

Suggested Implementation Order:

1. Build logistics list/detail over existing API. Completed as read-only inspection.
2. Add stock risk summary only after reviewed semantics and backend data contract exist.
3. Add warehouse filters/selectors.
4. Add geography/demand analysis after parser-backed data exists.

Notes / References:

- Requirements: stock control and sales geography.

### Task 8.2 - Implement expenses and profitability workflow

Priority: high  
Approximate Scope: large  
Affected Layers: frontend, backend, UX

Problem:

Finance APIs exist, but there is no frontend workflow for expenses, profitability, ABC analysis, or reporting.

Current State:

`Expenses` and `ExpenseCategories` CRUD APIs exist. Expenses page is a placeholder.

Target State:

Expenses page supports operational expense list/detail, category filters, workspace/user context, summary KPIs, date filters, and later profitability/ABC analysis with order/ad/logistics data.

Dependencies:

- Expenses APIs.
- Orders, CampaignMetrics, Logistics APIs.
- Future aggregation endpoints.

Suggested Implementation Order:

1. Build expense list/detail.
2. Add expense category filters.
3. Add financial dashboard summaries.
4. Add profitability/ABC analysis after aggregation endpoints exist.

Notes / References:

- Requirement: financial accounting table and sales dynamics per article.

### Task 8.3 - Add report export workflows

Priority: medium  
Approximate Scope: medium  
Affected Layers: backend, frontend, infra, UX

Problem:

Requirements include report exports, but no export system exists.

Current State:

No export endpoints or frontend export controls exist.

Target State:

Users can export selected list/report data in reviewed standard formats such as CSV/XLSX. Exports should respect filters, workspace authorization, and large-data constraints.

Dependencies:

- Stable list/filter contracts.
- Authorization hardening.
- Performance review for large exports.

Suggested Implementation Order:

1. Add CSV export for one stable list.
2. Add XLSX only after format requirements are clear.
3. Add export audit/logging if needed.

Notes / References:

- Do not add spreadsheet packages incidentally.

## Epic 9: Recommendations, Heat Signals, Parser, and ML Integration

### Task 9.1 - Define real signal and heat semantics

Priority: high  
Approximate Scope: medium  
Affected Layers: backend, frontend, ML, parser, UX, docs

Problem:

Current Products heat indicators are visual scaffolding, not real demand, growth, risk, or recommendation signals.

Current State:

Heat tiers are derived only from product status and update recency.

Target State:

Define real signal types, score ranges, explanations, input data, freshness, confidence, and UI language. Signals must be backed by backend/API data before being shown as business metrics.

Dependencies:

- Recommendation output schemas.
- Parser/analytics data.
- Product and dashboard UI.

Suggested Implementation Order:

1. Document signal taxonomy.
2. Map each signal to a backend data source.
3. Add API fields/endpoints.
4. Update UI labels and badges.

Notes / References:

- Do not describe current heat as real analytics.
- Fire/ember remains signal language only.

### Task 9.2 - Implement recommendation frontend over existing storage

Priority: high  
Approximate Scope: medium  
Affected Layers: frontend, backend, UX

Problem:

Recommendation storage APIs exist, but users cannot inspect recommendation records.

Current State:

Recommendations page is a placeholder. Backend CRUD exists for recommendations and product/category links.

Target State:

Recommendations page shows score, type, target links, explanation JSON, snapshot JSON, created/updated dates, and target products/categories where available. It must clearly state whether records are imported/stored results, not live ML generation.

Dependencies:

- Recommendations APIs.
- Products and Categories APIs.

Suggested Implementation Order:

1. Build read-only recommendations table.
2. Add detail drawer with explanation/snapshot JSON viewer.
3. Link targets to Products/Categories pages.

Notes / References:

- No ML inference in this task.

### Task 9.3 - Parser integration planning

Priority: medium  
Approximate Scope: large  
Affected Layers: parser, backend, infra, docs

Problem:

Parser scripts collect Wildberries data, but outputs are not integrated into backend storage or frontend analytics.

Current State:

`Parser/` contains WB parser scripts and result files. Backend database has normalized marketplace/product/analytics tables. No ingestion contract exists.

Target State:

Define ingestion contracts from parser outputs into backend: source identifiers, deduplication, timestamps, marketplace mapping, product/category mapping, error handling, retry strategy, and audit metadata.

Dependencies:

- Existing parser outputs.
- Database model review.
- Future schema/API ingestion task.

Suggested Implementation Order:

1. Document parser output fields and target backend entities.
2. Define ingestion DTO/contracts.
3. Add ingestion service/API as a separate reviewed backend task.
4. Add scheduled/background execution only after infrastructure decision.

Notes / References:

- `Documents/Практика_Неделя_1_Парсер.docx`
- Do not modify parser in backlog/documentation-only tasks.

### Task 9.4 - ML/recommendation integration planning

Priority: medium  
Approximate Scope: large  
Affected Layers: ML, backend, frontend, docs

Problem:

Requirements include AI recommendations, content generation, market entry advice, profitability advice, and stock advice, but Intelligence has no integrated runtime.

Current State:

`Intelligence/` exists but no ML runtime integration is implemented. Recommendations storage exists without generation.

Target State:

Define Python/ML service responsibilities, request/response contracts, model output schema, explanation JSON, snapshot JSON, training/update cadence, confidence scoring, and API boundaries.

Dependencies:

- Recommendation schema review.
- Parser ingestion.
- Backend API boundary design.

Suggested Implementation Order:

1. Define ML service contract and output schemas.
2. Store generated recommendations through existing/future recommendation APIs.
3. Add asynchronous generation workflow.
4. Add frontend explanation UI and feedback loop.

Notes / References:

- Requirements from `Раздел_Первый...`: recommended products, risk analysis, profitability advice, stock advice, content generation.

## Epic 10: Authorization, Security, Workspaces, and Roles

### Task 10.1 - Future authorization hardening

Priority: critical  
Approximate Scope: large  
Affected Layers: backend, frontend, infra

Problem:

Auth exists, but CRUD APIs remain anonymous and workspace permissions are not enforced.

Current State:

JWT access tokens and refresh sessions exist. `/auth/me` returns user, memberships, and direct permissions. CRUD APIs are anonymous for compatibility.

Target State:

Protect CRUD and analytics APIs, enforce workspace membership, global role, workspace role, resource ownership/workspace, and permissions. Frontend should reflect current user capabilities.

Dependencies:

- Existing roles/permissions/workspaces model.
- Auth foundation.
- Frontend access settings page.

Suggested Implementation Order:

1. Define permission policy matrix.
2. Add backend authorization policies incrementally.
3. Update frontend route/action guards.
4. Add authorization tests and smoke tests.

Notes / References:

- Do not break existing smoke workflows without a migration/testing plan.

### Task 10.2 - Implement Access settings UI

Priority: high  
Approximate Scope: large  
Affected Layers: frontend, backend, UX

Problem:

Access data exists but operators cannot manage users, workspaces, roles, and permissions in the frontend.

Current State:

Access settings page is a placeholder. Backend CRUD exists.

Target State:

Access settings supports users, workspaces, workspace memberships, roles, permissions, and role-permission assignments through safe list/detail workflows. Mutations should be gated by future authorization rules.

Dependencies:

- Users/Workspaces/Access APIs.
- Authorization hardening plan.

Suggested Implementation Order:

1. Build read-only access overview.
2. Add membership inspection.
3. Add mutation workflows after authorization rules exist.

Notes / References:

- Requirements: multi-user access, role distribution, workspaces for teams.

### Task 10.3 - Token and session security improvements

Priority: medium  
Approximate Scope: medium  
Affected Layers: backend, frontend, infra

Problem:

Auth foundation is MVP-level and localStorage access token storage is not the final security posture.

Current State:

Access tokens and refresh tokens are implemented. Frontend persists tokens for MVP behavior.

Target State:

Review token storage, refresh rotation UX, session revocation visibility, rate limiting, lockout, password reset, and audit logging. Public registration, OAuth, and 2FA remain separate tasks.

Dependencies:

- Current auth service.
- Frontend auth store.
- Production deployment strategy.

Suggested Implementation Order:

1. Add session visibility and logout-all planning.
2. Harden frontend token storage if required.
3. Add rate limiting/lockout in a dedicated security task.

Notes / References:

- Do not store raw refresh tokens or plaintext passwords.

## Epic 11: Performance, Scalability, Testing, Observability, and Deployment

### Task 11.1 - Testing foundation

Priority: high  
Approximate Scope: large  
Affected Layers: backend, frontend, infra

Problem:

The project has smoke verification history, but systematic automated tests are limited or absent.

Current State:

Backend builds cleanly. Frontend typecheck/build and browser smoke were previously verified. No broad test suite is documented as complete.

Target State:

Add focused automated tests for domain invariants, EF metadata, Application services, API endpoints, auth refresh/session behavior, seed idempotency, frontend query sync, table interactions, and protected routes.

Dependencies:

- Existing backend/frontend build setup.
- Stable APIs and frontend slices.

Suggested Implementation Order:

1. Add backend unit tests for domain/services.
2. Add backend integration tests for API/auth.
3. Add frontend component/unit tests for query/filter/table logic.
4. Add Playwright smoke tests for core workflows.

Notes / References:

- Keep tests focused and aligned with risk.

### Task 11.2 - Observability foundation

Priority: high  
Approximate Scope: medium  
Affected Layers: backend, frontend, infra

Problem:

Production diagnosis needs structured logs, request correlation, metrics, and frontend error visibility.

Current State:

API has ProblemDetails and standard ASP.NET logging baseline. No documented correlation/metrics/error telemetry foundation exists.

Target State:

Add request correlation IDs, structured logging rules, sensitive-data redaction, frontend error capture, parser/ML integration logging strategy, and operational health endpoints.

Dependencies:

- API middleware design.
- Deployment/infra plan.

Suggested Implementation Order:

1. Add request correlation middleware.
2. Add structured log conventions.
3. Add health/readiness endpoints.
4. Add frontend error boundary/logging strategy.

Notes / References:

- Never log passwords, raw tokens, or marketplace credentials.

### Task 11.3 - Performance and scalability

Priority: medium  
Approximate Scope: large  
Affected Layers: backend, frontend, infra

Problem:

Marketplace analytics can create large tables, expensive filters, and heavy dashboard queries.

Current State:

Pagination exists with `PagedResponse<T>`. No dedicated query optimization or large-table UX strategy exists.

Target State:

Add pagination hardening, server-side aggregation, query profiling, reviewed indexes, caching strategy, async ingestion strategy, large-table frontend virtualization if needed, and export limits.

Dependencies:

- Real analytics endpoints.
- Representative data volumes.
- Reviewed migration tasks for indexes.

Suggested Implementation Order:

1. Measure slow queries with seeded/representative data.
2. Add endpoint-specific query optimization.
3. Add indexes only through reviewed migrations.
4. Add cache/virtualization after proven need.

Notes / References:

- Do not add unique indexes or migrations based on assumptions.

### Task 11.4 - Deployment and local/prod infrastructure hardening

Priority: medium  
Approximate Scope: large  
Affected Layers: infra, backend, frontend

Problem:

Local dev scripts exist, but production deployment is not defined.

Current State:

Local Docker Compose includes PostgreSQL, pgAdmin, Redis, and MinIO. Scripts start local backend/frontend and do not auto-migrate or enable seed.

Target State:

Define production deployment topology, secrets handling, migration application process, static frontend hosting strategy, API environment configuration, backups, health checks, and rollback process.

Dependencies:

- Deployment target decision.
- Security hardening.
- Observability foundation.

Suggested Implementation Order:

1. Document production environment assumptions.
2. Define manual migration workflow.
3. Add production Docker/deployment config only as a dedicated infra task.
4. Add backup/restore checks.

Notes / References:

- Local dev scripts must not auto-apply migrations, enable seed automatically, delete volumes, or kill untracked port processes.
