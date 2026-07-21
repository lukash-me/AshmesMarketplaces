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
builder.Services.AddAuthSecurity(builder.Configuration);
builder.Services.AddSwaggerDocumentation();
builder.Services.AddControllers(...);
app.UseExceptionHandler();
app.UseSwaggerDocumentation();
app.UseAuthentication();
app.UseAuthorization();
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

Advertising:
GET/POST       /api/v1/campaigns
GET/PUT/DELETE /api/v1/campaigns/{id}
GET/POST       /api/v1/campaign-metrics
GET/PUT/DELETE /api/v1/campaign-metrics/{id}

Finance:
GET/POST       /api/v1/expense-categories
GET/PUT/DELETE /api/v1/expense-categories/{id}
GET/POST       /api/v1/expenses
GET/PUT/DELETE /api/v1/expenses/{id}

Users / Workspaces:
GET/POST       /api/v1/users
GET/PUT/DELETE /api/v1/users/{id}
GET/POST       /api/v1/workspaces
GET/PUT/DELETE /api/v1/workspaces/{id}
GET/POST       /api/v1/user-workspaces
GET            /api/v1/user-workspaces/{idUser}/{idWorkspace}
PUT/DELETE     /api/v1/user-workspaces/{idUser}/{idWorkspace}

Access:
GET/POST       /api/v1/roles
GET/PUT/DELETE /api/v1/roles/{id}
GET/POST       /api/v1/permission-categories
GET/PUT/DELETE /api/v1/permission-categories/{id}
GET/POST       /api/v1/permissions
GET/PUT/DELETE /api/v1/permissions/{id}
GET/POST       /api/v1/role-permissions
GET/DELETE     /api/v1/role-permissions/{idRole}/{idPermission}
GET/POST       /api/v1/role-subroles
GET/DELETE     /api/v1/role-subroles/{idRole}/{idSubrole}

Rules:
GET/POST       /api/v1/rules
GET/PUT/DELETE /api/v1/rules/{id}
GET/POST       /api/v1/rule-sets
GET/PUT/DELETE /api/v1/rule-sets/{id}
GET/POST       /api/v1/rule-set-rules
GET/DELETE     /api/v1/rule-set-rules/{idSet}/{idRule}

Recommendations:
GET/POST       /api/v1/recommendations
GET/PUT/DELETE /api/v1/recommendations/{id}
GET/POST       /api/v1/recommendation-products
GET/DELETE     /api/v1/recommendation-products/{idRecommendation}/{idProduct}
GET/POST       /api/v1/recommendation-categories
GET/DELETE     /api/v1/recommendation-categories/{idRecommendation}/{idCategory}

Auth:
POST           /api/v1/auth/login
POST           /api/v1/auth/refresh
POST           /api/v1/auth/logout
GET            /api/v1/auth/me
```

DTOs, validators, shared pagination/result models, ProblemDetails, Development-only Swagger, CRUD APIs, Auth/Security Foundation, Development Seed Foundation, and Frontend Foundation v2 exist for implemented slices. Existing CRUD APIs remain anonymous in the first auth stage for backward compatibility. Workspace authorization and full permission policy enforcement are not implemented yet.

Frontend Visual Redesign Stage 1, local dev startup scripts, and theme token hardening are implemented in commit `b18cd90 Add local dev startup scripts and harden theme tokens`. Products Polish Stage 1 is implemented as a frontend-only stage over existing Products APIs. These changes did not change API contracts, controllers, DTOs, auth endpoints, product query behavior, or backend schema.

Products frontend note:

- Products list continues to use `GET /api/v1/products` and existing query params.
- Product detail drawer uses existing `GET /api/v1/products/{id}`.
- No new Product media mutation, analytics, heat, demand, growth, or recommendation API contract exists.
- Frontend `ProductStatus` display must align with backend enum values: `Draft=0`, `Pending=1`, `Active=2`, `Rejected=3`, `Blocked=4`, `Archived=5`, `OutOfStock=6`, `Disappeared=7`.
- Current heat/signal UI is derived only from `status` and `dateUpdated`; it must not be documented or sold as real analytics.

Database runtime notes:

- `ApplicationDbContext` is registered from `ConnectionStrings:Postgres`.
- Missing connection string should fail fast during startup.
- Migrations must not be applied automatically on API startup.

Local dev startup:

- `scripts/dev/start-dev.ps1` and `scripts/dev/start-dev.sh` start PostgreSQL + pgAdmin, backend API, and frontend Vite.
- Default URLs:
  - API: `http://localhost:5019`;
  - Swagger: `http://localhost:5019/swagger`;
  - Frontend: `http://localhost:5173`;
  - pgAdmin: `http://localhost:5050`.
- Startup scripts do not apply migrations automatically.
- Startup scripts do not enable development seed automatically.
- Stop scripts may stop docker services only when explicitly requested and do not delete volumes.

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
- Do not return `User.Password` from response DTOs. Current user create/update requests accept plaintext `Password` and persist only a password hash using ASP.NET Core `PasswordHasher`.

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
- Broader resources use dedicated query DTOs when filters differ, for example `ProductListQuery`, `OrderListQuery`, `ReviewListQuery`, `ReviewReplyListQuery`, `LogisticListQuery`, `RolePermissionListQuery`, `RuleSetRuleListQuery`, and `RecommendationListQuery`.
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

Current authentication:

- Auth endpoints are implemented under `/api/v1/auth`.
- JWT access tokens are used for authenticated API requests.
- Opaque refresh tokens are returned to clients and stored only as hashes in `Sessions.token`.
- `Sessions` is used for refresh/session storage.
- `Users.password` stores password hashes generated by ASP.NET Core `PasswordHasher`.
- Existing CRUD APIs currently remain anonymous.
- Frontend Foundation v2 uses the existing auth endpoints for login, logout, refresh, `/auth/me`, and protected routes.
- No public registration endpoint exists.

Development seed:

- Development Seed Foundation is implemented under `AshmesMarketplaces.API/DevelopmentSeed`.
- It runs only in Development when `Seed:EnableDevelopmentSeed=true`.
- Default Development config keeps `Seed:EnableDevelopmentSeed=false`.
- It creates roles `Admin`, `Manager`, `Analyst`, `Viewer`, deterministic local accounts, one workspace, catalog records, products, and minimal orders/reviews/campaigns/expenses.
- Dev accounts:
  - `admin@ashmes.local` / `Admin123!`;
  - `manager@ashmes.local` / `Manager123!`;
  - `analyst@ashmes.local` / `Analyst123!`;
  - `viewer@ashmes.local` / `Viewer123!`.
- Seed uses `IPasswordHashService`/`PasswordHasher`; plaintext passwords are not stored in the database.
- Seed does not call `Database.Migrate()` and did not require migrations after `InitialCreate`.

Future authorization hardening:

- Use role and permission entities for authorization policies.
- Workspace membership should be checked through `Users_Workspaces`.

Authorization must account for:

- global role;
- workspace role;
- permissions;
- resource ownership/workspace.

## JWT and Session Strategy

Implemented fields:

- `Sessions.token`;
- `Sessions.date_create`;
- `Sessions.date_refreshed`;
- `Sessions.date_expires`;
- `Sessions.status`.

Current strategy:

- Access token: signed JWT.
- Refresh token: opaque random token returned to client; only a SHA-256 hash is stored.
- Refresh rotates the refresh token.
- Logout revokes the current session.
- `Sessions.status` auth semantics:
  - `1` active;
  - `2` revoked.
- Store refresh/session tokens securely.
- Do not store plaintext passwords.
- `Users.password` contains a password hash.
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
- Swagger includes a Bearer auth security scheme and authorize button in Development.
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
- Advertising:
  - `CampaignsController`, `ICampaignService`;
  - `CampaignMetricsController`, `ICampaignMetricService`.
- Finance:
  - `ExpenseCategoriesController`, `IExpenseCategoryService`;
  - `ExpensesController`, `IExpenseService`.
- Users / Workspaces:
  - `UsersController`, `IUserService`;
  - `WorkspacesController`, `IWorkspaceService`;
  - `UserWorkspacesController`, `IUserWorkspaceService`.
- Access:
  - `RolesController`, `IRoleService`;
  - `PermissionCategoriesController`, `IPermissionCategoryService`;
  - `PermissionsController`, `IPermissionService`;
  - `RolePermissionsController`, `IRolePermissionService`;
  - `RoleSubrolesController`, `IRoleSubroleService`.
- Rules:
  - `RulesController`, `IRuleService`;
  - `RuleSetsController`, `IRuleSetService`;
  - `RuleSetRulesController`, `IRuleSetRuleService`.
- Recommendations:
  - `RecommendationsController`, `IRecommendationService`;
  - `RecommendationProductsController`, `IRecommendationProductService`;
  - `RecommendationCategoriesController`, `IRecommendationCategoryService`.
- Auth:
  - `AuthController`, `IAuthService`;
  - `IPasswordHashService`, `IAccessTokenService`, `IRefreshTokenService`;
  - `ICurrentUser`.

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

Do not generate automatic migrations, generic CRUD abstractions, repository wrappers, standalone `Sessions` CRUD API, full permission matrix, complex RBAC engine, OAuth/external providers, 2FA, production rate limiting/lockout, public registration, ML inference, background jobs, parser integration, or production seed behavior unless explicitly requested. Frontend Foundation v2, Frontend Visual Redesign Stage 1, and Products Polish Stage 1 are already implemented; further frontend work should extend the existing feature-oriented structure and semantic theme tokens without changing API contracts unless separately requested.
