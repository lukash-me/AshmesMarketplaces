using AshmesMarketplaces.Application.ParserBatches.Services;

namespace AshmesMarketplaces.AnalyticsWorker.Background;

public sealed class ParserBatchProcessingHostedService : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AfterBatchDelay = TimeSpan.FromMilliseconds(250);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ParserBatchProcessingHostedService> _logger;

    public ParserBatchProcessingHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ParserBatchProcessingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Parser batch processing worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IParserBatchProcessingService>();
                var result = await processor.ProcessNextAsync(stoppingToken);

                if (!result.BatchClaimed)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                    continue;
                }

                _logger.LogInformation(
                    "Parser batch {ExternalBatchId} processed with status {Status}: {Message}",
                    result.ExternalBatchId,
                    result.Status,
                    result.Message);

                await Task.Delay(AfterBatchDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Parser batch processing loop failed.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }

        _logger.LogInformation("Parser batch processing worker stopped.");
    }
}
