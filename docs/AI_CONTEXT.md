# AI Context for AshmesMarketplaces

## Table of Contents

- [Project Summary](#project-summary)
- [Current Backend Status](#current-backend-status)
- [Implemented Entities](#implemented-entities)
- [Completed Stages and Commits](#completed-stages-and-commits)
- [Architecture Decisions](#architecture-decisions)
- [What AI Must Not Do](#what-ai-must-not-do)
- [Coding Rules](#coding-rules)
- [EF Core Rules](#ef-core-rules)
- [PostgreSQL Rules](#postgresql-rules)
- [JSONB Rules](#jsonb-rules)
- [Migration Rules](#migration-rules)
- [Git and Commit Strategy](#git-and-commit-strategy)
- [Build Command](#build-command)
- [How to Work with Documentation](#how-to-work-with-documentation)
- [How to Continue Development](#how-to-continue-development)
- [How to Start a New AI Chat](#how-to-start-a-new-ai-chat)

## Project Summary

AshmesMarketplaces is a marketplace analytics and sales-management platform for sellers. It is intended to combine:

- marketplace data parsing;
- product-card management;
- order/review/logistics analytics;
- advertising campaign tracking;
- workspace/team access;
- recommendation/ML output.

The current reliable implementation includes the completed .NET backend foundation, completed API/Application CRUD coverage, implemented Auth/Security Foundation, implemented Development Seed Foundation, implemented Frontend Foundation v2, completed Frontend Visual Redesign Stage 1, completed Products Polish Stage 1, local dev startup scripts, and hardened frontend theme tokens.

## Current Backend Status

Backend solution:

```text
E:\AshmesMarketplaces\Backend\AshmesMarketplaces.sln
```

Projects:

- `AshmesMarketplaces.API`: ASP.NET Core host with PostgreSQL DbContext registration, controllers, Development-only Swagger with Bearer auth support, ProblemDetails setup, FluentValidation action filter, JWT Bearer authentication middleware, CRUD controllers for Catalog, Product, Operations, Advertising, Finance, Users/Workspaces, Access, Rules, and Recommendations resources, `AuthController`, and Development-only seed support.
- `AshmesMarketplaces.Application`: DTOs, shared pagination/result models, validators, resource services for implemented API vertical slices, auth DTOs/services, password hashing wrapper, JWT access-token generation, refresh-token hashing, and current-user abstraction.
- `AshmesMarketplaces.Domain`: entities, shared errors/constants, `ProductId`, UTC `DateTime` guard.
- `AshmesMarketplaces.DataAccess`: EF Core `ApplicationDbContext`, configurations, Npgsql provider, design-time DbContext factory.
- `AshmesMarketplaces.Infrastructure`: placeholder.

Important:

- `InitialCreate` migration exists under `AshmesMarketplaces.DataAccess/Migrations` with prefix `20260518152454_InitialCreate`.
- PostgreSQL is target DB; Npgsql runtime setup is configured, and the first migration was applied successfully to local PostgreSQL during verification.
- Local development infrastructure files exist for PostgreSQL, pgAdmin, Redis, and MinIO.
- CRUD API coverage is completed across reviewed vertical slices:
  - Catalog/reference CRUD: `Marketplaces`, `Brands`, `Categories`, `Warehouses`;
  - Product aggregate stage 1: root `Products` CRUD plus read-only `ProductHistory` list;
  - Operations CRUD: `Orders`, `Reviews`, `ReviewReplies`, `Logistics`.
  - Advertising CRUD: `Campaigns`, `CampaignMetrics`;
  - Finance CRUD: `Expenses`, `ExpenseCategories`;
  - Users/workspaces CRUD: `Users`, `Workspaces`, `UserWorkspaces`;
  - Access CRUD: `Roles`, `PermissionCategories`, `Permissions`, `RolePermissions`, `RoleSubroles`;
  - Rules CRUD: `Rules`, `RuleSets`, `RuleSetRules`;
  - Recommendations CRUD: `Recommendations`, `RecommendationProducts`, `RecommendationCategories`.
- Auth/Security Foundation is implemented:
  - `POST /api/v1/auth/login`;
  - `POST /api/v1/auth/refresh`;
  - `POST /api/v1/auth/logout`;
  - `GET /api/v1/auth/me`.
- Authentication uses JWT access tokens plus opaque refresh tokens.
- `Sessions` is used for refresh/session storage; `Sessions.token` stores a refresh-token hash, not the raw refresh token.
- User passwords are hashed with ASP.NET Core `PasswordHasher`; `Users.password` now semantically stores password hashes.
- Existing CRUD APIs remain anonymous in the first auth stage for backward compatibility.
- Development Seed Foundation is implemented in the API project:
  - seed code lives under `AshmesMarketplaces.API/DevelopmentSeed`;
  - seed runs only in `Development` when `Seed:EnableDevelopmentSeed=true`;
  - default `appsettings.Development.json` value is `false`;
  - seed creates roles, deterministic dev accounts, one workspace, marketplace/catalog records, products, and minimal orders/reviews/campaigns/expenses for UI testing;
  - seed uses `IPasswordHashService`/`PasswordHasher`; plaintext passwords are not stored in the database.
- Workspace authorization and full permission policy enforcement are not implemented yet.
- Frontend Foundation v2 is implemented in `Frontend/` with Vue 3, TypeScript, Composition API, Vite, Vue Router, Pinia, Axios, and Tailwind-based tokens/components.
- Frontend Visual Redesign Stage 1 is implemented:
  - dark obsidian marketplace intelligence visual system;
  - compact protected shell, sidebar, topbar, shared primitives, KPI cards, filters, and Products table styling;
  - Products table includes presentation-only heat signal scaffolding derived from current product fields such as status/update recency;
  - heat signals are not real demand, growth, or recommendation metrics.
- Frontend theme tokens are hardened:
  - `Frontend/src/styles/tokens.css` is the theme source of truth;
  - semantic CSS variables are scoped through `:root, [data-theme="obsidian"]`;
  - shared UI primitives consume semantic variables so future theme changes should flow through tokens rather than component rewrites;
  - no theme switcher UI is implemented yet.
- Frontend has a persistent protected app shell, auth login/logout/refresh/protected routes, and a Products vertical slice with real backend integration, filters, sorting, pagination, URL query sync, KPI cards, presentation-only heat signals, and loading/error/empty states.
- Products Polish Stage 1 is implemented:
  - Products table now has denser scan-oriented hierarchy, clickable/keyboard-focusable rows, selected row state, sticky header behavior, compact ID display, corrected backend `ProductStatus` mapping, and signal badges/mini bars;
  - filters now use a compact toolbar, native status select, quick reset, active filter chips, and existing URL query synchronization;
  - product detail opens in a side drawer without route changes and fetches the existing `GET /api/v1/products/{id}` response for description, characteristics JSON, images, videos, IDs, dates, status, and signal display;
  - heat/signal state remains visual scaffolding only, derived from `status` and `dateUpdated`, with `dormant`, `warm`, `rising`, and `hot` states;
  - no backend API contract, auth, route, schema, parser, ML, chart library, UI library, or animation library change was introduced.
- Local dev startup scripts are implemented:
  - `scripts/dev/start-dev.ps1`;
  - `scripts/dev/stop-dev.ps1`;
  - `scripts/dev/start-dev.sh`;
  - `scripts/dev/stop-dev.sh`.
- Local dev scripts start PostgreSQL and pgAdmin through `docker-compose.local.yml`, then backend API and frontend Vite. They store logs/PIDs under `.dev/`, which is ignored by git.
- Other frontend sections are lightweight placeholders.
- Parser/ML must not be touched unless explicitly requested.

## Implemented Entities

Implemented in Domain/DataAccess:

- Catalog/reference:
  - `Marketplace`, `Brand`, `BrandMarketplace`, `Category`, `Warehouse`
- Access:
  - `Role`, `Permission`, `PermissionCategory`, `RolePermission`, `RoleSubrole`
- Rules:
  - `Rule`, `RuleSet`, `RuleSetRule`
- Product aggregate:
  - `Product`, `ProductImage`, `ProductVideo`, `ProductHistory`
- Operations:
  - `Order`, `Review`, `ReviewReply`, `Logistic`
- Users/workspaces/finance:
  - `User`, `Session`, `Workspace`, `UserWorkspace`, `Expense`, `ExpenseCategory`
- Advertising:
  - `Campaign`, `CampaignMetric`
- Recommendations:
  - `Recommendation`, `RecommendationProduct`, `RecommendationCategory`

## Completed Stages and Commits

Recent commits:

| Commit | Meaning |
|---|---|
| `9062ac6 Add base reference entities` | Marketplaces, brands, categories, warehouses, roles, permissions, rules |
| `0c0aca0 Add product aggregate entities` | Product aggregate, media tables, history |
| `78ab339 Add order review logistics entities` | Orders, reviews, replies, logistics |
| `d7b0eb3 Ignore project documents` | `Documents/` excluded from git |
| `e38b5bf Add user workspace expense entities` | Users, sessions, workspaces, expenses |
| `6e6c310 Add campaign entities` | Campaign and campaign metrics |
| `4fb0851 Add recommendation entities` | Recommendation entities and configurations |
| `01f229a Stabilize EF model before migration` | Product ownership FKs, `ProductId` value equality, UTC policy, cascade review |
| `cb2a4a7 Add PostgreSQL runtime setup` | Npgsql provider, DbContext registration, design-time factory, local Docker infrastructure |
| `350c74a Add initial EF Core migration` | First reviewed EF Core migration, `InitialCreate` |
| `b719185 Add marketplaces API vertical slice` | API/Application foundation and `Marketplaces` CRUD endpoints |
| `318f8fb Add catalog reference APIs` | `Brands`, `Categories`, and `Warehouses` CRUD endpoints |
| `f95ff19 Add product API vertical slice` | `Products` CRUD, ProductId-as-Guid API contract, characteristics JSON, initial media URLs, read-only product history |
| `5537306 Add operations APIs` | `Orders`, `Reviews`, `ReviewReplies`, and `Logistics` CRUD endpoints |
| `7825508 Add advertising finance and workspace APIs` | `Campaigns`, `CampaignMetrics`, `Expenses`, `ExpenseCategories`, `Users`, `Workspaces`, and `UserWorkspaces` CRUD endpoints |
| `4c8b57b Add access and rules API slices` | `Roles`, `PermissionCategories`, `Permissions`, `RolePermissions`, `RoleSubroles`, `Rules`, `RuleSets`, and `RuleSetRules` CRUD endpoints |
| `0646566 Add recommendation API slices` | `Recommendations`, `RecommendationProducts`, and `RecommendationCategories` CRUD endpoints |
| `2201971 Add auth security foundation` | Password hashing, JWT access tokens, opaque refresh tokens, `Sessions` refresh/session storage, `/api/v1/auth/*` endpoints |
| `a897c18 Add frontend foundation` | Frontend Foundation v2 with Vue 3/Vite, protected shell, auth flow, and Products vertical slice |
| `2315ee9 Add development seed foundation` | Development-only seed for local roles, accounts, workspace, catalog, products, and demo UI data |
| `b18cd90 Add local dev startup scripts and harden theme tokens` | Local dev start/stop scripts, dark obsidian visual hardening, semantic theme tokens, and Products presentation-only heat signals |

Current working tree after Frontend Foundation v2, Development Seed Foundation, Frontend Visual Redesign Stage 1, and local dev/theme-token hardening:

- Backend and frontend source trees are expected to be clean after commits `2201971`, `a897c18`, `2315ee9`, and `b18cd90`.
- Documentation files under `docs/` may be locally modified when keeping architecture context aligned.
- Runtime database setup is committed.
- `InitialCreate` migration is committed.
- Catalog, Product, Operations, Advertising, Finance, Users/Workspaces, Access, Rules, and Recommendations API/Application vertical slices are committed.
- Auth/Security Foundation is committed.
- Frontend Foundation v2 is committed.
- Development Seed Foundation is committed.
- Frontend Visual Redesign Stage 1 and local dev/theme-token hardening are committed in `b18cd90`.
- Full runtime smoke-tests completed for the latest CRUD blocks, including Swagger route verification, PostgreSQL CRUD smoke, FK conflict checks, duplicate composite-key conflict checks, JSONB request/response checks for recommendations, cleanup verification, and final `0 warnings / 0 errors` build.
- Full auth runtime smoke-test completed against local PostgreSQL, including password hash verification, login, `/auth/me`, refresh rotation, old refresh rejection, logout, refresh-after-logout rejection, anonymous CRUD compatibility, cleanup verification, and final `0 warnings / 0 errors` build.
- Frontend verification completed with `npm run typecheck`, `npm run build`, browser smoke for login/protected shell/Products query sync/logout, and backend API smoke against real endpoints.
- Local dev workflow verification completed for PowerShell and Git Bash start/stop scripts, including logs/PIDs under `.dev/`, URL output, PostgreSQL/pgAdmin container startup, managed backend/frontend shutdown, and docker stop without volume deletion.
- Development seed verification completed with seed enabled, repeated seed idempotency check, dev account login checks, seeded products/orders/reviews/campaigns/expenses checks, seed-disabled startup check, and final `0 warnings / 0 errors` backend build.
- Docs are locally excluded from git tracking via `.git/info/exclude`.

## Architecture Decisions

Do not re-litigate these without explicit user request:

- SKU and external marketplace IDs are strings.
- Product media is stored in `ProductImages` and `ProductVideos`, not JSON arrays.
- Dynamic JSONB fields use `JsonDocument?`.
- Undocumented enum values are not invented; use `int` with comments.
- EF Core Fluent API is the database mapping source of truth.
- Migrations are reviewed tasks; `InitialCreate` now exists, and further migrations still require explicit request.
- No unique indexes unless documented or explicitly requested.
- `Documents/` is ignored by git.
- `ProductId` is a readonly value object with value equality and EF conversion to/from `Guid`.
- Persisted `DateTime` values are UTC-only and are prepared for PostgreSQL `timestamptz`.
- Guid and `ProductId` primary keys are application-generated with `.ValueGeneratedNever()`; `Session.Id` remains the integer identity exception.
- Product media cascades from `Product`; analytical/history records use restrictive delete behavior.
- Npgsql provider and DbContext registration are configured. `InitialCreate` exists; future migrations require explicit reviewed tasks.
- Auth uses JWT access tokens and opaque refresh tokens stored through `Sessions`; existing CRUD APIs remain anonymous until a dedicated authorization-hardening stage.
- Development seed is Development-only and config-gated with `Seed:EnableDevelopmentSeed=true`; it must not run in Production and must not apply migrations.
- Frontend is feature-oriented rather than a backend mirror; `entities` are limited to shared model types.
- Frontend visual identity is dark obsidian marketplace intelligence; fire/ember accents are signal tokens, not decorative flames.
- Products heat indicators are presentation-only scaffolding and must not be described as real demand, growth, or recommendation metrics until real analytics data exists. Current frontend heat tiers are derived only from existing `ProductStatus` and `dateUpdated`.
- Frontend theme changes should be made through semantic CSS variables in `Frontend/src/styles/tokens.css`, not by hardcoding colors in components.
- Local dev startup scripts must not apply migrations automatically, enable seed automatically, delete docker volumes, or kill untracked processes on ports.

## What AI Must Not Do

Unless explicitly requested:

- Do not create migrations.
- Do not modify frontend unless explicitly requested.
- Do not modify parser.
- Do not modify ML/Intelligence.
- Do not modify docker/infrastructure deployment unless the task is explicitly about runtime/local infrastructure.
- Do not invent business enum values.
- Do not add API controllers just because entities exist; extend API one reviewed vertical slice at a time.
- Do not add unique indexes based on assumptions.
- Do not commit automatically unless asked.
- Do not track `/docs` files in git if the active instruction says docs must stay outside repo tracking.
- Do not add flame images, fake analytics datasets, fake demand/growth/recommendation numbers, UI libraries, chart libraries, or animation libraries as part of frontend visual polish unless explicitly requested.

## Coding Rules

- Preserve existing style:
  - private EF constructor;
  - private setters;
  - public constructor/factory with obvious validation.
- Keep Domain free from EF attributes.
- Put persistence details in DataAccess configurations.
- Add comments next to int enum placeholders.
- Keep files focused and avoid unrelated refactoring.

## EF Core Rules

- One `IEntityTypeConfiguration<T>` per entity.
- Explicit `ToTable`.
- Explicit `HasColumnName`.
- Explicit key/composite key config.
- Explicit FK/delete behavior.
- Use `ApplyConfigurationsFromAssembly`.
- Add `DbSet<>` when adding an entity.
- Do not rely on implicit many-to-many skip navigations.

## PostgreSQL Rules

- Target database is PostgreSQL.
- PostgreSQL provider is installed in DataAccess: `Npgsql.EntityFrameworkCore.PostgreSQL`.
- DbContext runtime registration exists in API and uses `ConnectionStrings:Postgres`.
- Design-time EF setup exists via `ApplicationDbContextFactory`.
- Use `jsonb` for dynamic JSON.
- Use `decimal(18,2)` for money.
- Use `decimal(18,6)` for persisted recommendation score.
- Persisted `DateTime` values must be UTC.

## JSONB Rules

Current JSONB fields:

- `Product.Characteristics`
- `Campaign.TimeToImpression`
- `Recommendation.Explanation`
- `Recommendation.Snapshot`

Use `JsonDocument?` until JSON schemas are stable. Entities containing `JsonDocument?` should implement `IDisposable`.

## Migration Rules

- Do not create migrations during entity planning, runtime setup, or API-only stages.
- `InitialCreate` already exists and is the current baseline migration.
- Future migrations must be dedicated reviewed tasks after intentional schema/model changes.
- Before any future migration, check:
  - provider package and versions;
  - connection string source;
  - DbContext runtime and design-time registration;
  - table names;
  - FK delete behavior;
  - JSONB mappings;
  - ProductId conversions.
  - UTC/timestamptz behavior;
  - app-generated Guid policy.

## Git and Commit Strategy

- Commit after each logical stage when asked.
- Keep commits scoped to one subsystem/stage.
- Do not include `Documents/`.
- Do not include `/docs` if user wants docs outside repository tracking.
- Build before reporting completion.

Branch strategy:

- Existing local branch appears to be `dev`.
- Default Codex branch prefix is `codex/` only when creating new branches.
- Do not create/switch branches unless asked.

## Build Command

Run:

```powershell
dotnet build E:\AshmesMarketplaces\Backend\AshmesMarketplaces.sln
```

Expected:

```text
Сборка успешно завершена.
Предупреждений: 0
Ошибок: 0
```

## Local Dev Startup

From repository root, the preferred local startup scripts are:

```powershell
./scripts/dev/start-dev.ps1
./scripts/dev/stop-dev.ps1 -StopDocker
```

If Windows PowerShell execution policy blocks direct script execution, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev\start-dev.ps1
```

Git Bash / WSL-style workflow:

```bash
bash scripts/dev/start-dev.sh
bash scripts/dev/stop-dev.sh --docker
```

Default startup behavior:

- starts PostgreSQL and pgAdmin from `docker-compose.local.yml`;
- starts backend API on `http://localhost:5019`;
- starts frontend Vite on `http://localhost:5173`;
- prints API, Swagger, Frontend, and pgAdmin URLs;
- writes runtime logs/PIDs under `.dev/`;
- does not apply migrations automatically;
- does not enable development seed automatically;
- does not delete docker volumes.

## How to Work with Documentation

Before changing architecture or entities:

1. Read `docs/AI_CONTEXT.md`.
2. Read `docs/ENTITY_CONVENTIONS.md`.
3. For DB/table changes, read `docs/DATABASE.md`.
4. For new architectural tradeoffs, update `docs/DECISIONS.md`.
5. Keep docs aligned with code if the user asks to track docs.

## How to Continue Development

Recommended next backend tasks:

1. Add a Marketplaces page using the existing catalog API.
2. Build an Overview dashboard over real backend data and seed/demo scenarios.
3. Expand analytics UX for orders, reviews, campaigns, expenses, and logistics.
4. Continue Products UX with write workflows only when explicitly scoped; current product detail drawer is read-only and uses existing product detail API data.
5. Add focused tests for entity validation, EF model metadata, API/Application services, auth services, and development seed idempotency.
6. Consider a minimal API infrastructure hardening stage after CRUD/auth/frontend foundation stabilization:
   centralized `ServiceResult` to ProblemDetails mapping, request correlation, and Swagger cleanup.
7. Plan workspace authorization and permission policy enforcement as a dedicated backend stage.
8. Plan parser/ML integrations only as separate reviewed stages.

## How to Start a New AI Chat

Paste or instruct the new AI session to read:

```text
You are working in E:\AshmesMarketplaces.
First read docs/AI_CONTEXT.md, docs/ARCHITECTURE.md, docs/DATABASE.md,
docs/DECISIONS.md, and docs/ENTITY_CONVENTIONS.md.
Do not create migrations, do not touch parser/ML/docker, and preserve existing entity/configuration conventions.
Run dotnet build E:\AshmesMarketplaces\Backend\AshmesMarketplaces.sln after backend changes.
PostgreSQL runtime setup exists. InitialCreate migration exists. No migrations were added after InitialCreate. CRUD API coverage is completed for Catalog, Product stage 1, Operations, Advertising, Finance, Users/Workspaces, Access, Rules, and Recommendations. Auth/Security Foundation is implemented with JWT access tokens, opaque refresh tokens, `Sessions` refresh/session storage, and hashed passwords. Existing CRUD APIs are still anonymous. Development Seed Foundation is implemented and runs only in Development when Seed:EnableDevelopmentSeed=true. Frontend Foundation v2 is implemented with Vue 3 + TypeScript + Vite, protected app shell, auth flow, and Products vertical slice. Frontend Visual Redesign Stage 1 is implemented with dark obsidian marketplace intelligence UI and semantic theme tokens in Frontend/src/styles/tokens.css. Products Polish Stage 1 is implemented with dense clickable Products table rows, compact filters/chips, corrected ProductStatus labels, a read-only detail drawer backed by existing GET /api/v1/products/{id}, and presentation-only heat indicators derived only from status/dateUpdated. Local dev startup scripts exist under scripts/dev and write logs/PIDs under .dev. Recommended next direction: Marketplaces page, Overview dashboard, analytics UX, Products write/media workflows when scoped, and authorization hardening later.
```

Then ask for the specific next stage.
