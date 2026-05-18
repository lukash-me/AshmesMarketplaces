# API Guidelines

## Table of Contents

- [Current API Status](#current-api-status)
- [REST Conventions](#rest-conventions)
- [Versioning](#versioning)
- [DTO Policy](#dto-policy)
- [Validation](#validation)
- [Pagination, Filtering, Sorting](#pagination-filtering-sorting)
- [Errors and Problem Details](#errors-and-problem-details)
- [Authentication and Authorization](#authentication-and-authorization)
- [JWT and Session Strategy](#jwt-and-session-strategy)
- [Response Envelopes](#response-envelopes)
- [OpenAPI / Swagger](#openapi--swagger)
- [Endpoint Grouping](#endpoint-grouping)
- [Async and Cancellation](#async-and-cancellation)
- [Logging and Tracing](#logging-and-tracing)
- [Do Not Implement Yet](#do-not-implement-yet)

## Current API Status

The API project now has a small implemented foundation:

```csharp
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddApiProblemDetails();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddControllers(...);
app.UseExceptionHandler();
app.UseSwaggerDocumentation();
app.MapControllers();
```

Implemented API surface currently includes reviewed vertical slices:

```text
Catalog:
GET/POST       /api/v1/marketplaces
GET/PUT/DELETE /api/v1/marketplaces/{id}
GET/POST       /api/v1/brands
GET/PUT/DELETE /api/v1/brands/{id}
GET/POST       /api/v1/categories
GET/PUT/DELETE /api/v1/categories/{id}
GET/POST       /api/v1/warehouses
GET/PUT/DELETE /api/v1/warehouses/{id}

Product:
GET/POST       /api/v1/products
GET/PUT/DELETE /api/v1/products/{id}
GET            /api/v1/products/{id}/history

Operations:
GET/POST       /api/v1/orders
GET/PUT/DELETE /api/v1/orders/{id}
GET/POST       /api/v1/reviews
GET/PUT/DELETE /api/v1/reviews/{id}
GET/POST       /api/v1/review-replies
GET/PUT/DELETE /api/v1/review-replies/{id}
GET/POST       /api/v1/logistics
GET/PUT/DELETE /api/v1/logistics/{id}
```

DTOs, validators, shared pagination/result models, ProblemDetails, and Development-only Swagger exist for implemented slices. Auth/JWT/workspace authorization are not implemented yet.

Database runtime notes:

- `ApplicationDbContext` is registered from `ConnectionStrings:Postgres`.
- Missing connection string should fail fast during startup.
- Migrations must not be applied automatically on API startup.

## REST Conventions

- Use resource-oriented routes.
- Use nouns, not verbs.
- Prefer plural resources.
- Use kebab-case or lowercase route segments consistently.
- Use HTTP methods according to semantics:
  - `GET` for reads;
  - `POST` for creation/commands;
  - `PUT` for full replacement;
  - `PATCH` for partial updates;
  - `DELETE` for deletion/archival.

Example route groups:

```text
/api/v1/products
/api/v1/products/{productId}/images
/api/v1/campaigns
/api/v1/recommendations
/api/v1/workspaces/{workspaceId}/expenses
```

## Versioning

Preferred initial strategy:

- URL versioning: `/api/v1/...`.
- Keep `v1` stable once clients exist.
- Breaking changes require a new version.

Do not introduce versioning libraries until API surface exists.

## DTO Policy

- Do not expose Domain entities directly from API.
- Use request and response DTOs.
- DTOs live outside Domain.
- DTOs should use API-friendly naming and types.
- Convert JSONB fields deliberately:
  - either `JsonElement`;
  - or typed DTO once schema is stable.

## Validation

- Use FluentValidation for request DTO validation.
- Keep entity constructors for local domain invariants.
- Do not use DataAnnotations on Domain entities.
- Validation failures should return `400 Bad Request` with problem details.

## Pagination, Filtering, Sorting

For list endpoints:

```text
GET /api/v1/products?page=1&pageSize=50&sort=-dateUpdate&filter[status]=Active
```

Current implemented list query shape for `Marketplaces`:

```text
GET /api/v1/marketplaces?page=1&pageSize=50&sort=-dateUpdate&search=wildberries
```

Guidelines:

- Default `pageSize`: 50.
- Maximum `pageSize`: 200 unless justified.
- Sorting fields must be allowlisted.
- Filtering fields must be allowlisted.
- Return pagination metadata.
- Current shared query model is `ListQuery` with `page`, `pageSize`, `sort`, and `search` for catalog/reference resources.
- Broader resources use dedicated query DTOs when filters differ, for example `ProductListQuery`, `OrderListQuery`, `ReviewListQuery`, `ReviewReplyListQuery`, and `LogisticListQuery`.
- Current shared list response is `PagedResponse<T>`.

## Errors and Problem Details

Use RFC 7807-style problem details.

Map domain errors:

| Domain error type | HTTP status |
|---|---:|
| Validation | 400 |
| NotFound | 404 |
| Conflict | 409 |
| Failure | 500 |

Expected shape:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Validation failed",
  "status": 400,
  "errors": {
    "skuSeller": ["skuSeller is required"]
  }
}
```

## Authentication and Authorization

Future approach:

- Use JWT access tokens for API requests.
- Store sessions in `Sessions`.
- Use role and permission entities for authorization.
- Workspace membership should be checked through `Users_Workspaces`.

Authorization must account for:

- global role;
- workspace role;
- permissions;
- resource ownership/workspace.

## JWT and Session Strategy

Planned fields already exist:

- `Sessions.token`;
- `Sessions.date_create`;
- `Sessions.date_refreshed`;
- `Sessions.date_expires`;
- `Sessions.status`.

Guidelines:

- Store refresh/session tokens securely.
- Do not store plaintext passwords.
- `Users.password` should contain a password hash.
- Token uniqueness should be considered later, but no unique index exists now because it was not documented.

## Response Envelopes

Use plain DTOs for simple reads unless a consistent envelope is adopted.

For paged lists, use:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 50,
  "totalCount": 123
}
```

Avoid wrapping every response in generic `{ success, data, error }` unless there is a clear client requirement.

## OpenAPI / Swagger

When API endpoints are added:

- Enable OpenAPI/Swagger in Development only.
- Add operation summaries only where useful.
- Document auth requirements.
- Document error responses.
- Keep DTO examples realistic.

## Endpoint Grouping

Recommended groups:

- Catalog: marketplaces, brands, categories, warehouses.
- Products: products, media, product history.
- Operations: orders, logistics, reviews.
- Access: users, sessions, roles, permissions.
- Workspaces: workspaces, membership, expenses.
- Advertising: campaigns, metrics.
- Recommendations: recommendations and target links.

Current implemented groups:

- Marketplaces: `MarketplacesController`, `IMarketplaceService`, marketplace DTOs and validators.
- Catalog:
  - `BrandsController`, `IBrandService`;
  - `CategoriesController`, `ICategoryService`;
  - `WarehousesController`, `IWarehouseService`.
- Product:
  - `ProductsController`, `IProductService`;
  - `IProductHistoryService` for read-only product history.
- Operations:
  - `OrdersController`, `IOrderService`;
  - `ReviewsController`, `IReviewService`;
  - `ReviewRepliesController`, `IReviewReplyService`;
  - `LogisticsController`, `ILogisticService`.

## Async and Cancellation

- All I/O endpoints should be async.
- Accept `CancellationToken` in handlers/services.
- Pass cancellation tokens to EF Core calls.
- Do not call `Database.Migrate()` from request handlers or startup without a dedicated migration task.

Example:

```csharp
public async Task<IResult> GetProducts(ApplicationDbContext db, CancellationToken cancellationToken)
{
    var products = await db.Products.ToListAsync(cancellationToken);
    return Results.Ok(products);
}
```

## Logging and Tracing

Guidelines:

- Use structured logging.
- Do not log tokens, passwords, or sensitive credentials.
- Include correlation/request IDs when middleware is added.
- Log external integration failures with enough context to diagnose.
- Use metrics/traces later for parser, ML, and marketplace API calls.

## Do Not Implement Yet

Do not generate auth middleware, JWT/session flows, automatic migration/seed startup code, generic CRUD abstractions, repository wrappers, or CRUD endpoints for remaining entities unless explicitly requested.
