using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserImportExecution
{
    private ParserImportExecution() { }

    public ParserImportExecution(
        Guid? idParserRun,
        string mode,
        bool isDryRun,
        DateTime startedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(mode))
            throw new ArgumentException("Import mode is required.", nameof(mode));

        DateTimeUtc.EnsureUtc(startedAtUtc, nameof(startedAtUtc));

        Id = Guid.NewGuid();
        IdParserRun = idParserRun;
        Mode = mode;
        IsDryRun = isDryRun;
        Status = "running";
        StartedAtUtc = startedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? IdParserRun { get; private set; }
    public string Mode { get; private set; } = string.Empty;
    public bool IsDryRun { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public long RowsRead { get; private set; }
    public long RowsWritten { get; private set; }
    public long RowsSkipped { get; private set; }
    public long ErrorCount { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }

    public void Finish(
        string status,
        long rowsRead,
        long rowsWritten,
        long rowsSkipped,
        long errorCount,
        DateTime finishedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Import status is required.", nameof(status));

        DateTimeUtc.EnsureUtc(finishedAtUtc, nameof(finishedAtUtc));

        Status = status;
        RowsRead = rowsRead;
        RowsWritten = rowsWritten;
        RowsSkipped = rowsSkipped;
        ErrorCount = errorCount;
        FinishedAtUtc = finishedAtUtc;
    }
}
