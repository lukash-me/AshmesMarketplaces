namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed record ParserBatchProcessingResult(
    bool BatchClaimed,
    Guid? BatchId,
    string? ExternalBatchId,
    string Status,
    string? Message)
{
    public static ParserBatchProcessingResult NoWork() =>
        new(false, null, null, "idle", null);
}

public interface IParserBatchProcessingService
{
    Task<ParserBatchProcessingResult> ProcessNextAsync(CancellationToken cancellationToken);
}
