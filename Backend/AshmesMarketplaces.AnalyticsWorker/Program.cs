using AshmesMarketplaces.AnalyticsWorker.Background;
using AshmesMarketplaces.AnalyticsWorker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWorkerDatabase(builder.Configuration);
builder.Services.AddWorkerIntelligenceIntegration(builder.Configuration);
builder.Services.AddWorkerApplicationServices();
builder.Services.AddHostedService<AnalysisRefreshHostedService>();
builder.Services.AddHostedService<ParserBatchProcessingHostedService>();

var app = builder.Build();
await app.RunAsync();
