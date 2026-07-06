namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserRunRollback
{
    private ParserRunRollback() { }

    public ParserRunRollback(Guid parserProxyRunId, Guid? requestedByUserId, DateTime startedAtUtc)
    {
        if (parserProxyRunId == Guid.Empty)
            throw new ArgumentException("Parser proxy run id is required.", nameof(parserProxyRunId));

        Id = Guid.NewGuid();
        ParserProxyRunId = parserProxyRunId;
        RequestedByUserId = requestedByUserId;
        Status = ParserRunRollbackStatuses.Running;
        StartedAtUtc = DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc);
        CreatedAtUtc = StartedAtUtc;
        UpdatedAtUtc = StartedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ParserProxyRunId { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int CreatedProductsCount { get; private set; }
    public int UpdatedProductsCount { get; private set; }
    public int DeletedProductsCount { get; private set; }
    public int RestoredProductsCount { get; private set; }
    public int ConflictProductsCount { get; private set; }
    public string? Message { get; private set; }
    public string? Error { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Complete(
        int createdProductsCount,
        int updatedProductsCount,
        int deletedProductsCount,
        int restoredProductsCount,
        int conflictProductsCount,
        string message,
        DateTime completedAtUtc)
    {
        Status = ParserRunRollbackStatuses.Completed;
        CreatedProductsCount = Math.Max(0, createdProductsCount);
        UpdatedProductsCount = Math.Max(0, updatedProductsCount);
        DeletedProductsCount = Math.Max(0, deletedProductsCount);
        RestoredProductsCount = Math.Max(0, restoredProductsCount);
        ConflictProductsCount = Math.Max(0, conflictProductsCount);
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        Error = null;
        CompletedAtUtc = DateTime.SpecifyKind(completedAtUtc, DateTimeKind.Utc);
        UpdatedAtUtc = CompletedAtUtc.Value;
    }

    public void Fail(string error, int conflictProductsCount, DateTime completedAtUtc)
    {
        Status = ParserRunRollbackStatuses.Failed;
        Error = string.IsNullOrWhiteSpace(error) ? "Parser run rollback failed." : error.Trim();
        ConflictProductsCount = Math.Max(0, conflictProductsCount);
        CompletedAtUtc = DateTime.SpecifyKind(completedAtUtc, DateTimeKind.Utc);
        UpdatedAtUtc = CompletedAtUtc.Value;
    }
}

public static class ParserRunRollbackStatuses
{
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
