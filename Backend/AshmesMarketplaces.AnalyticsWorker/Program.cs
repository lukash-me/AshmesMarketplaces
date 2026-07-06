using AshmesMarketplaces.AnalyticsWorker.Background;
using AshmesMarketplaces.AnalyticsWorker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWorkerDatabase(builder.Configuration);
builder.Services.AddWorkerIntelligenceIntegration(builder.Configuration);
builder.Services.AddWorkerApplicationServices(builder.Configuration);
builder.Services.AddHostedService<AnalysisRefreshHostedService>();
builder.Services.AddHostedService<ParserBatchProcessingHostedService>();
builder.Services.AddHostedService<ParserLaunchHostedService>();
builder.Services.AddHostedService<ParserRunLogMonitoringHostedService>();

var app = builder.Build();
await app.RunAsync();
