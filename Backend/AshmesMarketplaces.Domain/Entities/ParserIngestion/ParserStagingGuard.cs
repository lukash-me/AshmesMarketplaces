namespace AshmesMarketplaces.Domain.Entities.ParserIngestion;

internal static class ParserStagingGuard
{
    public static void EnsureSource(Guid idParserRun, Guid idParserFile, long sourceLineNumber, string rowHash)
    {
        if (idParserRun == Guid.Empty)
            throw new ArgumentException("Parser run id is required.", nameof(idParserRun));

        if (idParserFile == Guid.Empty)
            throw new ArgumentException("Parser file id is required.", nameof(idParserFile));

        if (sourceLineNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(sourceLineNumber), "Source line number must be positive.");

        if (string.IsNullOrWhiteSpace(rowHash))
            throw new ArgumentException("Row hash is required.", nameof(rowHash));
    }
}
