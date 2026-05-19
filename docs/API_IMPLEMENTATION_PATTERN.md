# API Implementation Pattern

This document is the compact template for adding backend resources after completed CRUD API coverage for Catalog, Product, Operations, Advertising, Finance, Users/Workspaces, Access, Rules, Recommendations, the implemented Auth/Security Foundation, implemented Development Seed Foundation, and implemented Frontend Foundation v2.

## Current Pattern

Use one vertical slice per resource:

- API controller in `AshmesMarketplaces.API/Controllers/V1`;
- DTOs in `AshmesMarketplaces.Application/<Resource>/Dtos`;
- validators in `AshmesMarketplaces.Application/<Resource>/Validators`;
- service interface and implementation in `AshmesMarketplaces.Application/<Resource>/Services`;
- registration in `ApplicationServiceCollectionExtensions`.

Use separate resource services for implemented resources:

- `IMarketplaceService`;
- `IBrandService`;
- `ICategoryService`;
- `IWarehouseService`.
- `IProductService`;
- `IProductHistoryService`;
- `IOrderService`;
- `IReviewService`;
- `IReviewReplyService`;
- `ILogisticService`.
- `ICampaignService`;
- `ICampaignMetricService`;
- `IExpenseService`;
- `IExpenseCategoryService`;
- `IUserService`;
- `IWorkspaceService`;
- `IUserWorkspaceService`.
- `IRoleService`;
- `IPermissionCategoryService`;
- `IPermissionService`;
- `IRolePermissionService`;
- `IRoleSubroleService`;
- `IRuleService`;
- `IRuleSetService`;
- `IRuleSetRuleService`;
- `IRecommendationService`;
- `IRecommendationProductService`;
- `IRecommendationCategoryService`.
- `IAuthService`.
- `DevelopmentSeeder` for Development-only local seed data.

Do not introduce a shared `CatalogService`, generic CRUD service, generic controller, repository wrapper, or CQRS handler-per-operation for basic CRUD.

## Service Rules

- Application services may use `ApplicationDbContext` directly.
- Read queries use `AsNoTracking()`.
- All database calls are async and accept `CancellationToken`.
- Services return DTOs through `ServiceResult<T>` / `ServiceResult`.
- Mapping is manual.
- Sorting and filtering are allowlisted per service.
- Catalog/reference resources may use shared `ListQuery`.
- Product, Operations, Advertising, Finance, Users, Workspaces, Access, Rules, Recommendations, and explicit join resources use dedicated query DTOs when filters differ.
- API contracts expose `ProductId` as `Guid`; Application services convert to/from the `ProductId` value object.
- Recommendation JSONB fields are exposed as `JsonElement?` in DTOs and converted to/from `JsonDocument?` inside services.
- `User.Password` is accepted in user create/update requests, persisted as a password hash through `PasswordHasher`, and never returned by user response DTOs.
- Auth uses JWT access tokens plus opaque refresh tokens; `Sessions` stores refresh-token hashes and session status.
- Existing CRUD APIs remain anonymous until a dedicated authorization-hardening stage changes endpoint access.
- Development seed uses existing Domain constructors/factories and `ApplicationDbContext`, runs only in Development when `Seed:EnableDevelopmentSeed=true`, and must not call migrations.
- Delete methods should stay behind `DeleteAsync(id)` to remain soft-delete-ready later, but current behavior is hard delete unless a dedicated soft-delete task changes the model.

## Endpoint Shape

Use resource-oriented routes under `/api/v1`.

Typical CRUD shape:

```text
GET    /api/v1/<resources>
GET    /api/v1/<resources>/{id}
POST   /api/v1/<resources>
PUT    /api/v1/<resources>/{id}
DELETE /api/v1/<resources>/{id}
```

Explicit join resources with composite keys use route keys instead of hidden EF many-to-many behavior:

```text
GET    /api/v1/user-workspaces
GET    /api/v1/user-workspaces/{idUser}/{idWorkspace}
POST   /api/v1/user-workspaces
PUT    /api/v1/user-workspaces/{idUser}/{idWorkspace}
DELETE /api/v1/user-workspaces/{idUser}/{idWorkspace}

GET    /api/v1/recommendation-products
GET    /api/v1/recommendation-products/{idRecommendation}/{idProduct}
POST   /api/v1/recommendation-products
DELETE /api/v1/recommendation-products/{idRecommendation}/{idProduct}
```

Auth endpoints use the dedicated auth route group:

```text
POST   /api/v1/auth/login
POST   /api/v1/auth/refresh
POST   /api/v1/auth/logout
GET    /api/v1/auth/me
```

Paged list endpoints use `ListQuery`:

```text
page=1&pageSize=50&sort=-dateUpdate&search=value
```

For resources with richer filters, use a dedicated query DTO instead of expanding shared `ListQuery` for all controllers.

Current dedicated query DTO examples include `ProductListQuery`, `OrderListQuery`, `ReviewListQuery`, `ReviewReplyListQuery`, `LogisticListQuery`, `CampaignListQuery`, `CampaignMetricListQuery`, `ExpenseListQuery`, `ExpenseCategoryListQuery`, `UserListQuery`, `WorkspaceListQuery`, `UserWorkspaceListQuery`, `RolePermissionListQuery`, `RuleSetRuleListQuery`, `RecommendationListQuery`, `RecommendationProductListQuery`, and `RecommendationCategoryListQuery`.

Responses:

- simple reads return plain response DTOs;
- list reads return `PagedResponse<T>`;
- errors return ProblemDetails;
- validation errors return `400 ValidationProblemDetails`;
- missing resources return `404`;
- FK/database conflicts return `409`.

Implemented Product-specific rule:

- `GET /api/v1/products/{id}/history` is read-only in the first Product API stage.
- Product create can accept initial image/video URL arrays, but standalone product media mutation endpoints are not implemented yet.

Implemented Users/Workspaces rule:

- `Users` CRUD is not login/authentication. It hashes incoming passwords and does not return passwords.
- `UserWorkspaces` exposes membership between existing `Users`, `Workspaces`, and `Roles`.

Implemented Auth/Security rule:

- `POST /api/v1/auth/login` verifies credentials, creates a `Sessions` row, and returns a JWT access token plus opaque refresh token.
- `POST /api/v1/auth/refresh` validates the session/refresh token hash and rotates the refresh token.
- `POST /api/v1/auth/logout` revokes the current session.
- `GET /api/v1/auth/me` returns the current user, direct workspace memberships, and direct permissions from the global role.
- `Sessions.status` uses `1` for active and `2` for revoked in the auth layer.
- `Sessions.token` stores only the refresh-token hash.

Implemented Development Seed rule:

- Seed code lives under `AshmesMarketplaces.API/DevelopmentSeed`.
- Seed is enabled only by `ASPNETCORE_ENVIRONMENT=Development` plus `Seed:EnableDevelopmentSeed=true`.
- Default Development config keeps seed disabled.
- Seed creates local roles, dev users, workspace, marketplace/catalog records, products, and minimal orders/reviews/campaigns/expenses.
- Seed users are `admin@ashmes.local`, `manager@ashmes.local`, `analyst@ashmes.local`, and `viewer@ashmes.local`.
- Seed uses `IPasswordHashService`/`PasswordHasher`; plaintext passwords are not stored in the database.
- Seed is lookup-idempotent and did not require migrations after `InitialCreate`.

Implemented Frontend Foundation rule:

- Frontend Foundation v2 lives under `Frontend/`.
- It uses Vue 3, TypeScript, Composition API, Vite, Vue Router, Pinia, Axios, Tailwind tokens/components.
- It is feature-oriented, not a backend mirror.
- It includes protected shell, auth flow, and Products vertical slice with real backend integration.
- Remaining frontend sections are placeholders until dedicated vertical slices are implemented.
- Frontend Visual Redesign Stage 1 is implemented with a dark obsidian marketplace intelligence direction.
- Shared UI primitives and shell components are backed by semantic theme tokens in `Frontend/src/styles/tokens.css`.
- Theme variables are scoped through `:root, [data-theme="obsidian"]`; no theme switcher UI exists yet.
- Products heat indicators are presentation-only scaffolding based on current product fields and must not be treated as real demand/growth/recommendation metrics.
- Products Polish Stage 1 is implemented:
  - the Products table is dense, clickable, keyboard-openable, and uses corrected backend `ProductStatus` labels;
  - Products filters use a compact toolbar, native status select, active chips, quick reset, and the existing URL query sync;
  - the product detail drawer is read-only and fetches existing `GET /api/v1/products/{id}`;
  - heat tiers are visual scaffolding derived only from existing `status` and `dateUpdated`;
  - no backend API contracts, route paths, auth behavior, migrations, chart libraries, UI libraries, or animation libraries were added.
- Frontend visual changes must not change API client behavior, auth flow, products query sync, route paths, or backend API contracts without a separate reviewed task.

Implemented Access/Rules/Recommendations rules:

- `RolePermissions`, `RoleSubroles`, `RuleSetRules`, `RecommendationProducts`, and `RecommendationCategories` are explicit join resources.
- Key-only join resources do not expose `PUT`; changing a pair is `DELETE` plus `POST`.
- `RecommendationProduct.IdProduct` is exposed as `Guid` and converted with `ProductId.Create(...)`.
- `Recommendations` CRUD is storage/API management only; it does not run ML inference or recommendation generation.

## Validation

Use FluentValidation for create/update/list request DTOs when validation is meaningful.

Validate:

- required strings;
- max lengths aligned with EF configuration/constants;
- valid URL shape where applicable;
- UTC `DateTime` for persisted timestamps;
- paging bounds.

Do not invent enum values. For undocumented enum fields represented as `int`, validate only broad numeric sanity unless real values are documented.

DTO validation does not replace Domain constructors/factories.

## Swagger

Swagger/OpenAPI is Development-only.

Swagger includes a Bearer auth security scheme and authorize button.

Controllers should include concise response metadata for:

- `200`;
- `201`;
- `204`;
- `400`;
- `404`;
- `409` when delete/update can conflict.

Do not add large generated API documentation to markdown. Swagger is the source for endpoint surface.

## Checklist

Before reporting completion:

1. Build:

   ```powershell
   dotnet build E:\AshmesMarketplaces\Backend\AshmesMarketplaces.sln
   ```

2. Required result:

   ```text
   0 warnings
   0 errors
   ```

3. Verify Swagger in Development.
4. Smoke-test CRUD against local PostgreSQL when the endpoint writes data.
5. For seed-related work, verify seed-enabled and seed-disabled startup plus repeated-run idempotency.
6. Do not create migrations unless the task explicitly asks for schema changes.
