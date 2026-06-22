using AshmesMarketplaces.Domain.Entities.Access;
using AshmesMarketplaces.Domain.Entities.Advertising;
using AshmesMarketplaces.Domain.Entities.Finance;
using AshmesMarketplaces.Domain.Entities.Logistics;
using AshmesMarketplaces.Domain.Entities.Marketplaces;
using AshmesMarketplaces.Domain.Entities.Orders;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;
using AshmesMarketplaces.Domain.Entities.Product;
using AshmesMarketplaces.Domain.Entities.Recommendations;
using AshmesMarketplaces.Domain.Entities.Reviews;
using AshmesMarketplaces.Domain.Entities.Rules;
using AshmesMarketplaces.Domain.Entities.Users;
using AshmesMarketplaces.Domain.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.DataAccess;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVideo> ProductVideos => Set<ProductVideo>();
    public DbSet<ProductHistory> ProductHistories => Set<ProductHistory>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<ParserRun> ParserRuns => Set<ParserRun>();
    public DbSet<ParserFile> ParserFiles => Set<ParserFile>();
    public DbSet<ParserImportExecution> ParserImportExecutions => Set<ParserImportExecution>();
    public DbSet<ParserImportError> ParserImportErrors => Set<ParserImportError>();
    public DbSet<ParserProductRow> ParserProductRows => Set<ParserProductRow>();
    public DbSet<ParserProductDetailRow> ParserProductDetailRows => Set<ParserProductDetailRow>();
    public DbSet<ParserReviewRootFetch> ParserReviewRootFetches => Set<ParserReviewRootFetch>();
    public DbSet<ParserReviewRow> ParserReviewRows => Set<ParserReviewRow>();
    public DbSet<ParserReviewReplyRow> ParserReviewReplyRows => Set<ParserReviewReplyRow>();
    public DbSet<ParserRankSnapshotRow> ParserRankSnapshotRows => Set<ParserRankSnapshotRow>();
    public DbSet<ParserRankPageFetch> ParserRankPageFetches => Set<ParserRankPageFetch>();
    public DbSet<ParserLogisticsSnapshotRow> ParserLogisticsSnapshotRows => Set<ParserLogisticsSnapshotRow>();
    public DbSet<ParserWarehouseAvailabilityRow> ParserWarehouseAvailabilityRows => Set<ParserWarehouseAvailabilityRow>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewReply> ReviewReplies => Set<ReviewReply>();
    public DbSet<Logistic> Logistics => Set<Logistic>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<UserAnalysisSchedule> UserAnalysisSchedules => Set<UserAnalysisSchedule>();
    public DbSet<PublicAnalysisSchedule> PublicAnalysisSchedules => Set<PublicAnalysisSchedule>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<UserWorkspace> UserWorkspaces => Set<UserWorkspace>();
    public DbSet<WorkspaceMarketProduct> WorkspaceMarketProducts => Set<WorkspaceMarketProduct>();
    public DbSet<WorkspaceMarketProductMedia> WorkspaceMarketProductMedia => Set<WorkspaceMarketProductMedia>();
    public DbSet<WorkspaceMarketProductUserReadState> WorkspaceMarketProductUserReadStates => Set<WorkspaceMarketProductUserReadState>();
    public DbSet<WorkspaceMarketProductAnalysisRun> WorkspaceMarketProductAnalysisRuns => Set<WorkspaceMarketProductAnalysisRun>();
    public DbSet<WorkspaceMarketProductAnalysis> WorkspaceMarketProductAnalyses => Set<WorkspaceMarketProductAnalysis>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignMetric> CampaignMetrics => Set<CampaignMetric>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<RecommendationProduct> RecommendationProducts => Set<RecommendationProduct>();
    public DbSet<RecommendationCategory> RecommendationCategories => Set<RecommendationCategory>();
    public DbSet<MarketRecommendationRun> MarketRecommendationRuns => Set<MarketRecommendationRun>();
    public DbSet<MarketHotProductRecommendation> MarketHotProductRecommendations => Set<MarketHotProductRecommendation>();
    public DbSet<PublicTopForecastRun> PublicTopForecastRuns => Set<PublicTopForecastRun>();
    public DbSet<PublicTopForecastPrediction> PublicTopForecastPredictions => Set<PublicTopForecastPrediction>();

    public DbSet<Marketplace> Marketplaces => Set<Marketplace>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandMarketplace> BrandMarketplaces => Set<BrandMarketplace>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<PublicMarketConcentrationSnapshot> PublicMarketConcentrationSnapshots => Set<PublicMarketConcentrationSnapshot>();

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
