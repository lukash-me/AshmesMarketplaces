using AshmesMarketplaces.Domain.Shared;

namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

public sealed class ParserFile
{
    private ParserFile() { }

    public ParserFile(
        Guid idParserRun,
        string kind,
        string path,
        string sha256,
        long byteLength,
        long? rowCount,
        DateTime dateRegisteredUtc)
    {
        if (idParserRun == Guid.Empty)
            throw new ArgumentException("Parser run id is required.", nameof(idParserRun));

        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("Parser file kind is required.", nameof(kind));

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Parser file path is required.", nameof(path));

        if (string.IsNullOrWhiteSpace(sha256))
            throw new ArgumentException("Parser file hash is required.", nameof(sha256));

        if (byteLength < 0)
            throw new ArgumentOutOfRangeException(nameof(byteLength), "Byte length must be non-negative.");

        if (rowCount is < 0)
            throw new ArgumentOutOfRangeException(nameof(rowCount), "Row count must be non-negative.");

        DateTimeUtc.EnsureUtc(dateRegisteredUtc, nameof(dateRegisteredUtc));

        Id = Guid.NewGuid();
        IdParserRun = idParserRun;
        Kind = kind;
        Path = path;
        Sha256 = sha256;
        ByteLength = byteLength;
        RowCount = rowCount;
        DateRegisteredUtc = dateRegisteredUtc;
    }

    public Guid Id { get; private set; }
    public Guid IdParserRun { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public long ByteLength { get; private set; }
    public long? RowCount { get; private set; }
    public DateTime DateRegisteredUtc { get; private set; }
}
