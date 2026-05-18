using AshmesMarketplaces.API.Extensions;
using AshmesMarketplaces.API.Filters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddApiProblemDetails();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<FluentValidationActionFilter>();
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwaggerDocumentation();

app.MapControllers();

app.Run();
