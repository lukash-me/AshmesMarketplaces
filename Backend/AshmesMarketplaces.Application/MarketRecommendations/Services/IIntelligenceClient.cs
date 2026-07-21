using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public interface IIntelligenceClient
{
    Task<ServiceResult<HotProductsIntelligenceResponse>> CalculateHotProductsAsync(
        HotProductsIntelligenceRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<TopForecastTrainResponse>> TrainTopForecastAsync(
        TopForecastTrainRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<TopForecastPredictResponse>> PredictTopForecastAsync(
        TopForecastPredictRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<WorkspaceProductAnalysisIntelligenceResponse>> AnalyzeWorkspaceProductAsync(
        WorkspaceProductAnalysisIntelligenceRequest request,
        CancellationToken cancellationToken);
}
