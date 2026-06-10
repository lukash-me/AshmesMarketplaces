using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public interface IIntelligenceClient
{
    Task<ServiceResult<HotProductsIntelligenceResponse>> CalculateHotProductsAsync(
        HotProductsIntelligenceRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<WorkspaceProductAnalysisIntelligenceResponse>> AnalyzeWorkspaceProductAsync(
        WorkspaceProductAnalysisIntelligenceRequest request,
        CancellationToken cancellationToken);
}
