using AshmesMarketplaces.API.Filters;
using AshmesMarketplaces.Application.Brands.Services;
using AshmesMarketplaces.Application.CampaignMetrics.Services;
using AshmesMarketplaces.Application.Campaigns.Services;
using AshmesMarketplaces.Application.Categories.Services;
using AshmesMarketplaces.Application.ExpenseCategories.Services;
using AshmesMarketplaces.Application.Expenses.Services;
using AshmesMarketplaces.Application.Logistics.Services;
using AshmesMarketplaces.Application.Marketplaces.Services;
using AshmesMarketplaces.Application.Marketplaces.Validators;
using AshmesMarketplaces.Application.Orders.Services;
using AshmesMarketplaces.Application.Products.Services;
using AshmesMarketplaces.Application.ReviewReplies.Services;
using AshmesMarketplaces.Application.Reviews.Services;
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
        services.AddValidatorsFromAssemblyContaining<CreateMarketplaceRequestValidator>();
        services.AddScoped<FluentValidationActionFilter>();

        return services;
    }
}
