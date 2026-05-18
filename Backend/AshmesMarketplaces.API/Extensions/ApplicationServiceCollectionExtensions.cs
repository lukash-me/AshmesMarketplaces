using AshmesMarketplaces.API.Filters;
using AshmesMarketplaces.Application.Brands.Services;
using AshmesMarketplaces.Application.Categories.Services;
using AshmesMarketplaces.Application.Marketplaces.Services;
using AshmesMarketplaces.Application.Marketplaces.Validators;
using AshmesMarketplaces.Application.Warehouses.Services;
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
        services.AddValidatorsFromAssemblyContaining<CreateMarketplaceRequestValidator>();
        services.AddScoped<FluentValidationActionFilter>();

        return services;
    }
}
