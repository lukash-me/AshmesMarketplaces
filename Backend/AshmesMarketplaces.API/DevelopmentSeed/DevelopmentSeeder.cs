using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Advertising;
using AshmesMarketplaces.Domain.Entities.Finance;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using AshmesMarketplaces.Domain.Entities.Orders;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.Entities.Reviews;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using AshmesMarketplaces.Domain.IDs;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.API.DevelopmentSeed;

public sealed class DevelopmentSeeder
{
    private const string SeedBrandName = "Ashmes Local Demo Brand";
    private const string SeedCategoryName = "Home Organization";
    private const string SeedMarketplaceName = "Wildberries Local";
    private const string SeedWarehouseCode = "ASH-DEV-WH-01";
    private const string SeedWorkspaceName = "Ashmes Local Workspace";
    private const string SeedExpenseCategoryName = "Marketplace Operations";

    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ILogger<DevelopmentSeeder> _logger;

    public DevelopmentSeeder(
        ApplicationDbContext dbContext,
        IPasswordHashService passwordHashService,
        ILogger<DevelopmentSeeder> logger)
    {
        _dbContext = dbContext;
        _passwordHashService = passwordHashService;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Development seed is enabled. This must never be enabled in Production.");

        var roles = await SeedRolesAsync(cancellationToken);
        var users = await SeedUsersAsync(roles, cancellationToken);
        var brand = await GetOrCreateBrandAsync(cancellationToken);
        var category = await GetOrCreateCategoryAsync(cancellationToken);
        var marketplace = await GetOrCreateMarketplaceAsync(cancellationToken);
        var warehouse = await GetOrCreateWarehouseAsync(marketplace.Id, cancellationToken);
        var workspace = await GetOrCreateWorkspaceAsync(brand.Id, cancellationToken);

        await SeedUserWorkspacesAsync(users, roles, workspace.Id, cancellationToken);

        var products = await SeedProductsAsync(
            marketplace.Id,
            brand.Id,
            category.Id,
            workspace.Id,
            users["Admin"].Id,
            cancellationToken);

        await SeedOperationsAsync(products, warehouse.Id, workspace.Id, users["Admin"].Id, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        LogCredentials();
        _logger.LogInformation("Development seed completed.");
    }

    private async Task<Dictionary<string, Role>> SeedRolesAsync(CancellationToken cancellationToken)
    {
        var roles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);

        foreach (var seedRole in DevelopmentSeedData.Roles)
        {
            var role = await _dbContext.Roles
                .FirstOrDefaultAsync(x => x.Name == seedRole.Name, cancellationToken);

            if (role is null)
            {
                role = new Role(
                    seedRole.Name,
                    seedRole.Description,
                    DevelopmentSeedData.SeedDate,
                    DevelopmentSeedData.SeedDate);

                _dbContext.Roles.Add(role);
            }

            roles[seedRole.Name] = role;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return roles;
    }

    private async Task<Dictionary<string, User>> SeedUsersAsync(
        IReadOnlyDictionary<string, Role> roles,
        CancellationToken cancellationToken)
    {
        var users = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

        foreach (var seedUser in DevelopmentSeedData.Users)
        {
            var role = roles[seedUser.RoleName];
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Login == seedUser.Login, cancellationToken);
            var passwordHash = _passwordHashService.HashPassword(seedUser.Password);

            if (user is null)
            {
                user = new User(
                    role.Id,
                    seedUser.Login,
                    passwordHash,
                    seedUser.Login,
                    seedUser.Phone,
                    status: 1,
                    DevelopmentSeedData.SeedDate,
                    DevelopmentSeedData.SeedDate);

                _dbContext.Users.Add(user);
            }
            else
            {
                var entry = _dbContext.Entry(user);
                entry.Property(x => x.IdRole).CurrentValue = role.Id;
                entry.Property(x => x.Status).CurrentValue = 1;
                entry.Property(x => x.Email).CurrentValue = seedUser.Login;
                entry.Property(x => x.Phone).CurrentValue = seedUser.Phone;

                if (!_passwordHashService.VerifyPassword(user.Password, seedUser.Password))
                    entry.Property(x => x.Password).CurrentValue = passwordHash;
            }

            users[seedUser.RoleName] = user;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return users;
    }

    private async Task<Brand> GetOrCreateBrandAsync(CancellationToken cancellationToken)
    {
        var brand = await _dbContext.Brands
            .FirstOrDefaultAsync(x => x.Name == SeedBrandName, cancellationToken);

        if (brand is not null)
            return brand;

        brand = new Brand(
            SeedBrandName,
            isVerified: true,
            country: "RU",
            manufacturer: "Ashmes Local",
            salesAmount: 250,
            rateRedemption: 91,
            level: 3,
            type: 1,
            DevelopmentSeedData.SeedDate,
            DevelopmentSeedData.SeedDate);

        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return brand;
    }

    private async Task<Category> GetOrCreateCategoryAsync(CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Name == SeedCategoryName, cancellationToken);

        if (category is not null)
            return category;

        category = new Category(
            idParentCategory: null,
            idOnMp: "ashmes-local-category",
            SeedCategoryName,
            level: 1,
            isActive: true,
            DevelopmentSeedData.SeedDate);

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return category;
    }

    private async Task<Marketplace> GetOrCreateMarketplaceAsync(CancellationToken cancellationToken)
    {
        var marketplace = await _dbContext.Marketplaces
            .FirstOrDefaultAsync(x => x.Name == SeedMarketplaceName, cancellationToken);

        if (marketplace is not null)
            return marketplace;

        marketplace = new Marketplace(
            SeedMarketplaceName,
            "https://marketplace.local/api",
            "dev-v1",
            "RUB",
            "RU",
            typeCommission: 1,
            schemeDelivery: "FBO",
            DevelopmentSeedData.SeedDate);

        _dbContext.Marketplaces.Add(marketplace);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return marketplace;
    }

    private async Task<Warehouse> GetOrCreateWarehouseAsync(Guid marketplaceId, CancellationToken cancellationToken)
    {
        var warehouse = await _dbContext.Warehouses
            .FirstOrDefaultAsync(x => x.Code == SeedWarehouseCode, cancellationToken);

        if (warehouse is not null)
            return warehouse;

        warehouse = new Warehouse(
            marketplaceId,
            "Ashmes Local Warehouse",
            SeedWarehouseCode,
            "Moscow",
            "Moscow",
            "Local development address",
            "55.7558",
            "37.6173",
            isActive: true,
            type: 1,
            DevelopmentSeedData.SeedDate,
            DevelopmentSeedData.SeedDate);

        _dbContext.Warehouses.Add(warehouse);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return warehouse;
    }

    private async Task<Workspace> GetOrCreateWorkspaceAsync(Guid brandId, CancellationToken cancellationToken)
    {
        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(x => x.Name == SeedWorkspaceName, cancellationToken);

        if (workspace is not null)
            return workspace;

        workspace = new Workspace(
            brandId,
            SeedWorkspaceName,
            "Local development workspace for frontend and API smoke testing.",
            urlInvite: null,
            status: 1,
            DevelopmentSeedData.SeedDate,
            DevelopmentSeedData.SeedDate);

        _dbContext.Workspaces.Add(workspace);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return workspace;
    }

    private async Task SeedUserWorkspacesAsync(
        IReadOnlyDictionary<string, User> users,
        IReadOnlyDictionary<string, Role> roles,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        foreach (var seedUser in DevelopmentSeedData.Users)
        {
            var user = users[seedUser.RoleName];
            var role = roles[seedUser.RoleName];
            var exists = await _dbContext.UserWorkspaces.AnyAsync(
                x => x.IdUser == user.Id && x.IdWorkspace == workspaceId,
                cancellationToken);

            if (!exists)
                _dbContext.UserWorkspaces.Add(new UserWorkspace(user.Id, workspaceId, role.Id));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Product>> SeedProductsAsync(
        Guid marketplaceId,
        Guid brandId,
        Guid categoryId,
        Guid workspaceId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var products = new List<Product>();

        foreach (var seedProduct in DevelopmentSeedData.Products)
        {
            var product = await _dbContext.Products
                .Include(x => x.Images)
                .FirstOrDefaultAsync(x => x.SkuSeller == seedProduct.SkuSeller, cancellationToken);

            if (product is null)
            {
                var result = Product.Create(
                    ProductId.Create(CreateStableGuid(seedProduct.SkuSeller)),
                    idSetPrice: null,
                    workspaceId,
                    brandId,
                    marketplaceId,
                    adminUserId,
                    categoryId,
                    idOnMp: seedProduct.SkuProduct,
                    seedProduct.SkuProduct,
                    seedProduct.SkuSeller,
                    seedProduct.Name,
                    description: "Seeded local product for frontend testing.",
                    characteristicsJson: """{"seed":true,"segment":"local-demo"}""",
                    seedProduct.Barcode,
                    seedProduct.Commission,
                    (ProductStatus)seedProduct.Status,
                    DevelopmentSeedData.SeedDate,
                    DevelopmentSeedData.SeedDate);

                if (result.IsFailure)
                    throw new InvalidOperationException($"Seed product '{seedProduct.SkuSeller}' is invalid: {result.Error.Message}");

                product = result.Value;
                product.AddImage($"https://assets.local/{seedProduct.SkuSeller.ToLowerInvariant()}.jpg");
                _dbContext.Products.Add(product);
            }

            products.Add(product);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return products;
    }

    private async Task SeedOperationsAsync(
        IReadOnlyList<Product> products,
        Guid warehouseId,
        Guid workspaceId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var expenseCategory = await GetOrCreateExpenseCategoryAsync(cancellationToken);

        for (var index = 0; index < products.Count; index++)
        {
            var product = products[index];
            var productId = product.Id;
            var seedDate = DevelopmentSeedData.SeedDate.AddDays(index);

            if (!await _dbContext.Orders.AnyAsync(x => x.IdProduct == productId && x.DateOpened == seedDate, cancellationToken))
            {
                _dbContext.Orders.Add(new Order(
                    productId,
                    price: 1990 + index * 350,
                    discount: 150,
                    amount: 2 + index,
                    locationSource: "Seed local origin",
                    locationDestination: "Seed local destination",
                    status: 1,
                    dateDelivered: seedDate.AddDays(2),
                    dateOpened: seedDate,
                    dateClosed: seedDate.AddDays(3),
                    dateUpdate: seedDate.AddDays(3)));
            }

            var reviewExternalId = $"seed-review-{index + 1}";
            if (!await _dbContext.Reviews.AnyAsync(x => x.IdOnMp == reviewExternalId, cancellationToken))
            {
                _dbContext.Reviews.Add(new Review(
                    productId,
                    reviewExternalId,
                    rating: 4 + index % 2,
                    text: "Seeded review for local UI testing.",
                    isReplied: index % 2 == 0,
                    dateCreate: seedDate.AddDays(1),
                    dateReply: index % 2 == 0 ? seedDate.AddDays(2) : null));
            }

            var campaignName = $"Seed Campaign {index + 1}";
            if (!await _dbContext.Campaigns.AnyAsync(x => x.Name == campaignName, cancellationToken))
            {
                _dbContext.Campaigns.Add(new Campaign(
                    productId,
                    idSetCampaign: null,
                    campaignName,
                    budget: 5000 + index * 1500,
                    region: "RU",
                    status: 1,
                    type: 1,
                    description: "Seeded campaign for local UI testing.",
                    timeToImpression: null,
                    dateStart: seedDate,
                    dateEnd: seedDate.AddDays(14),
                    dateCreate: seedDate,
                    dateUpdate: seedDate));
            }
        }

        if (!await _dbContext.Expenses.AnyAsync(x => x.Name == "Seed Marketplace Operations", cancellationToken))
        {
            _dbContext.Expenses.Add(new Expense(
                workspaceId,
                expenseCategory.Id,
                adminUserId,
                idResponsible: null,
                name: "Seed Marketplace Operations",
                description: "Seeded operational expense for local UI testing.",
                cost: 12500,
                status: 1,
                datePay: DevelopmentSeedData.SeedDate.AddDays(5),
                dateCreate: DevelopmentSeedData.SeedDate,
                dateUpdate: DevelopmentSeedData.SeedDate.AddDays(5)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ExpenseCategory> GetOrCreateExpenseCategoryAsync(CancellationToken cancellationToken)
    {
        var category = await _dbContext.ExpenseCategories
            .FirstOrDefaultAsync(x => x.Name == SeedExpenseCategoryName, cancellationToken);

        if (category is not null)
            return category;

        category = new ExpenseCategory(
            SeedExpenseCategoryName,
            "Seeded local expense category.",
            DevelopmentSeedData.SeedDate,
            DevelopmentSeedData.SeedDate);

        _dbContext.ExpenseCategories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return category;
    }

    private void LogCredentials()
    {
        _logger.LogWarning("Development seed credentials:");

        foreach (var user in DevelopmentSeedData.Users)
            _logger.LogWarning("{Login} / {Password}", user.Login, user.Password);
    }

    private static Guid CreateStableGuid(string key)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return new Guid(bytes[..16]);
    }
}
