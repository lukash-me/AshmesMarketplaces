using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;
using Microsoft.Extensions.Logging;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public sealed class IntelligenceClient : IIntelligenceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<IntelligenceClient> _logger;

    public IntelligenceClient(HttpClient httpClient, ILogger<IntelligenceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ServiceResult<HotProductsIntelligenceResponse>> CalculateHotProductsAsync(
        HotProductsIntelligenceRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(
                "/api/v1/recommendations/hot-products",
                content,
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = TryReadPythonError(responseBody)
                    ?? $"Intelligence service returned HTTP {(int)response.StatusCode}.";

                _logger.LogWarning(
                    "Intelligence hot-products call failed. requestId={RequestId} productCount={ProductCount} algorithm={Algorithm} statusCode={StatusCode} durationMs={DurationMs}",
                    request.RequestId,
                    request.Products.Count,
                    request.Options.Algorithm,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);

                return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
                    ? ServiceResult<HotProductsIntelligenceResponse>.BadRequest(errorMessage)
                    : ServiceResult<HotProductsIntelligenceResponse>.Unavailable(errorMessage);
            }

            HotProductsIntelligenceResponse? payload;
            try
            {
                payload = JsonSerializer.Deserialize<HotProductsIntelligenceResponse>(responseBody, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Intelligence hot-products returned invalid JSON. requestId={RequestId} productCount={ProductCount} algorithm={Algorithm} durationMs={DurationMs}",
                    request.RequestId,
                    request.Products.Count,
                    request.Options.Algorithm,
                    stopwatch.ElapsedMilliseconds);
                return ServiceResult<HotProductsIntelligenceResponse>.Unavailable(
                    "Intelligence service returned an invalid JSON response.");
            }

            if (payload is null)
                return ServiceResult<HotProductsIntelligenceResponse>.Unavailable(
                    "Intelligence service returned an empty response.");

            _logger.LogInformation(
                "Intelligence hot-products call completed. requestId={RequestId} productCount={ProductCount} algorithm={Algorithm} status={Status} durationMs={DurationMs}",
                request.RequestId,
                request.Products.Count,
                request.Options.Algorithm,
                payload.Status,
                stopwatch.ElapsedMilliseconds);

            return ServiceResult<HotProductsIntelligenceResponse>.Success(payload);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Intelligence hot-products call timed out. requestId={RequestId} productCount={ProductCount} algorithm={Algorithm} durationMs={DurationMs}",
                request.RequestId,
                request.Products.Count,
                request.Options.Algorithm,
                stopwatch.ElapsedMilliseconds);
            return ServiceResult<HotProductsIntelligenceResponse>.Unavailable(
                "Intelligence service request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Intelligence hot-products call could not connect. requestId={RequestId} productCount={ProductCount} algorithm={Algorithm} durationMs={DurationMs}",
                request.RequestId,
                request.Products.Count,
                request.Options.Algorithm,
                stopwatch.ElapsedMilliseconds);
            return ServiceResult<HotProductsIntelligenceResponse>.Unavailable(
                "Intelligence service is unavailable.");
        }
    }

    public async Task<ServiceResult<WorkspaceProductAnalysisIntelligenceResponse>> AnalyzeWorkspaceProductAsync(
        WorkspaceProductAnalysisIntelligenceRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(
                "/api/v1/recommendations/workspace-product-analysis",
                content,
                cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = TryReadPythonError(responseBody)
                    ?? $"Intelligence service returned HTTP {(int)response.StatusCode}.";

                _logger.LogWarning(
                    "Intelligence workspace product analysis failed. requestId={RequestId} productKey={ProductKey} candidateCount={CandidateCount} statusCode={StatusCode} durationMs={DurationMs}",
                    request.RequestId,
                    request.Product.ProductKey,
                    request.Candidates.Count,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);

                return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
                    ? ServiceResult<WorkspaceProductAnalysisIntelligenceResponse>.BadRequest(errorMessage)
                    : ServiceResult<WorkspaceProductAnalysisIntelligenceResponse>.Unavailable(errorMessage);
            }

            WorkspaceProductAnalysisIntelligenceResponse? payload;
            try
            {
                payload = JsonSerializer.Deserialize<WorkspaceProductAnalysisIntelligenceResponse>(responseBody, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Intelligence workspace product analysis returned invalid JSON. requestId={RequestId} productKey={ProductKey} durationMs={DurationMs}",
                    request.RequestId,
                    request.Product.ProductKey,
                    stopwatch.ElapsedMilliseconds);
                return ServiceResult<WorkspaceProductAnalysisIntelligenceResponse>.Unavailable(
                    "Intelligence service returned an invalid JSON response.");
            }

            if (payload is null)
                return ServiceResult<WorkspaceProductAnalysisIntelligenceResponse>.Unavailable(
                    "Intelligence service returned an empty response.");

            _logger.LogInformation(
                "Intelligence workspace product analysis completed. requestId={RequestId} productKey={ProductKey} status={Status} durationMs={DurationMs}",
                request.RequestId,
                request.Product.ProductKey,
                payload.Status,
                stopwatch.ElapsedMilliseconds);

            return ServiceResult<WorkspaceProductAnalysisIntelligenceResponse>.Success(payload);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Intelligence workspace product analysis timed out. requestId={RequestId} productKey={ProductKey} durationMs={DurationMs}",
                request.RequestId,
                request.Product.ProductKey,
                stopwatch.ElapsedMilliseconds);
            return ServiceResult<WorkspaceProductAnalysisIntelligenceResponse>.Unavailable(
                "Intelligence service request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Intelligence workspace product analysis could not connect. requestId={RequestId} productKey={ProductKey} durationMs={DurationMs}",
                request.RequestId,
                request.Product.ProductKey,
                stopwatch.ElapsedMilliseconds);
            return ServiceResult<WorkspaceProductAnalysisIntelligenceResponse>.Unavailable(
                "Intelligence service is unavailable.");
        }
    }

    private static string? TryReadPythonError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            var error = JsonSerializer.Deserialize<IntelligenceErrorResponse>(responseBody, JsonOptions);
            if (!string.IsNullOrWhiteSpace(error?.Message))
                return error.Message;
            if (!string.IsNullOrWhiteSpace(error?.ErrorCode))
                return error.ErrorCode;
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
