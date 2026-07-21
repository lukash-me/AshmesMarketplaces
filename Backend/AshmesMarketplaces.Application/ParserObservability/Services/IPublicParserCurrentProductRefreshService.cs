using AshmesMarketplaces.Application.Common.Results;

namespace AshmesMarketplaces.Application.ParserObservability.Services;

public interface IPublicParserCurrentProductRefreshService
{
    Task<ServiceResult<PublicParserCurrentProductRefreshResult>> RefreshAsync(CancellationToken cancellationToken);
}

public sealed record PublicParserCurrentProductRefreshResult(
    int TotalCount,
    DateTime CalculatedAtUtc);
