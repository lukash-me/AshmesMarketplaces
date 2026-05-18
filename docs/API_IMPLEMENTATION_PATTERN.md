# API Implementation Pattern

This document is the compact template for adding CRUD resources after the implemented Catalog, Product, and Operations slices.

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

Do not introduce a shared `CatalogService`, generic CRUD service, generic controller, repository wrapper, or CQRS handler-per-operation for basic CRUD.

## Service Rules

- Application services may use `ApplicationDbContext` directly.
- Read queries use `AsNoTracking()`.
- All database calls are async and accept `CancellationToken`.
- Services return DTOs through `ServiceResult<T>` / `ServiceResult`.
- Mapping is manual.
- Sorting and filtering are allowlisted per service.
- Catalog/reference resources may use shared `ListQuery`.
- Product and Operations resources use dedicated query DTOs when filters differ.
- API contracts expose `ProductId` as `Guid`; Application services convert to/from the `ProductId` value object.
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

Paged list endpoints use `ListQuery`:

```text
page=1&pageSize=50&sort=-dateUpdate&search=value
```

For resources with richer filters, use a dedicated query DTO instead of expanding shared `ListQuery` for all controllers.

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
5. Do not create migrations unless the task explicitly asks for schema changes.
