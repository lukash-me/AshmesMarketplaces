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

The current reliable implementation focus is the .NET backend entity/EF Core mapping layer, PostgreSQL runtime setup, reviewed initial migration, and incremental API/Application CRUD vertical slices.

## Current Backend Status

Backend solution:

```text
E:\AshmesMarketplaces\Backend\AshmesMarketplaces.sln
```

Projects:

- `AshmesMarketplaces.API`: ASP.NET Core host with PostgreSQL DbContext registration, controllers, Development-only Swagger, ProblemDetails setup, FluentValidation action filter, and CRUD controllers for Catalog, Product, and Operations resources.
- `AshmesMarketplaces.Application`: DTOs, shared pagination/result models, validators, and resource services for implemented API vertical slices.
- `AshmesMarketplaces.Domain`: entities, shared errors/constants, `ProductId`, UTC `DateTime` guard.
- `AshmesMarketplaces.DataAccess`: EF Core `ApplicationDbContext`, configurations, Npgsql provider, design-time DbContext factory.
- `AshmesMarketplaces.Infrastructure`: placeholder.

Important:

- `InitialCreate` migration exists under `AshmesMarketplaces.DataAccess/Migrations` with prefix `20260518152454_InitialCreate`.
- PostgreSQL is target DB; Npgsql runtime setup is configured, and the first migration was applied successfully to local PostgreSQL during verification.
- Local development infrastructure files exist for PostgreSQL, pgAdmin, Redis, and MinIO.
- API foundation is implemented across reviewed vertical slices:
  - Catalog/reference CRUD: `Marketplaces`, `Brands`, `Categories`, `Warehouses`;
  - Product aggregate stage 1: root `Products` CRUD plus read-only `ProductHistory` list;
  - Operations CRUD: `Orders`, `Reviews`, `ReviewReplies`, `Logistics`.
- Auth/JWT/workspace authorization are not implemented.
- Frontend/parser/ML must not be touched unless explicitly requested.

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

Current working tree after Operations API slice:

- Working tree is expected to be clean.
- Runtime database setup is committed.
- `InitialCreate` migration is committed.
- Catalog, Product, and Operations API/Application vertical slices are committed.
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

## What AI Must Not Do

Unless explicitly requested:

- Do not create migrations.
- Do not modify frontend.
- Do not modify parser.
- Do not modify ML/Intelligence.
- Do not modify docker/infrastructure deployment unless the task is explicitly about runtime/local infrastructure.
- Do not invent business enum values.
- Do not add API controllers just because entities exist; extend API one reviewed vertical slice at a time.
- Do not add unique indexes based on assumptions.
- Do not commit automatically unless asked.
- Do not track `/docs` files in git if the active instruction says docs must stay outside repo tracking.

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

## How to Work with Documentation

Before changing architecture or entities:

1. Read `docs/AI_CONTEXT.md`.
2. Read `docs/ENTITY_CONVENTIONS.md`.
3. For DB/table changes, read `docs/DATABASE.md`.
4. For new architectural tradeoffs, update `docs/DECISIONS.md`.
5. Keep docs aligned with code if the user asks to track docs.

## How to Continue Development

Recommended next backend tasks:

1. Continue CRUD coverage for remaining subsystems one reviewed vertical slice at a time:
   access/reference data, workspaces/finance, advertising, recommendations.
2. Add focused tests for entity validation, EF model metadata, and API/Application services.
3. Consider a minimal API infrastructure hardening stage after CRUD coverage stabilizes:
   centralized `ServiceResult` to ProblemDetails mapping, request correlation, and Swagger cleanup.
4. Define reviewed seed strategy for roles, permissions, marketplaces, and development test data.
5. Plan auth/JWT/workspace authorization only as a dedicated stage.

## How to Start a New AI Chat

Paste or instruct the new AI session to read:

```text
You are working in E:\AshmesMarketplaces.
First read docs/AI_CONTEXT.md, docs/ARCHITECTURE.md, docs/DATABASE.md,
docs/DECISIONS.md, and docs/ENTITY_CONVENTIONS.md.
Do not create migrations, do not touch frontend/parser/ML/docker, and preserve existing entity/configuration conventions.
Run dotnet build E:\AshmesMarketplaces\Backend\AshmesMarketplaces.sln after backend changes.
PostgreSQL runtime setup exists. InitialCreate migration exists. Implemented API slices include Catalog, Product stage 1, and Operations.
```

Then ask for the specific next stage.
