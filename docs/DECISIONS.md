# Architecture Decisions

## Table of Contents

- [ADR-001: SKU and Marketplace External IDs Are Strings](#adr-001-sku-and-marketplace-external-ids-are-strings)
- [ADR-002: Product Media Uses Separate Tables](#adr-002-product-media-uses-separate-tables)
- [ADR-003: JSONB Uses JsonDocument for Dynamic JSON](#adr-003-jsonb-uses-jsondocument-for-dynamic-json)
- [ADR-004: Do Not Invent Enum Values](#adr-004-do-not-invent-enum-values)
- [ADR-005: EF Core Fluent API Is the Persistence Source of Truth](#adr-005-ef-core-fluent-api-is-the-persistence-source-of-truth)
- [ADR-006: Migrations Are Reviewed Dedicated Tasks](#adr-006-migrations-are-reviewed-dedicated-tasks)
- [ADR-007: Documents Folder Is Excluded from Git](#adr-007-documents-folder-is-excluded-from-git)
- [ADR-008: Architecture Context Belongs in Docs, Not Chat History](#adr-008-architecture-context-belongs-in-docs-not-chat-history)
- [ADR-009: Recommendation Score Uses Decimal](#adr-009-recommendation-score-uses-decimal)
- [ADR-010: ProductId Is a Value Object With Value Equality](#adr-010-productid-is-a-value-object-with-value-equality)
- [ADR-011: Persisted DateTime Values Are UTC](#adr-011-persisted-datetime-values-are-utc)
- [ADR-012: Runtime PostgreSQL Setup Exists Before First Migration](#adr-012-runtime-postgresql-setup-exists-before-first-migration)
- [ADR-013: Analytical Data Uses Restrictive Delete Behavior](#adr-013-analytical-data-uses-restrictive-delete-behavior)
- [ADR-014: API CRUD Uses Resource Services Without CQRS Boilerplate](#adr-014-api-crud-uses-resource-services-without-cqrs-boilerplate)
- [ADR-015: Marketplaces Is the First API Vertical Slice](#adr-015-marketplaces-is-the-first-api-vertical-slice)
- [ADR-016: API Coverage Is Added in Reviewed Vertical Stages](#adr-016-api-coverage-is-added-in-reviewed-vertical-stages)
- [ADR-017: Complex List Filters Use Resource-Specific Query DTOs](#adr-017-complex-list-filters-use-resource-specific-query-dtos)
- [ADR-018: Advertising, Finance, and Workspace APIs Follow Existing CRUD Pattern](#adr-018-advertising-finance-and-workspace-apis-follow-existing-crud-pattern)
- [ADR-019: Access and Rules APIs Follow Existing CRUD Pattern](#adr-019-access-and-rules-apis-follow-existing-crud-pattern)
- [ADR-020: Recommendations APIs Expose JSONB Through JsonElement](#adr-020-recommendations-apis-expose-jsonb-through-jsonelement)
- [ADR-021: Auth Security Foundation Uses JWT Access Tokens and Opaque Refresh Sessions](#adr-021-auth-security-foundation-uses-jwt-access-tokens-and-opaque-refresh-sessions)
- [ADR-022: Frontend Foundation v2 Uses Feature-Oriented Vue](#adr-022-frontend-foundation-v2-uses-feature-oriented-vue)
- [ADR-023: Development Seed Foundation Is Development-Only and Config-Gated](#adr-023-development-seed-foundation-is-development-only-and-config-gated)
- [ADR-024: Frontend Visual System Uses Obsidian Marketplace Intelligence Theme](#adr-024-frontend-visual-system-uses-obsidian-marketplace-intelligence-theme)
- [ADR-025: Local Dev Startup Uses Scripts With Managed Logs And PIDs](#adr-025-local-dev-startup-uses-scripts-with-managed-logs-and-pids)
- [ADR-026: Theme Changes Must Flow Through Semantic CSS Tokens](#adr-026-theme-changes-must-flow-through-semantic-css-tokens)
- [ADR-027: Products Polish Stage 1 Uses Existing Product API And Field-Derived Signals](#adr-027-products-polish-stage-1-uses-existing-product-api-and-field-derived-signals)

## ADR-001: SKU and Marketplace External IDs Are Strings

### Context

Marketplace identifiers, seller articles, product SKUs, and external IDs may come from external systems. Documentation used mixed representations, and early `Product` code used numeric SKU fields.

### Problem

Numeric IDs would break for:

- leading zeros;
- alphanumeric vendor codes;
- marketplace-specific formatting;
- IDs that exceed integer limits;
- IDs that are identifiers rather than quantities.

### Decision

Use `string` for:

- `SkuSeller`;
- `SkuProduct`;
- `IdOnMp`;
- other external marketplace codes.

Default max length is `128` unless a more specific documented constraint exists.

### Consequences

- No numeric comparisons on SKU fields.
- Validation focuses on requiredness and length, not numeric ranges.
- The model can support multiple marketplaces without refactoring ID types.

## ADR-002: Product Media Uses Separate Tables

### Context

The ER documentation represented product images and videos as JSON fields on `Products`. Earlier code represented them as owned collections.

### Problem

JSON media arrays are inconvenient for:

- ordering;
- marking main image;
- partial updates;
- validating URLs;
- indexing;
- future metadata such as dimensions, source, moderation status.

### Decision

Use separate tables:

- `ProductImages`;
- `ProductVideos`.

They are normal entity classes with `id_product` FK and cascade delete from `Products`.

### Consequences

- Product media is queryable and extensible.
- Product aggregate remains clear.
- The implementation intentionally differs from the original ER JSON field for long-term maintainability.

## ADR-003: JSONB Uses JsonDocument for Dynamic JSON

### Context

Several fields are JSON by nature:

- product characteristics;
- campaign impression schedule;
- recommendation explanation;
- recommendation snapshot/context.

### Problem

Plain `string` would store JSON without type-level meaning. Strong POCO/value objects would require stable schemas, which are not yet known.

### Decision

Use `JsonDocument?` mapped to PostgreSQL `jsonb` for dynamic JSON fields.

### Consequences

- JSON is represented as JSON in .NET, not as arbitrary text.
- Entities with `JsonDocument?` implement `IDisposable`.
- Value objects may be introduced later when schemas stabilize.

## ADR-004: Do Not Invent Enum Values

### Context

Project documentation marks many fields as `enum`, but does not define allowed values.

### Problem

Inventing enum members would create false business semantics and migration churn.

### Decision

Store undocumented enum fields as `int` and add comments:

```csharp
public int Status { get; private set; } // enum по документации, значения не определены
```

`ProductStatus` remains a real enum because values already existed in code.

### Consequences

- The database model can be implemented now.
- Business enum values can be added later from real requirements.
- API contracts must not expose fake enum names.

## ADR-005: EF Core Fluent API Is the Persistence Source of Truth

### Context

The project uses Domain entities and separate DataAccess configurations.

### Problem

Mixing DataAnnotations, implicit EF conventions, and Fluent API would make mappings hard to audit.

### Decision

Use `IEntityTypeConfiguration<T>` for:

- table names;
- column names;
- required/nullability;
- length constraints;
- precision;
- JSONB;
- keys;
- FKs;
- delete behavior.

### Consequences

- Domain classes remain persistence-light.
- Configuration files are the authoritative database mapping.
- Future migrations should be reviewed against configuration files.

## ADR-006: Migrations Are Reviewed Dedicated Tasks

### Context

The entity layer is built incrementally. PostgreSQL provider registration exists, and the reviewed `InitialCreate` migration has been created and applied during verification.

### Problem

Creating migrations too early locks in a model before review and before runtime DB configuration. After `InitialCreate`, unreviewed follow-up migrations would create the same risk for later subsystem changes.

### Decision

Do not create migrations during entity design stages or API-only stages. Create future migrations only as dedicated reviewed tasks after intentional model changes.

### Consequences

- Current build validates compile-time model shape and runtime DbContext registration can be resolved by EF tooling.
- `InitialCreate` is the current committed baseline migration.
- No new generated migration files should appear unless explicitly requested.

## ADR-007: Documents Folder Is Excluded from Git

### Context

`Documents/` contains project materials, docx files, screenshots, and reference artifacts.

### Problem

These files are large and not part of backend source code.

### Decision

Exclude `Documents/` from git.

### Consequences

- Source commits remain focused on code.
- AI can read local documents when present.
- Project documentation that should be versioned must be moved into a dedicated docs strategy and explicitly tracked.

## ADR-008: Architecture Context Belongs in Docs, Not Chat History

### Context

The project is being developed over multiple AI sessions.

### Problem

Chat history is not a stable source of truth. New AI sessions may lose prior decisions and repeat analysis or introduce inconsistent conventions.

### Decision

Record architecture, database shape, entity conventions, API guidelines, and AI context in markdown files under `/docs`.

### Consequences

- Future AI chats can start from `docs/AI_CONTEXT.md`.
- Decisions are easier to audit.
- Architecture should be updated whenever conventions or implemented scope changes.

## ADR-009: Recommendation Score Uses Decimal

### Context

Recommendation has a `Score` field used for ranking or model output.

### Problem

`double` is natural for ML computations but may introduce binary floating point representation artifacts in persisted business data.

### Decision

Persist `Score` as `decimal` with EF precision `18,6`.

### Consequences

- Stored scores are deterministic for filtering, sorting, and reporting.
- If ML services produce `double`, conversion should happen at application boundary.
- Precision can be revised if model requirements demand more scale.

## ADR-010: ProductId Is a Value Object With Value Equality

### Context

`Product.Id` is a domain-specific identifier and is persisted as PostgreSQL `uuid`.

### Problem

A reference-type ID without value equality can create confusing behavior for EF tracking, comparisons, and relationship fixup.

### Decision

Represent `ProductId` as a readonly value object with value equality and keep EF conversion to/from `Guid`.

Guid/ProductId primary keys are generated by application constructors/factories and configured with `.ValueGeneratedNever()`.

`Session.Id` remains the documented integer identity exception.

### Consequences

- `ProductId` compares by value.
- EF mappings continue to persist it as `uuid`.
- Application code remains responsible for creating stable IDs before persistence.

## ADR-011: Persisted DateTime Values Are UTC

### Context

PostgreSQL runtime setup uses Npgsql, and Npgsql maps .NET `DateTime` to PostgreSQL timestamp types with clear UTC expectations for `timestamptz`.

### Problem

Mixing `Local`, `Unspecified`, and UTC values can create incorrect persisted timestamps and migration/runtime surprises.

### Decision

All persisted `DateTime` and `DateTime?` values accepted by domain constructors/factories must be UTC.

The Domain layer uses `DateTimeUtc` to validate persisted date/time arguments.

### Consequences

- Application/service boundaries must normalize date/time input to UTC.
- Domain entities reject local/unspecified persisted timestamps.
- The model is prepared for PostgreSQL `timestamptz`.

## ADR-012: Runtime PostgreSQL Setup Exists Before First Migration

### Context

The model was stabilized before creating the first migration.

### Problem

EF migrations need a real provider, runtime registration, and design-time DbContext creation. Earlier code had only provider-independent EF Core packages.

### Decision

Configure runtime PostgreSQL infrastructure before creating migrations:

- `Npgsql.EntityFrameworkCore.PostgreSQL` in DataAccess;
- API DbContext registration using `ConnectionStrings:Postgres`;
- `ApplicationDbContextFactory` for EF design-time tooling;
- local Docker Compose infrastructure for PostgreSQL, pgAdmin, Redis, and MinIO;
- local `dotnet-ef` tool manifest.

Do not auto-apply migrations on application startup.

### Consequences

- `dotnet ef` can resolve `ApplicationDbContext`.
- First migration remains a dedicated reviewed task.
- Local infrastructure can be started independently from backend runtime.

## ADR-013: Analytical Data Uses Restrictive Delete Behavior

### Context

Product, order, review, logistics, campaign, recommendation, and expense data are analytical/business history.

### Problem

Broad cascade deletes can accidentally remove historical or financial data when a product, campaign, review, or workspace is deleted.

### Decision

Use restrictive delete behavior for analytical/history relationships.

Keep cascade only for true cleanup children, such as product media, user sessions, workspace membership rows, and recommendation links from the recommendation side.

### Consequences

- Historical records are protected from accidental root deletion.
- Deletion workflows must explicitly handle dependent analytical data.
- First migration must be reviewed for FK delete actions before applying.

## ADR-014: API CRUD Uses Resource Services Without CQRS Boilerplate

### Context

The project needs CRUD endpoints for many backend entities, but adding separate commands, queries, handlers, repositories, and validators for every small CRUD operation would create excessive boilerplate.

### Problem

The backend has 30+ entities. A handler-per-operation or generic CRUD architecture would either multiply files unnecessarily or hide behavior behind abstractions that are harder to audit.

### Decision

Use a pragmatic resource-service pattern:

- one controller per public resource group;
- one service per resource/entity, for example `IMarketplaceService`;
- direct `ApplicationDbContext` access from Application services;
- no repository/unit-of-work wrapper over EF Core for simple CRUD;
- no CQRS/handler-per-operation for basic CRUD;
- no generic CRUD controller/service abstraction;
- manual mapping between entities and DTOs;
- FluentValidation for request DTOs;
- ProblemDetails for API errors;
- `ListQuery` and `PagedResponse<T>` for paged list endpoints.
- resource-specific query DTOs when a resource needs filters beyond the shared catalog query shape.

### Consequences

- CRUD slices stay readable and small.
- EF Core remains the data access abstraction.
- Future complex workflows can still receive dedicated use cases when real behavior appears.
- Product and Operations list endpoints can expose richer filters without forcing those filters onto simple catalog resources.

## ADR-015: Marketplaces Is the First API Vertical Slice

### Context

The backend needed the first end-to-end API/Application implementation after entities, PostgreSQL runtime setup, and `InitialCreate` migration.

### Problem

Implementing CRUD for all entities at once would create too much surface area before the API pattern was proven.

### Decision

Implement `Marketplaces` first:

- `GET /api/v1/marketplaces`;
- `GET /api/v1/marketplaces/{id}`;
- `POST /api/v1/marketplaces`;
- `PUT /api/v1/marketplaces/{id}`;
- `DELETE /api/v1/marketplaces/{id}`.

Continue reference/catalog CRUD with separate services:

- `IBrandService`;
- `ICategoryService`;
- `IWarehouseService`.

Do not use a shared `CatalogService` for all reference entities.

### Consequences

- The API foundation is validated on a simple reference entity.
- Next reference resources can follow the same pattern without creating generic CRUD abstractions.
- Soft delete remains a future design concern; current delete is hard delete behind service methods such as `DeleteAsync`.

## ADR-016: API Coverage Is Added in Reviewed Vertical Stages

### Context

After the first `Marketplaces` slice, the project needed broader CRUD coverage while still avoiding a one-shot API surface for every implemented entity.

### Problem

Adding all controllers at once would make review, smoke testing, and API contract consistency harder. Waiting too long to expose core aggregates would block backend validation work.

### Decision

Add API coverage in reviewed vertical stages:

1. Catalog/reference APIs:
   - `Marketplaces`;
   - `Brands`;
   - `Categories`;
   - `Warehouses`.
2. Product API stage 1:
   - `Products` CRUD;
   - product detail with characteristics JSON and media DTOs;
   - read-only product history.
3. Operations APIs:
   - `Orders`;
   - `Reviews`;
   - `ReviewReplies`;
   - `Logistics`.

Remaining subsystems should continue the same reviewed-stage approach.

### Consequences

- API coverage grows without introducing a generic CRUD framework.
- Each stage can be built, Swagger-checked, smoke-tested against PostgreSQL, and committed independently.
- Auth/workspace authorization remains a separate stage.

## ADR-017: Complex List Filters Use Resource-Specific Query DTOs

### Context

Simple catalog resources share `ListQuery` with `page`, `pageSize`, `sort`, and `search`. Product and Operations resources need filters such as product IDs, statuses, date ranges, warehouse IDs, ratings, and reply flags.

### Problem

Expanding shared `ListQuery` for all resources would leak unrelated filters into catalog endpoints. A generic filtering framework would add unnecessary complexity before client needs are stable.

### Decision

Use resource-specific query DTOs for richer list endpoints, for example:

- `ProductListQuery`;
- `OrderListQuery`;
- `ReviewListQuery`;
- `ReviewReplyListQuery`;
- `LogisticListQuery`.

Keep sorting/filtering allowlisted inside the resource service/validator pair.

### Consequences

- Each API slice remains explicit and easy to review.
- Catalog endpoints stay small.
- The project avoids generic query frameworks while keeping room for future shared helpers if patterns stabilize.

## ADR-018: Advertising, Finance, and Workspace APIs Follow Existing CRUD Pattern

### Context

After Catalog, Product, and Operations APIs were implemented, the next API coverage stages exposed existing Advertising, Finance, and Users/Workspaces database entities.

### Problem

The project needed CRUD coverage for campaigns, campaign metrics, expenses, expense categories, users, workspaces, and workspace membership without changing the EF model, adding migrations, or introducing auth/session behavior before those concerns were reviewed.

### Decision

Use the same resource-service CRUD pattern for:

- `Campaigns`;
- `CampaignMetrics`;
- `Expenses`;
- `ExpenseCategories`;
- `Users`;
- `Workspaces`;
- `UserWorkspaces`.

Keep Application services on direct `ApplicationDbContext`, manual DTO mapping, FluentValidation, allowlisted filtering/sorting, `ServiceResult`, ProblemDetails, async EF calls, and hard delete with `409 Conflict` for FK/database conflicts.

At those CRUD stages, do not add `Sessions API`, auth/JWT/session login, repositories, CQRS, generic CRUD abstractions, schema changes, or migrations.

`User.Password` is accepted by create/update DTOs because the existing entity requires it, but it is not returned by response DTOs. Password hashing and authentication were intentionally deferred from these CRUD stages and later implemented by ADR-021.

### Consequences

- API coverage now includes Advertising, Finance, and Users/Workspaces while preserving the established architecture.
- `Expenses` can be runtime-smoke-tested through API-created `Users` and `Workspaces`.
- Role data is still required for `Users` and `UserWorkspaces`; after the Access API stage, verification can create roles through the API instead of temporary SQL fixtures.
- Auth was intentionally deferred from this CRUD stage and later implemented by ADR-021; workspace authorization remains a future dedicated stage.

## ADR-019: Access and Rules APIs Follow Existing CRUD Pattern

### Context

After Advertising, Finance, and Workspace APIs, the backend needed CRUD coverage for existing Access and Rules tables without changing schema or introducing auth behavior.

### Problem

`Roles`, `Permissions`, `PermissionCategories`, `RolePermissions`, `RoleSubroles`, `Rules`, `RuleSets`, and `RuleSetRules` are existing EF entities used by other subsystems. Exposing them should not create a new architecture or imply completed authorization/session behavior.

### Decision

Expose Access and Rules as reviewed API/Application-only CRUD slices using the existing resource-service pattern:

- separate controllers, DTOs, validators, and services;
- direct `ApplicationDbContext`;
- manual mapping;
- resource-specific query DTOs where filters differ;
- explicit composite-key routes for join resources;
- no `PUT` for key-only joins;
- no auth/JWT/session login, repositories, CQRS, generic CRUD abstractions, schema changes, or migrations in that Access/Rules CRUD stage.

### Consequences

- Access and Rules reference data can be managed through API endpoints.
- `Users` and `UserWorkspaces` verification can use API-created roles.
- Auth/session flow was later implemented by ADR-021; workspace authorization and permission policy enforcement remain future dedicated stages.

## ADR-020: Recommendations APIs Expose JSONB Through JsonElement

### Context

`Recommendation` has dynamic JSONB fields `Explanation` and `Snapshot`, and recommendation target links are explicit join entities for products and categories.

### Problem

The API needs to expose recommendation results without stabilizing JSON schemas or adding ML inference, background jobs, parser integration, or recommendation engine logic.

### Decision

Expose Recommendations as an API/Application-only CRUD slice:

- `Recommendations` CRUD uses `JsonElement?` in request/response DTOs and converts to/from `JsonDocument?` inside services;
- `RecommendationProducts` and `RecommendationCategories` are explicit composite-key join resources;
- `RecommendationProduct.IdProduct` is exposed as `Guid` and converted with `ProductId.Create(...)`;
- list filters are allowlisted and do not do full-text search over JSONB;
- no schema changes, migrations, ML inference, background jobs, parser integration, repositories, CQRS, or generic CRUD abstractions.

### Consequences

- Recommendation records and target links can be smoke-tested and managed through the API.
- Dynamic JSON remains flexible until schemas are stable.
- ML/runtime recommendation generation remains a separate future integration stage.

## ADR-021: Auth Security Foundation Uses JWT Access Tokens and Opaque Refresh Sessions

### Context

CRUD API coverage is completed, `Users`, `Roles`, `Permissions`, `Workspaces`, `UserWorkspaces`, and `Sessions` already exist, and `InitialCreate` is the current migration baseline.

### Problem

The project needed a minimal safe auth foundation without schema churn, OAuth, 2FA, a complex RBAC engine, or breaking existing CRUD smoke-tests.

### Decision

Implement Auth/Security Foundation in commit `2201971 Add auth security foundation`:

- Passwords are hashed with ASP.NET Core `PasswordHasher`.
- `Users.password` remains the database column and now semantically stores password hashes.
- Authentication uses JWT access tokens plus opaque refresh tokens.
- `Sessions` is used for refresh/session storage.
- `Sessions.token` stores only a refresh-token hash, not the raw token.
- `Sessions.status` uses `1` for active and `2` for revoked in the auth layer.
- Auth endpoints are:
  - `POST /api/v1/auth/login`;
  - `POST /api/v1/auth/refresh`;
  - `POST /api/v1/auth/logout`;
  - `GET /api/v1/auth/me`.
- Existing CRUD APIs remain anonymous in this stage for backward compatibility.
- Swagger remains Development-only and includes Bearer auth support.

### Consequences

- No migration or DataAccess schema change was required.
- Existing plaintext test users cannot log in until their password is reset through the current Users API or recreated.
- Access tokens remain valid until expiry after logout; refresh/session state is revoked through `Sessions`.
- Workspace authorization, permission policies, rate limiting, lockout, OAuth, external providers, 2FA, and full RBAC remain future dedicated stages.
- Frontend Foundation v2 was later implemented by ADR-022.

## ADR-022: Frontend Foundation v2 Uses Feature-Oriented Vue

### Context

Backend CRUD coverage and Auth/Security Foundation were complete, so the project needed the first seller-facing frontend foundation without copying a specific reference product.

### Problem

The frontend needed real backend integration and premium B2B SaaS dashboard structure, but it should not become a backend entity mirror or attempt every page at once.

### Decision

Implement Frontend Foundation v2 in commit `a897c18 Add frontend foundation`:

- Vue 3, TypeScript, Composition API, Vite, Vue Router, Pinia, Axios, and Tailwind CSS tokens/components.
- Feature-oriented source structure under `Frontend/src/features`, with `entities` reserved for shared model types.
- Persistent protected app shell with sidebar, topbar, content container, and page header.
- Auth frontend flow with login, logout, refresh, persisted MVP token storage, and protected routes.
- Products vertical slice with real backend integration, filters, sorting, pagination, URL query sync, KPI cards, and loading/error/empty states.
- Overview, Orders, Reviews, Logistics, Campaigns, Expenses, Recommendations, and Access settings remain lightweight placeholders.

### Consequences

- Frontend foundation is implemented and should no longer be described as absent or future work.
- Next frontend work should build on the existing shell and feature structure.
- Recommended next frontend stages are product polish, Marketplaces page, Overview dashboard, and broader analytics UX.
- Access token storage in localStorage is an MVP approach; future hardening may move access tokens to memory-only storage.

## ADR-023: Development Seed Foundation Is Development-Only and Config-Gated

### Context

Local frontend and API testing needed deterministic accounts, roles, workspace data, products, and analytical demo records. Public registration is intentionally not part of the current auth foundation.

### Problem

Developers needed easy local login and role/scenario testing without adding migrations, exposing registration, storing plaintext passwords, or risking Production seed execution.

### Decision

Implement Development Seed Foundation in commit `2315ee9 Add development seed foundation`:

- Seed code lives under `AshmesMarketplaces.API/DevelopmentSeed`.
- Seed runs only when the environment is `Development` and `Seed:EnableDevelopmentSeed=true`.
- Default Development config keeps `Seed:EnableDevelopmentSeed=false`.
- Seed creates roles `Admin`, `Manager`, `Analyst`, `Viewer`.
- Seed creates dev users:
  - `admin@ashmes.local` / `Admin123!`;
  - `manager@ashmes.local` / `Manager123!`;
  - `analyst@ashmes.local` / `Analyst123!`;
  - `viewer@ashmes.local` / `Viewer123!`.
- Seed creates one workspace, marketplace/catalog records, products, and minimal orders/reviews/campaigns/expenses for UI testing.
- Seed uses existing `IPasswordHashService`/`PasswordHasher`; plaintext passwords are never stored in the database.
- Seed is idempotent through lookup-based business keys and does not call `Database.Migrate()`.

### Consequences

- No migration or schema change was required after `InitialCreate`.
- Public registration remains deferred because it needs separate security, validation, verification, abuse-control, and permission design.
- Seed credentials may be logged/documented only for local Development use.
- Authorization hardening remains a later dedicated stage.

## ADR-024: Frontend Visual System Uses Obsidian Marketplace Intelligence Theme

### Context

Frontend Foundation v2 provided a working shell and Products slice, but the first visual implementation was still closer to a generic light SaaS dashboard than the intended marketplace intelligence workstation.

### Problem

The product needs a premium analytical identity without copying competitor visuals and without turning fire into decoration. It also needs to avoid fake analytics while real growth, demand, and recommendation metrics are not available in the Products list API.

### Decision

Implement Frontend Visual Redesign Stage 1 in commit `b18cd90 Add local dev startup scripts and harden theme tokens`:

- Use a dark obsidian marketplace intelligence visual direction.
- Keep layouts compact and data-first.
- Use layered transparent panels, restrained ember accents, readable dense tables, and clear hover/focus states.
- Treat fire/ember as a signal system only.
- Do not use flame images, emoji, animated fire, excessive glow, gaming/casino visuals, or toy-like animations.
- Add Products heat indicators only as presentation scaffolding derived from current product fields such as status/update recency.
- Do not show fake demand, growth, or recommendation numbers.
- Do not describe presentation heat signals as real demand/growth/recommendation metrics.

### Consequences

- Products table readability and row scan speed take priority over decorative KPI polish.
- Future real heat/recommendation UI must be backed by explicit backend/API data before it can claim business meaning.
- Competitors may inform navigation/workflow ideas, not copied visual styling.

## ADR-025: Local Dev Startup Uses Scripts With Managed Logs And PIDs

### Context

Local development requires PostgreSQL through Docker Compose, backend API, and frontend Vite. Running these manually was repetitive and error-prone on Windows.

### Problem

Developers needed one-command startup and shutdown without adding a heavy task runner, auto-applying migrations, enabling seed unexpectedly, deleting volumes, or killing unrelated processes on occupied ports.

### Decision

Add local dev scripts in commit `b18cd90 Add local dev startup scripts and harden theme tokens`:

- PowerShell:
  - `scripts/dev/start-dev.ps1`;
  - `scripts/dev/stop-dev.ps1`.
- Bash / Git Bash / WSL-style:
  - `scripts/dev/start-dev.sh`;
  - `scripts/dev/stop-dev.sh`.
- Default startup runs PostgreSQL and pgAdmin from `docker-compose.local.yml`, then backend API on `http://localhost:5019`, then frontend Vite on `http://localhost:5173`.
- Runtime logs and PID files are stored under `.dev/`, which is ignored by git.
- Stop scripts stop only managed backend/frontend processes from PID files.
- Docker services are stopped only with explicit flags:
  - PowerShell: `-StopDocker`;
  - Bash: `--docker`.
- Volumes are never deleted by these scripts.
- Startup scripts do not apply migrations automatically and do not enable development seed automatically.

### Consequences

- Local developer startup is standardized without introducing a root-level npm task runner.
- Port conflicts with unmanaged processes are surfaced as errors instead of being killed.
- Database schema and seed behavior remain explicit reviewed operations.

## ADR-026: Theme Changes Must Flow Through Semantic CSS Tokens

### Context

Frontend Visual Redesign Stage 1 introduced a dark obsidian visual system. The first pass still had some raw component-level colors.

### Problem

Future theme changes would be brittle if visual colors were scattered across shared primitives and shell components.

### Decision

Harden frontend theme tokens in commit `b18cd90 Add local dev startup scripts and harden theme tokens`:

- `Frontend/src/styles/tokens.css` is the theme source of truth.
- Semantic variables are scoped through `:root, [data-theme="obsidian"]`.
- Tokens cover background, surfaces, controls, tables, sidebar/topbar, borders, text, muted text, ember/fire accents, state colors, focus rings, shadows, and heat states.
- Shared primitives should consume semantic tokens rather than hardcoded colors.
- Backward-compatible `--color-*` aliases may remain where they reduce churn.
- No theme switcher UI is implemented in this stage.
- Do not add UI libraries, chart libraries, or animation libraries for theme changes.

### Consequences

- Future visual themes should be implemented mostly by updating CSS variables.
- Components such as `Button`, `Input`, `Card`, `DataTable`, `Badge`, and shell widgets should not need rewrites for theme color changes.
- New frontend components should avoid raw visual color literals outside token definitions.

## ADR-027: Products Polish Stage 1 Uses Existing Product API And Field-Derived Signals

### Context

Frontend Foundation v2 and the obsidian visual system already provided a working Products page integrated with real backend data. The page needed to become the reference marketplace intelligence workstation screen without changing backend contracts or inventing analytics that do not exist yet.

### Problem

The Products page needed better density, row scan speed, filters, and detail inspection. At the same time, the frontend had to avoid fake demand/growth/recommendation metrics, avoid route/API/auth/schema changes, and correct the frontend interpretation of `ProductStatus`.

### Decision

Implement Products Polish Stage 1 as a frontend-only stage:

- Keep `/products` and existing Products API contracts unchanged.
- Continue listing through `GET /api/v1/products`.
- Open a read-only side drawer from product rows and fetch existing `GET /api/v1/products/{id}` for description, characteristics JSON, images, videos, identifiers, status, and dates.
- Use the backend `ProductStatus` enum mapping exactly:
  - `Draft=0`;
  - `Pending=1`;
  - `Active=2`;
  - `Rejected=3`;
  - `Blocked=4`;
  - `Archived=5`;
  - `OutOfStock=6`;
  - `Disappeared=7`.
- Derive heat/signal tiers only from existing `status` and `dateUpdated`:
  - `hot`: `Active` updated within 7 days;
  - `rising`: `Active` updated within 30 days, or `Pending` updated within 7 days;
  - `warm`: active, pending, out-of-stock records not qualifying above, or any product updated within 60 days;
  - `dormant`: draft/rejected/blocked/archived/disappeared, invalid date, or older than 60 days.
- Present heat as signal scaffolding only through badges, mini bars, row accents, and drawer summary.
- Keep filters URL-query driven and add compact toolbar, status select, quick reset, and active chips.
- Allow row click and simple keyboard interaction (`Enter`/`Space`) to open the drawer; close with button, backdrop, or `Escape`.

### Consequences

- Products is now the frontend benchmark screen for dense marketplace-intelligence UI.
- No backend API contract, auth behavior, route path, schema, migration, parser, ML, chart library, UI library, or animation library was introduced.
- Product detail is read-only; create/edit/delete/media mutation workflows remain future scoped tasks.
- Heat/signal UI remains intentionally non-analytical until real demand, growth, or recommendation data is added through reviewed backend/API work.
