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
