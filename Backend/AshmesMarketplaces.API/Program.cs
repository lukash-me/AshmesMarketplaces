using AshmesMarketplaces.API.Extensions;
using AshmesMarketplaces.API.Filters;
using AshmesMarketplaces.API.DevelopmentSeed;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddIntelligenceIntegration(builder.Configuration);
builder.Services.AddApiProblemDetails();
builder.Services.AddAuthSecurity(builder.Configuration);
builder.Services.AddSwaggerDocumentation();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<FluentValidationActionFilter>();
});
builder.Services.Configure<DevelopmentSeedOptions>(
    builder.Configuration.GetSection(DevelopmentSeedOptions.SectionName));

var app = builder.Build();

await app.SeedDevelopmentDataAsync();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 2
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);
app.UseExceptionHandler();
app.UseSwaggerDocumentation();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
