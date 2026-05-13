using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.Entities.Rules;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.DataAccess;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVideo> ProductVideos => Set<ProductVideo>();
    public DbSet<ProductHistory> ProductHistories => Set<ProductHistory>();

    public DbSet<Marketplace> Marketplaces => Set<Marketplace>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandMarketplace> BrandMarketplaces => Set<BrandMarketplace>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<PermissionCategory> PermissionCategories => Set<PermissionCategory>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RoleSubrole> RoleSubroles => Set<RoleSubrole>();

    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<RuleSet> RuleSets => Set<RuleSet>();
    public DbSet<RuleSetRule> RuleSetRules => Set<RuleSetRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
