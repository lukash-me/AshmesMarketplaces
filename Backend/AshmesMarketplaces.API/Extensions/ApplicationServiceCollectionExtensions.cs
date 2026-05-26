using AshmesMarketplaces.API.Filters;
using AshmesMarketplaces.API.DevelopmentSeed;
using AshmesMarketplaces.API.Security;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Auth.Services;
using AshmesMarketplaces.Application.Brands.Services;
using AshmesMarketplaces.Application.CampaignMetrics.Services;
using AshmesMarketplaces.Application.Campaigns.Services;
using AshmesMarketplaces.Application.Categories.Services;
using AshmesMarketplaces.Application.ExpenseCategories.Services;
using AshmesMarketplaces.Application.Expenses.Services;
using AshmesMarketplaces.Application.Logistics.Services;
using AshmesMarketplaces.Application.MarketIntelligence.Services;
using AshmesMarketplaces.Application.Marketplaces.Services;
using AshmesMarketplaces.Application.Marketplaces.Validators;
using AshmesMarketplaces.Application.Orders.Services;
using AshmesMarketplaces.Application.ParserObservability.Services;
using AshmesMarketplaces.Application.Products.Services;
using AshmesMarketplaces.Application.PermissionCategories.Services;
using AshmesMarketplaces.Application.Permissions.Services;
using AshmesMarketplaces.Application.ReviewReplies.Services;
using AshmesMarketplaces.Application.Reviews.Services;
using AshmesMarketplaces.Application.RecommendationCategories.Services;
using AshmesMarketplaces.Application.RecommendationProducts.Services;
using AshmesMarketplaces.Application.Recommendations.Services;
using AshmesMarketplaces.Application.RolePermissions.Services;
using AshmesMarketplaces.Application.Roles.Services;
using AshmesMarketplaces.Application.RoleSubroles.Services;
using AshmesMarketplaces.Application.RuleSetRules.Services;
using AshmesMarketplaces.Application.RuleSets.Services;
using AshmesMarketplaces.Application.Rules.Services;
using AshmesMarketplaces.Application.Users.Services;
using AshmesMarketplaces.Application.UserWorkspaces.Services;
using AshmesMarketplaces.Application.Warehouses.Services;
using AshmesMarketplaces.Application.Workspaces.Services;
using FluentValidation;

namespace AshmesMarketplaces.API.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IMarketplaceService, MarketplaceService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductHistoryService, ProductHistoryService>();
        services.AddScoped<IParserProductReadService, ParserProductReadService>();
        services.AddScoped<IParserObservedStockDecreaseReadService, ParserObservedStockDecreaseReadService>();
        services.AddScoped<IParserObservedMarketEventReadService, ParserObservedMarketEventReadService>();
        services.AddScoped<IParserReviewReadService, ParserReviewReadService>();
        services.AddScoped<IParserRunReadService, ParserRunReadService>();
        services.AddScoped<IPublicMarketIntelligenceReadService, PublicMarketIntelligenceReadService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReviewReplyService, ReviewReplyService>();
        services.AddScoped<ILogisticService, LogisticService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<ICampaignMetricService, CampaignMetricService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IUserWorkspaceService, UserWorkspaceService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionCategoryService, PermissionCategoryService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IRoleSubroleService, RoleSubroleService>();
        services.AddScoped<IRuleService, RuleService>();
        services.AddScoped<IRuleSetService, RuleSetService>();
        services.AddScoped<IRuleSetRuleService, RuleSetRuleService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IRecommendationProductService, RecommendationProductService>();
        services.AddScoped<IRecommendationCategoryService, RecommendationCategoryService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<DevelopmentSeeder>();
        services.AddValidatorsFromAssemblyContaining<CreateMarketplaceRequestValidator>();
        services.AddScoped<FluentValidationActionFilter>();

        return services;
    }
}
