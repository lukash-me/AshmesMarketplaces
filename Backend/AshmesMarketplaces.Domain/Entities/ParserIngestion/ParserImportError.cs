using System.Text.Json;
using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserImportError : IDisposable
{
    private ParserImportError() { }

    public ParserImportError(
        Guid idImportExecution,
        Guid? idParserRun,
        Guid? idParserFile,
        long? sourceLineNumber,
        string phase,
        string severity,
        string message,
        JsonDocument? details,
        DateTime dateCreatedUtc)
    {
        if (idImportExecution == Guid.Empty)
            throw new ArgumentException("Import execution id is required.", nameof(idImportExecution));

        if (string.IsNullOrWhiteSpace(phase))
            throw new ArgumentException("Import error phase is required.", nameof(phase));

        if (string.IsNullOrWhiteSpace(severity))
            throw new ArgumentException("Import error severity is required.", nameof(severity));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Import error message is required.", nameof(message));

        if (sourceLineNumber is < 1)
            throw new ArgumentOutOfRangeException(nameof(sourceLineNumber), "Source line number must be positive.");

        DateTimeUtc.EnsureUtc(dateCreatedUtc, nameof(dateCreatedUtc));

        Id = Guid.NewGuid();
        IdImportExecution = idImportExecution;
        IdParserRun = idParserRun;
        IdParserFile = idParserFile;
        SourceLineNumber = sourceLineNumber;
        Phase = phase;
        Severity = severity;
        Message = message;
        Details = details;
        DateCreatedUtc = dateCreatedUtc;
    }

    public Guid Id { get; private set; }
    public Guid IdImportExecution { get; private set; }
    public Guid? IdParserRun { get; private set; }
    public Guid? IdParserFile { get; private set; }
    public long? SourceLineNumber { get; private set; }
    public string Phase { get; private set; } = string.Empty;
    public string Severity { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public JsonDocument? Details { get; private set; }
    public DateTime DateCreatedUtc { get; private set; }

    public void Dispose()
    {
        Details?.Dispose();
    }
}
