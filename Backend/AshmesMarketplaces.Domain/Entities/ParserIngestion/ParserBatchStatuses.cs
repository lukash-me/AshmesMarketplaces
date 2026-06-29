namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public static class ParserBatchStatuses
{
    public const string Accepted = "accepted";
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string FailedRetryable = "failed_retryable";
    public const string FailedFinal = "failed_final";

    public static bool IsTerminal(string status) =>
        status is Completed or FailedRetryable or FailedFinal;
}
