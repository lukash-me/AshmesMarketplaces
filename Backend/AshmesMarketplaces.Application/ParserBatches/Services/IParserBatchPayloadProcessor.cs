using System.Text.Json;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed record ParserBatchPayloadProcessingSummary(
    string Mode,
    long RowsRead,
    long RowsWritten,
    long RowsSkipped,
    long ErrorCount);

public interface IParserBatchPayloadProcessor
{
    Task<ParserBatchPayloadProcessingSummary> ProcessAsync(
        ParserBatchSubmission batch,
        JsonElement payload,
        CancellationToken cancellationToken);
}
