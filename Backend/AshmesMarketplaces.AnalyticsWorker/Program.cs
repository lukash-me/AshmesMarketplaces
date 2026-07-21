using AshmesMarketplaces.AnalyticsWorker.Background;
using AshmesMarketplaces.AnalyticsWorker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWorkerDatabase(builder.Configuration);
builder.Services.AddWorkerIntelligenceIntegration(builder.Configuration);
builder.Services.AddWorkerApplicationServices(builder.Configuration);

if (builder.Configuration.GetValue("WorkerFeatures:AnalysisRefreshEnabled", true))
    builder.Services.AddHostedService<AnalysisRefreshHostedService>();

if (builder.Configuration.GetValue("WorkerFeatures:ParserBatchProcessingEnabled", true))
    builder.Services.AddHostedService<ParserBatchProcessingHostedService>();

if (builder.Configuration.GetValue("WorkerFeatures:ParserLaunchEnabled", true))
    builder.Services.AddHostedService<ParserLaunchHostedService>();

if (builder.Configuration.GetValue("WorkerFeatures:ParserLogMonitoringEnabled", true))
    builder.Services.AddHostedService<ParserRunLogMonitoringHostedService>();

var app = builder.Build();
await app.RunAsync();
