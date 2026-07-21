# Entity and EF Core Conventions

## Table of Contents

- [Entity Naming](#entity-naming)
- [Table and Column Naming](#table-and-column-naming)
- [Folder Structure](#folder-structure)
- [Keys and Foreign Keys](#keys-and-foreign-keys)
- [Nullable Policy](#nullable-policy)
- [Decimal Policy](#decimal-policy)
- [JSONB Policy](#jsonb-policy)
- [Enum Policy](#enum-policy)
- [DateTime Policy](#datetime-policy)
- [Validation Policy](#validation-policy)
- [Delete Behavior Policy](#delete-behavior-policy)
- [Identifier Generation Policy](#identifier-generation-policy)
- [DbContext Conventions](#dbcontext-conventions)
- [Configuration Conventions](#configuration-conventions)
- [Build Cleanliness Policy](#build-cleanliness-policy)
- [Examples](#examples)

## Entity Naming

- Entity class names are PascalCase singular.
- Join entities are explicit classes.
- Entity names should describe domain concepts, not EF implementation details.

Examples:

- `Product`
- `ProductImage`
- `BrandMarketplace`
- `UserWorkspace`
- `RecommendationProduct`

## Table and Column Naming

- Table names follow project ER/documentation names.
- Column names are explicit `snake_case`.
- Do not rely on EF implicit column naming.

Examples:

```csharp
builder.ToTable("Products");

builder.Property(x => x.SkuSeller)
    .IsRequired()
    .HasMaxLength(Constants.EXTERNAL_ID_MAX_LENGTH)
    .HasColumnName("sku_seller");
```

## Folder Structure

Domain entities are grouped by subsystem:

```text
AshmesMarketplaces.Domain/Entities
├── Access
├── Advertising
├── Finance
├── Logistics
├── Marketplaces
├── Orders
├── Product
├── Recommendations
├── Reviews
├── Rules
├── Users
└── Workspaces
```

DataAccess configurations live in:

```text
AshmesMarketplaces.DataAccess/Configurations
```

Use one configuration file per entity.

## Keys and Foreign Keys

- Most entities use `Guid Id`.
- `Session` currently uses `int Id`, matching the implemented entity.
- Product aggregate uses `ProductId` as a readonly value object with value equality.
- EF configurations convert `ProductId` to/from `Guid`.
- Join tables use composite primary keys.
- Guid and `ProductId` primary keys are application-generated and configured with `.ValueGeneratedNever()`.
- `Session.Id` is the only current database-generated integer identity exception.

Example:

```csharp
builder.HasKey(x => new { x.IdRecommendation, x.IdProduct });

builder.Property(x => x.IdProduct)
    .HasConversion(id => id.Value, value => ProductId.Create(value))
    .ValueGeneratedNever()
    .HasColumnName("id_product");
```

## Nullable Policy

- Required documentation fields are non-nullable.
- ER fields marked `NULL` are nullable.
- Optional relationship FKs are nullable.
- Nullable reference types are enabled.
- Domain constructors validate required strings using `string.IsNullOrWhiteSpace`.

## Decimal Policy

- Money fields use `decimal`.
- Money EF precision is `18,2`.
- Recommendation score uses `decimal(18,6)`.
- Counts use `int`.

Examples:

```csharp
builder.Property(x => x.Budget)
    .HasPrecision(18, 2)
    .HasColumnName("budget");

builder.Property(x => x.Score)
    .HasPrecision(18, 6)
    .HasColumnName("score");
```

## JSONB Policy

- Dynamic JSON fields use `JsonDocument?`.
- EF maps them using `.HasColumnType("jsonb")`.
- Entities containing `JsonDocument?` implement `IDisposable`.
- Do not use `string` for JSON unless explicitly justified.
- Do not introduce strong JSON value objects until schema is stable.

Current JSONB fields:

- `Product.Characteristics`
- `Campaign.TimeToImpression`
- `Recommendation.Explanation`
- `Recommendation.Snapshot`

## Enum Policy

- If documentation says `enum` but values are not defined, use `int`.
- Add a comment beside the property.
- Do not invent enum members.
- `ProductStatus` is the only existing concrete enum.

Example:

```csharp
public int Status { get; private set; } // enum по документации, значения не определены
```

## DateTime Policy

- Persisted `DateTime` and `DateTime?` values must be UTC.
- Domain constructors and factories validate persisted date/time inputs with `DateTimeUtc`.
- Do not use local or unspecified `DateTime` values for persistence.
- The PostgreSQL runtime model is prepared for Npgsql `timestamptz`.

Example:

```csharp
DateTimeUtc.EnsureUtc(dateCreate, nameof(dateCreate));
DateTimeUtc.EnsureUtc(dateUpdate, nameof(dateUpdate));
```

## Validation Policy

Domain constructors perform obvious local validation:

- required IDs are not empty;
- required strings are not whitespace;
- money and counts are non-negative;
- amount-like values are positive when appropriate;
- end dates are not before start dates;
- update dates are not before create dates;
- persisted date/time values are UTC.

API/request validation is implemented for current Catalog, Product, Operations, Advertising, Finance, Users/Workspaces, Access, Rules, Recommendations, and Auth/Security slices and should use FluentValidation for future request DTOs.

Current API/Application foundation uses FluentValidation for request DTOs. DTO validation does not replace Domain invariants: entity constructors/factories remain the final guard for persisted domain state.

Recommendation API DTOs expose dynamic JSONB fields as `JsonElement?`; Application services convert them to `JsonDocument?` before persistence and clone `JsonElement` values for responses.

Auth/Security Foundation keeps existing entity and EF conventions:

- `Users.password` stores PasswordHasher output, not plaintext.
- `Sessions.token` stores only an opaque refresh-token hash, not the raw refresh token.
- Auth uses existing `Sessions.status` values `1` active and `2` revoked without changing the schema.
- Existing CRUD APIs remain anonymous until a later authorization-hardening stage.

Development Seed Foundation also keeps existing entity and EF conventions:

- seed code uses existing Domain constructors/factories and `ApplicationDbContext`;
- seed runs only in Development when `Seed:EnableDevelopmentSeed=true`;
- seed uses `IPasswordHashService`/`PasswordHasher` for dev account password hashes;
- seed uses lookup-based idempotency on stable names, logins, SKUs, and external IDs because most seed lookup fields are not backed by unique indexes;
- seed must not add EF configurations, schema objects, migrations, startup migration calls, or public registration endpoints.

Frontend Visual Redesign Stage 1, Products Polish Stage 1, local dev startup scripts, and theme token hardening did not change Domain entities, DataAccess configurations, EF conventions, migrations, JSONB mappings, key policies, or delete behavior. Products Polish Stage 1 only changed frontend display/interaction over existing Products list/detail API responses.

API/Application changes must not change entity conventions. Do not add DataAnnotations to Domain entities for API validation.

## Delete Behavior Policy

- Product media cascades from `Product`:
  - `ProductImage`;
  - `ProductVideo`.
- Analytical/history data generally restricts deletion:
  - `ProductHistory`;
  - `Order`;
  - `Review` and `ReviewReply`;
  - `Logistic`;
  - `Campaign` and `CampaignMetric`;
  - `Expense`;
  - recommendation links from `Product`.
- Reference data generally restricts deletion.
- Cleanup joins may cascade from their owning cleanup side when they are not analytical history.

Examples:

```csharp
builder.HasOne<Product>()
    .WithMany()
    .HasForeignKey(x => x.IdProduct)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne<Marketplace>()
    .WithMany()
    .HasForeignKey(x => x.IdMp)
    .OnDelete(DeleteBehavior.Restrict);
```

## Identifier Generation Policy

- Entity constructors/factories create Guid identifiers with `Guid.NewGuid()` unless the entity uses a documented non-Guid key.
- EF configurations for Guid/ProductId primary keys must use `.ValueGeneratedNever()`.
- Do not switch to database-generated Guid values without a dedicated architecture decision.
- `Session.Id` remains an `int` key generated by the database/provider convention.

## DbContext Conventions

`ApplicationDbContext`:

- inherits `DbContext`;
- accepts `DbContextOptions<ApplicationDbContext>`;
- exposes DbSet properties for implemented entities;
- applies configurations from assembly;
- is registered in API using PostgreSQL/Npgsql and `ConnectionStrings:Postgres`;
- has a design-time factory in DataAccess for EF tooling.

Pattern:

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

## Configuration Conventions

Each configuration:

- implements `IEntityTypeConfiguration<T>`;
- sets `.ToTable(...)` first;
- configures key(s);
- configures every column explicitly;
- configures FK relationships and delete behavior explicitly;
- avoids unique indexes unless documented;
- configures Guid/ProductId primary keys with `.ValueGeneratedNever()` except documented identity keys.

## Build Cleanliness Policy

Every implementation stage must end with:

```powershell
dotnet build E:\AshmesMarketplaces\Backend\AshmesMarketplaces.sln
```

Expected result:

- `0 warnings`
- `0 errors`

Warnings are not acceptable as a steady state.

## Examples

### Entity Pattern

```csharp
public class CampaignMetric
{
    private CampaignMetric() { }

    public CampaignMetric(Guid idCampaign, int? impressionAmount, int? clicksAmount, decimal? costDay, DateTime date)
    {
        if (idCampaign == Guid.Empty)
            throw new ArgumentException("Campaign id is required", nameof(idCampaign));

        if (impressionAmount is < 0)
            throw new ArgumentOutOfRangeException(nameof(impressionAmount), "ImpressionAmount must be non-negative");

        DateTimeUtc.EnsureUtc(date, nameof(date));

        Id = Guid.NewGuid();
        IdCampaign = idCampaign;
        ImpressionAmount = impressionAmount;
        ClicksAmount = clicksAmount;
        CostDay = costDay;
        Date = date;
    }

    public Guid Id { get; private set; }
    public Guid IdCampaign { get; private set; }
    public int? ImpressionAmount { get; private set; }
    public int? ClicksAmount { get; private set; }
    public decimal? CostDay { get; private set; }
    public DateTime Date { get; private set; }
}
```

### Configuration Pattern

```csharp
public class CampaignMetricConfiguration : IEntityTypeConfiguration<CampaignMetric>
{
    public void Configure(EntityTypeBuilder<CampaignMetric> builder)
    {
        builder.ToTable("Metrics_Campaign");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(x => x.CostDay)
            .HasPrecision(18, 2)
            .HasColumnName("cost_day");

        builder.HasOne<Campaign>()
            .WithMany()
            .HasForeignKey(x => x.IdCampaign)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```
