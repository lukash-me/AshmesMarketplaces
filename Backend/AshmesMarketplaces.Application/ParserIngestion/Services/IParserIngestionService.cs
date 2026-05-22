using AshmesMarketplaces.Application.ParserIngestion.Dtos;

namespace AshmesMarketplaces.Application.ParserIngestion.Services;

public interface IParserIngestionService
{
    Task<ParserIngestionResult> ValidateProductsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken);

    Task<ParserIngestionResult> ValidateReviewsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken);

    Task<ParserIngestionResult> StageProductsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken);

    Task<ParserIngestionResult> StageReviewsAsync(
        string runDirectory,
        ParserIngestionOptions options,
        CancellationToken cancellationToken);

    Task<ParserIngestionResult> PromoteProductsAsync(
        string parserRunId,
        ParserIngestionOptions options,
        CancellationToken cancellationToken);
}
