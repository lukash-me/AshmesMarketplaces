namespace AshmesMarketplaces.Application.ParserIngestion.Dtos;

public sealed record ParserIngestionOptions(
    int BatchSize = 5000,
    long? MaxRowsPerFile = null,
    bool DryRun = false)
{
    public int NormalizedBatchSize => Math.Clamp(BatchSize, 1, 10000);
}
