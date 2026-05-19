using AshmesMarketplaces.API.Extensions;
using AshmesMarketplaces.API.Filters;
using AshmesMarketplaces.API.DevelopmentSeed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddApplicationServices();
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

app.UseExceptionHandler();
app.UseSwaggerDocumentation();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
