using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;

namespace AshmesMarketplaces.Application.ParserBatches.Services;

public interface IParserProxyManagementService
{
    Task<ServiceResult<IReadOnlyList<ParserProxyDto>>> GetAdminListAsync(CancellationToken cancellationToken);

    Task<ServiceResult<ParserProxyDto>> CreateAsync(
        CreateParserProxyRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserProxyDto>> UpdateAsync(
        Guid id,
        UpdateParserProxyRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<ServiceResult<ParserRuntimeProxyAssignmentsDto>> GetRuntimeAssignmentsAsync(
        string? parserInstanceId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ParserRuntimeReviewSyncStateDto>> GetRuntimeReviewSyncStateAsync(
        string? wbProductId,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<WbCategoryScopeSubjectMappingDto>>> GetScopeSubjectMappingsAsync(
        long wbMenuId,
        CancellationToken cancellationToken);

    Task<ServiceResult<WbCategoryScopeSubjectMappingDto>> UpdateScopeSubjectMappingAsync(
        Guid id,
        UpdateWbCategoryScopeSubjectMappingRequest request,
        CancellationToken cancellationToken);
}
