# 1. Project Summary

- AshmesMarketplaces is a marketplace analytics and sales-management platform for sellers.
- Target product: marketplace intelligence workstation for product cards, orders, reviews, logistics, ads, finance, teams, parser data, and recommendations.
- Current stage: backend/API/auth/seed/frontend foundation complete; Products, Orders, Reviews, Campaigns, and Logistics frontend slices implemented; broad analytics workflow coverage pending.
- Positioning: premium market intelligence/operator workstation, not generic AI SaaS.
- Backend maturity: broad CRUD/auth foundation over PostgreSQL.
- Frontend maturity: auth shell and Products/Orders/Reviews/Campaigns/Logistics are real; remaining analytics, finance, recommendations, and access routes are placeholders.

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
- CRUD coverage complete for Catalog, Products, Operations, Advertising, Finance, Users/Workspaces, Access, Rules, Recommendations.
- Catalog APIs: `Marketplaces`, `Brands`, `Categories`, `Warehouses`.
- Product APIs: `Products` CRUD, product detail, read-only product history.
- Operations APIs: `Orders`, `Reviews`, `ReviewReplies`, `Logistics`.
- Ads/finance/workspace APIs: `Campaigns`, `CampaignMetrics`, `Expenses`, `ExpenseCategories`, `Users`, `Workspaces`, `UserWorkspaces`.
- Access/rules/recommendations APIs are implemented as CRUD/storage slices.
- Auth/Security Foundation implemented: `login`, `refresh`, `logout`, `me`.
- Auth uses JWT access tokens + opaque refresh tokens stored as hashes in `Sessions.token`.
- Development seed implemented, Development-only, config-gated, disabled by default.
- CRUD APIs remain anonymous for compatibility; auth endpoints are protected where appropriate.
- Workspace authorization and permission enforcement are not implemented.
- Parser integration, ML runtime, recommendation generation, background jobs are not implemented.

# 6. Current Frontend Status

- Frontend Foundation v2 implemented under `Frontend/`.
- Auth flow, token refresh, protected routes, app shell, sidebar/topbar, shared primitives are implemented.
- Products slice has real backend integration: filters, sorting, pagination, URL sync, KPIs, dense table, loading/error/empty states.
- Products detail drawer is read-only and uses existing `GET /api/v1/products/{id}`.
- Orders slice has read-only backend integration: filters, sorting, pagination, URL sync, dense table, loading/error/empty states, and detail drawer.
- Reviews slice has read-only backend integration: filters, sorting, pagination, URL sync, dense table, loading/error/empty states, detail drawer, and lazy linked review replies.
- Campaigns slice has read-only backend integration: filters, sorting, pagination, URL sync, dense table, loading/error/empty states, detail drawer, and lazy linked campaign metrics.
- Logistics slice has read-only backend integration: filters, sorting, pagination, URL sync, dense table, loading/error/empty states, detail drawer, and drawer-only linked warehouse detail.
- Visual Redesign Stage 1 implemented.
- Products Polish Stage implemented.
- `Frontend/src/styles/tokens.css` is the theme source of truth.
- Current Products heat is presentation-only, derived from `status` and `dateUpdated`.
- Placeholders remain: Overview, Expenses, Recommendations, Access.

# 7. Visual Direction

- Dark obsidian workstation.
- Dense analytical UI.
- Transparent layered surfaces.
- Restrained ember/fire accents.
- Fire/ember = signal system only, not decoration.
- Primary feel: premium fintech / market intelligence / operator console.
- References: TradingView, Coinglass, CoinMarketCap, CoinGecko, MPStats, Moneyplace, MarketGuru, Маяк, TrueStats.
- Use references for workflow density, hierarchy, tables, filtering, dashboards, and signal systems.
- Do not copy competitor screens pixel-for-pixel.
- Avoid generic AI SaaS look.
- Avoid giant rounded cards.
- Avoid excessive gradients.
- Avoid gaming/casino aesthetics.
- Avoid decorative flames.
- Avoid fake analytics.

# 8. Dev Workflow

- PowerShell start: `scripts/dev/start-dev.ps1`.
- PowerShell stop: `scripts/dev/stop-dev.ps1`.
- Bash start: `scripts/dev/start-dev.sh`.
- Bash stop: `scripts/dev/stop-dev.sh`.
- Default backend URL: `http://localhost:5019`.
- Default frontend URL: `http://localhost:5173`.
- Default Swagger URL: `http://localhost:5019/swagger`.
- Runtime logs/PIDs live under `.dev`.
- Scripts do not auto-apply migrations.
- Scripts do not auto-enable seed.
- Scripts do not delete Docker volumes by default.
- Stop scripts should only stop managed processes from PID files.

# 9. Seed Accounts

- `admin@ashmes.local` / `Admin123!`
- `manager@ashmes.local` / `Manager123!`
- `analyst@ashmes.local` / `Analyst123!`
- `viewer@ashmes.local` / `Viewer123!`

# 10. Current Priorities

- Frontend workflow coverage over existing backend APIs.
- Analytics UX for orders, reviews, campaigns, expenses, logistics, recommendations.
- Dense tables, filtering UX, selectors, column controls, URL sync.
- Overview dashboard over real backend data.
- Recommendation/signal semantics backed by real data.
- Authorization/workspace permission hardening later.
- Parser ingestion later.
- ML/recommendation runtime later.
- Testing, observability, performance, deployment hardening later.

# 11. Source Of Truth

- `docs/BACKLOG.md` = operational roadmap.
- Full docs are required for architecture-level changes.
- Read `docs/AI_CONTEXT.md` before non-trivial work.
- Read `docs/ARCHITECTURE.md` for subsystem boundaries.
- Read `docs/DATABASE.md` and `docs/ENTITY_CONVENTIONS.md` before DB/entity work.
- Read `docs/DECISIONS.md` before changing architecture assumptions.
- Read `docs/API_GUIDELINES.md` and `docs/API_IMPLEMENTATION_PATTERN.md` before API work.
- `Documents/` contains local requirements and visual references; it is not source code.

# 12. How To Work With Codex

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
