namespace AshmesMarketplaces.Application.ParserIngestion.Services;

public sealed record ParserCdcApplyContext(Guid ParserProxyRunId, Guid ParserBatchSubmissionId, string? ParserCycleId = null);
