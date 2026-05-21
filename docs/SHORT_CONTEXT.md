# 1. Project Summary

- AshmesMarketplaces is a marketplace analytics and sales-management platform for sellers.
- Target product: marketplace intelligence workstation for product cards, orders, reviews, logistics, ads, finance, teams, parser data, and recommendations.
- Current stage: backend/API/auth/seed/frontend foundation is complete; main read-only frontend vertical slices are implemented; parser output contract and safe manual WB runner foundation exist; backend ingestion and broad analytics workflows are still pending.
- Positioning: premium market intelligence/operator workstation, not generic AI SaaS.
- Backend maturity: broad CRUD/auth foundation over PostgreSQL.
- Frontend maturity: auth shell and Products, Orders, Reviews, Campaigns, Logistics, Expenses, Recommendations, and Access Settings are real read-only slices; Overview dashboard remains the main placeholder.

# 2. Stack

- Backend: `.NET`, `EF Core`, `PostgreSQL`, `JWT auth`, `Swagger`.
- Frontend: `Vue 3`, `TypeScript`, `Composition API`, `Pinia`, `Vue Router`, `Tailwind`, `Vite`.
- Infra: local Docker PostgreSQL + pgAdmin.

# 3. Architecture Rules

- No CQRS, repositories, generic CRUD controllers/services for basic slices.
- Application services use `ApplicationDbContext` directly.
- API returns DTOs only; never expose Domain entities.
- Mapping is manual.
- Request validation uses FluentValidation.
- Services return `ServiceResult`; API maps errors to ProblemDetails.
- Build one reviewed vertical slice per resource/workflow.
- Keep simple CRUD pragmatic; add use-case services only when behavior justifies it.
- No migrations without explicit request and review.
- No fake analytics, fake signals, fake demand/growth numbers, or invented enum values.
- Existing API contracts/routes stay stable unless the task explicitly scopes a reviewed API change.

# 4. Database / EF Rules

- PostgreSQL is the target database.
- EF Core Fluent API is the persistence source of truth.
- Use explicit table names and explicit `snake_case` column names.
- One `IEntityTypeConfiguration<T>` per entity.
- Join tables are explicit entities with composite keys; no implicit many-to-many.
- Dynamic JSON uses `JsonDocument?` mapped to `jsonb`.
- Persisted `DateTime` / `DateTime?` values must be UTC.
- `ProductId` is a value object converted to/from `Guid`.
- Guid and `ProductId` keys are app-generated with `.ValueGeneratedNever()`.
- `Session.Id` is the integer identity exception.
- Analytical/history data uses restrictive delete behavior.
- Product media cascades from `Product`.

# 5. Current Backend Status

- Backend solution: `Backend/AshmesMarketplaces.sln`.
- CRUD coverage exists for Catalog, Products, Operations, Advertising, Finance, Users/Workspaces, Access, Rules, and Recommendations.
- Auth/Security Foundation exists: `login`, `refresh`, `logout`, `me`.
- Auth uses JWT access tokens + opaque refresh tokens stored as hashes in `Sessions.token`.
- Development seed exists, Development-only, config-gated, disabled by default.
- CRUD APIs remain anonymous for compatibility; auth endpoints are protected where appropriate.
- Missing backend capabilities: analytics aggregation endpoints, workspace authorization/permission enforcement, parser ingestion, ML runtime, recommendation generation, background jobs, exports, observability and production hardening.

# 6. Current Frontend Status

- Frontend Foundation v2 exists under `Frontend/`.
- Auth flow, token refresh, protected routes, app shell, sidebar/topbar, shared primitives, and obsidian theme tokens exist.
- Real read-only slices exist for Products, Orders, Reviews, Campaigns, Logistics, Expenses, Recommendations, and Access Settings.
- Implemented slice patterns: dense tables, compact filters, pagination, URL query sync where useful, loading/error/empty states, row selection, keyboard row opening, and detail drawers with truthful linked context where backend data exists.
- Products detail drawer uses existing product detail endpoint; Products heat remains presentation-only from `status` and `dateUpdated`.
- Access Settings shows persisted users/workspaces/roles/memberships/role-permission records read-only; it does not enforce permissions or provide IAM mutation workflows.
- Main frontend placeholder remaining: `Frontend/src/pages/OverviewPage.vue`.

# 7. Parser Status

- `Parser/` has a safe manual Wildberries product-card runner for explicit subcategory allowlists only.
- Canonical parser output is `products.jsonl`; CSV and optional XLSX are derived exports; every run writes `manifest.json` plus structured run logs/errors.
- Runner config exposes region/dest, allowlist, concurrency, delays, retries, catalog/item caps, output path, token handling, and network smoke checks without backend/frontend coupling.
- Real WB debug validation is currently scoped to selected footwear subcategories; full-category traversal, scheduler/background execution, and DB writes remain out of scope.
- Next parser milestone: validate representative canonical output, review raw/staging and domain mapping, then build backend ingestion/upsert in a dedicated schema/API task.

# 8. Visual Direction

- Dark obsidian workstation.
- Dense analytical UI.
- Transparent layered surfaces.
- Restrained ember/fire accents as signal semantics only, not decoration.
- Primary feel: premium fintech / market intelligence / operator console.
- References: TradingView, Coinglass, CoinMarketCap, CoinGecko, MPStats, Moneyplace, MarketGuru, Mayak, TrueStats.
- Use references for workflow density, hierarchy, tables, filtering, dashboards, and signal systems.
- Avoid generic AI SaaS look, giant rounded cards, excessive gradients, gaming/casino aesthetics, decorative flames, and fake analytics.

# 9. Dev Workflow

- PowerShell start: `scripts/dev/start-dev.ps1`.
- PowerShell stop: `scripts/dev/stop-dev.ps1`.
- Bash start: `scripts/dev/start-dev.sh`.
- Bash stop: `scripts/dev/stop-dev.sh`.
- Default backend URL: `http://localhost:5019`.
- Default frontend URL: `http://localhost:5173`.
- Default Swagger URL: `http://localhost:5019/swagger`.
- Runtime logs/PIDs live under `.dev`.
- Scripts do not auto-apply migrations, auto-enable seed, delete Docker volumes by default, or kill untracked port processes.

# 10. Seed Accounts

- `admin@ashmes.local` / `Admin123!`
- `manager@ashmes.local` / `Manager123!`
- `analyst@ashmes.local` / `Analyst123!`
- `viewer@ashmes.local` / `Viewer123!`

# 11. Current Priorities

1. Validate safe parser output on representative real WB product-card runs and keep execution rate-limit aware.
2. Design reviewed parser ingestion/raw-staging/domain mapping before adding backend schema or DB writes.
3. Build Overview only after real-data pipeline contracts are stable enough to avoid fake dashboard semantics.
4. Improve catalog/reference selectors so product and workflow filters stop relying on raw UUIDs where backend names exist.
5. Define real recommendation/signal semantics before showing heat as business intelligence.
6. Harden authorization/workspace permission enforcement after read-only access surface is stable.
7. Add focused tests, observability, performance, export, and deployment hardening.

# 12. Source Of Truth

- `docs/BACKLOG.md` = operational roadmap.
- Full docs are required for architecture-level changes.
- Read `docs/AI_CONTEXT.md` before non-trivial work.
- Read `docs/ARCHITECTURE.md` for subsystem boundaries.
- Read `docs/DATABASE.md` and `docs/ENTITY_CONVENTIONS.md` before DB/entity work.
- Read `docs/DECISIONS.md` before changing architecture assumptions.
- Read `docs/API_GUIDELINES.md` and `docs/API_IMPLEMENTATION_PATTERN.md` before API work.
- `Documents/` contains local requirements and visual references; it is not source code.

# 13. How To Work With Codex

- Plan first for non-trivial work.
- Implement one stage at a time.
- Keep changes scoped to the requested subsystem.
- Keep backend builds clean: `0 warnings`, `0 errors`.
- Run frontend typecheck/build after frontend changes.
- Do not rewrite architecture casually.
- Do not introduce unnecessary abstractions.
- Do not create migrations unless explicitly requested.
- Do not touch parser/ML/docker unless explicitly scoped.
- Do not change routes/API contracts unless explicitly scoped.
- Do not add packages unless explicitly scoped.
- Do not commit unless explicitly requested.
- When in doubt, preserve existing conventions and ask only for product decisions that cannot be discovered from the repo.
