namespace AshmesMarketplaces.Application.ParserIngestion.Dtos;

public sealed record ParserIngestionResult(
    string Mode,
    string ParserRunId,
    bool DryRun,
    long RowsRead,
    long RowsWritten,
    long RowsSkipped,
    long ErrorCount,
    IReadOnlyDictionary<string, long> Details);
