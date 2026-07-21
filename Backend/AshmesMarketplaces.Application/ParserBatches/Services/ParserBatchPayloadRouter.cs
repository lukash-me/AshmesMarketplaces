using System.Text.Json;
using AshmesMarketplaces.Domain.Entities.ParserIngestion;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public sealed class ParserBatchPayloadRouter : IParserBatchPayloadProcessor
{
    private readonly ParserCompleteBatchPayloadProcessor _completeBatchProcessor;
    private readonly ParserRankBatchPayloadProcessor _rankBatchProcessor;

    public ParserBatchPayloadRouter(
        ParserCompleteBatchPayloadProcessor completeBatchProcessor,
        ParserRankBatchPayloadProcessor rankBatchProcessor)
    {
        _completeBatchProcessor = completeBatchProcessor;
        _rankBatchProcessor = rankBatchProcessor;
    }

    public Task<ParserBatchPayloadProcessingSummary> ProcessAsync(
        ParserBatchSubmission batch,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        return batch.BatchKind switch
        {
            "complete_card_batch" => _completeBatchProcessor.ProcessAsync(batch, payload, cancellationToken),
            "rank_snapshot_batch" => _rankBatchProcessor.ProcessAsync(batch, payload, cancellationToken),
            _ => throw new ParserBatchFinalException($"Unsupported parser batch kind '{batch.BatchKind}'.")
        };
    }
}
