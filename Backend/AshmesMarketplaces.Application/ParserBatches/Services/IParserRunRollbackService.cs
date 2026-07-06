using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public interface IParserRunRollbackService
{
    Task<ServiceResult<ParserRunRollbackPreviewDto>> PreviewAsync(Guid parserProxyRunId, CancellationToken cancellationToken);

    Task<ServiceResult<ParserRunRollbackResponseDto>> RollbackAsync(Guid parserProxyRunId, CancellationToken cancellationToken);
}
