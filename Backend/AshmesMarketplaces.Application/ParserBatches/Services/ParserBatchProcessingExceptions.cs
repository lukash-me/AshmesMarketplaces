namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserBatchFinalException : Exception
{
    public ParserBatchFinalException(string message) : base(message)
    {
    }

    public ParserBatchFinalException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
