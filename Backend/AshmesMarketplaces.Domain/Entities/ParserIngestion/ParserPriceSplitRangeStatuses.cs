namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public static class ParserPriceSplitRangeStatuses
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string CompletedSplit = "completed_split";
    public const string FailedRetryable = "failed_retryable";
    public const string FailedFinal = "failed_final";
    public const string Cooldown = "cooldown";
}
