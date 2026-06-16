using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using AshmesMarketplaces.Application.MarketRecommendations.Intelligence;
using AshmesMarketplaces.Application.MarketRecommendations.Options;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AshmesMarketplaces.Application.MarketRecommendations.Services;

public sealed class MarketHotProductsRecalculationService : IMarketHotProductsRecalculationService
{
    private const string StatusCompleted = "completed";
    private const string StatusNotEnoughData = "not_enough_data";
    private const string StatusUnsupported = "unsupported";
    private const string StatusFailed = "failed";
    private const string AlgorithmVersion = "1.0.0";
    private const string ModelVersion = "none";
    private static readonly HashSet<string> AllowedStatuses = [StatusCompleted, StatusNotEnoughData, StatusUnsupported];
    private static readonly HashSet<string> AllowedDirections = ["positive", "negative", "neutral"];
    private static readonly string[] BannedSellerFacingWords =
    [
        "parser",
        "parsed",
        "staging",
        "root_payload",
        "payload",
        "attribution",
        "full history"
    ];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IMarketHotProductsSnapshotBuilder _snapshotBuilder;
    private readonly IIntelligenceClient _intelligenceClient;
    private readonly ICurrentUser _currentUser;
    private readonly IntelligenceOptions _options;
    private readonly ILogger<MarketHotProductsRecalculationService> _logger;

    public MarketHotProductsRecalculationService(
        ApplicationDbContext dbContext,
        IMarketHotProductsSnapshotBuilder snapshotBuilder,
        IIntelligenceClient intelligenceClient,
        ICurrentUser currentUser,
        IOptions<IntelligenceOptions> options,
        ILogger<MarketHotProductsRecalculationService> logger)
    {
        _dbContext = dbContext;
        _snapshotBuilder = snapshotBuilder;
        _intelligenceClient = intelligenceClient;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<RecalculateHotProductsResponse>> RecalculateAsync(
        RecalculateHotProductsRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            return ServiceResult<RecalculateHotProductsResponse>.Unauthorized("Authentication is required.");

        return await RecalculateForUserAsync(_currentUser.UserId.Value, request, cancellationToken);
    }

    public async Task<ServiceResult<RecalculateHotProductsResponse>> RecalculateForUserAsync(
        Guid userId,
        RecalculateHotProductsRequest request,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            return ServiceResult<RecalculateHotProductsResponse>.BadRequest("User id is required.");

        if (!_options.Enabled)
            return ServiceResult<RecalculateHotProductsResponse>.Unavailable("Intelligence service integration is disabled.");

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _))
            return ServiceResult<RecalculateHotProductsResponse>.Unavailable("Intelligence service base URL is invalid.");

        if (_options.TimeoutSeconds <= 0 || _options.MaxProductsPerRequest <= 0)
            return ServiceResult<RecalculateHotProductsResponse>.Unavailable("Intelligence service configuration is invalid.");

        var snapshotResult = await _snapshotBuilder.BuildAsync(request, _options, cancellationToken);
        if (!snapshotResult.IsSuccess)
            return snapshotResult.Error!.Type == ServiceErrorType.NotFound
                ? ServiceResult<RecalculateHotProductsResponse>.NotFound(snapshotResult.Error.Message)
                : ServiceResult<RecalculateHotProductsResponse>.BadRequest(snapshotResult.Error.Message);

        var snapshot = snapshotResult.Value!;
        var requestId = $"hot-products-{Guid.NewGuid():N}";
        var intelligenceRequest = BuildIntelligenceRequest(request, snapshot, requestId);
        var inputSnapshotHash = ComputeInputSnapshotHash(intelligenceRequest);
        var forceRecalculate = request.ForceRecalculate == true;

        if (!forceRecalculate)
        {
            var existing = await _dbContext.MarketRecommendationRuns
                .AsNoTracking()
                .Where(x =>
                    x.Kind == MarketRecommendationRun.HotProductsKind
                    && x.IdUser == userId
                    && x.Status == StatusCompleted
                    && x.Algorithm == _options.HotProductsAlgorithm
                    && x.AlgorithmVersion == AlgorithmVersion
                    && x.ModelVersion == ModelVersion
                    && x.InputSnapshotHash == inputSnapshotHash)
                .OrderByDescending(x => x.CompletedAtUtc)
                .ThenByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
                return ServiceResult<RecalculateHotProductsResponse>.Success(MapRunSummary(existing));
        }

        var batchResult = await CalculateInBatchesAsync(
            request,
            userId,
            snapshot,
            requestId,
            inputSnapshotHash,
            cancellationToken);
        if (!batchResult.IsSuccess)
            return batchResult.Error!.Type == ServiceErrorType.BadRequest
                ? ServiceResult<RecalculateHotProductsResponse>.BadRequest(batchResult.Error.Message)
                : ServiceResult<RecalculateHotProductsResponse>.Unavailable(batchResult.Error.Message);

        var batchResponse = batchResult.Value!;
        var recommendations = batchResponse.Recommendations;
        var validUntilUtc = recommendations.Count == 0 ? (DateTime?)null : recommendations.Max(x => x.ValidUntilUtc);
        var persistedRun = await PersistRunAsync(
            snapshot,
            userId,
            intelligenceRequest,
            inputSnapshotHash,
            batchResponse.Status,
            batchResponse.AlgorithmVersion,
            batchResponse.ModelVersion,
            batchResponse.ComputedAtUtc,
            validUntilUtc,
            recommendations.Count,
            batchResponse.Warnings,
            errorCode: null,
            errorMessage: null,
            recommendations,
            cancellationToken);

        return ServiceResult<RecalculateHotProductsResponse>.Success(MapRunSummary(persistedRun));
    }

    private HotProductsIntelligenceRequest BuildIntelligenceRequest(
        RecalculateHotProductsRequest request,
        MarketHotProductsSnapshot snapshot,
        string requestId)
    {
        return new HotProductsIntelligenceRequest(
            requestId,
            DateTime.UtcNow,
            snapshot.Marketplace,
            new HotProductsIntelligenceScope(
                snapshot.SourceCategory,
                snapshot.SourceSubcategories,
                snapshot.ProductParserRunId,
                snapshot.RankParserRunId,
                snapshot.ReviewParserRunIds),
            snapshot.Products.Select(x => x.Product).ToList(),
            new HotProductsIntelligenceOptions(
                request.MaxRecommendations,
                request.MinConfidence,
                request.MinProductsForScoring,
                _options.HotProductsAlgorithm));
    }

    private static string ComputeInputSnapshotHash(HotProductsIntelligenceRequest request)
    {
        var canonical = new
        {
            request.Marketplace,
            request.Scope,
            request.Products,
            request.Options
        };
        var json = JsonSerializer.Serialize(canonical, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IReadOnlyList<string> ValidateResponse(
        HotProductsIntelligenceResponse response,
        HotProductsIntelligenceRequest request,
        MarketHotProductsSnapshot snapshot)
    {
        var errors = new List<string>();
        if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal))
            errors.Add("Response requestId does not match request.");
        if (!AllowedStatuses.Contains(response.Status))
            errors.Add($"Unsupported response status '{response.Status}'.");
        if (response.ComputedAtUtc.Kind != DateTimeKind.Utc)
            errors.Add("computedAtUtc must be UTC.");
        if (response.Status == StatusCompleted)
        {
            if (!string.Equals(response.Algorithm, request.Options.Algorithm, StringComparison.Ordinal))
                errors.Add("Completed response algorithm does not match configured algorithm.");
            if (!string.Equals(response.AlgorithmVersion, AlgorithmVersion, StringComparison.Ordinal))
                errors.Add("Completed response algorithmVersion is not supported.");
            if (!string.Equals(response.ModelVersion, ModelVersion, StringComparison.Ordinal))
                errors.Add("Completed response modelVersion is not supported.");
        }

        var productKeys = snapshot.Products
            .Select(x => x.Product.ProductKey)
            .ToHashSet(StringComparer.Ordinal);
        var recommendationKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var recommendation in response.Recommendations)
        {
            if (recommendation.Score is < 0 or > 100)
                errors.Add($"Recommendation '{recommendation.RecommendationKey}' score is out of range.");
            if (recommendation.Confidence is < 0 or > 1)
                errors.Add($"Recommendation '{recommendation.RecommendationKey}' confidence is out of range.");
            if (string.IsNullOrWhiteSpace(recommendation.ProductKey) || !productKeys.Contains(recommendation.ProductKey))
                errors.Add($"Recommendation '{recommendation.RecommendationKey}' productKey was not in submitted snapshot.");
            if (string.IsNullOrWhiteSpace(recommendation.RecommendationKey))
                errors.Add("Recommendation key is required.");
            else if (!recommendationKeys.Add(recommendation.RecommendationKey))
                errors.Add($"Duplicate recommendationKey '{recommendation.RecommendationKey}'.");
            if (string.IsNullOrWhiteSpace(recommendation.Title))
                errors.Add($"Recommendation '{recommendation.RecommendationKey}' title is required.");
            if (string.IsNullOrWhiteSpace(recommendation.Reason))
                errors.Add($"Recommendation '{recommendation.RecommendationKey}' reason is required.");
            if (string.IsNullOrWhiteSpace(recommendation.InputSnapshotHash))
                errors.Add($"Recommendation '{recommendation.RecommendationKey}' inputSnapshotHash is required.");
            if (recommendation.ValidUntilUtc is null || recommendation.ValidUntilUtc.Value.Kind != DateTimeKind.Utc)
                errors.Add($"Recommendation '{recommendation.RecommendationKey}' validUntilUtc must be UTC.");
            if (recommendation.Factors.Count == 0)
                errors.Add($"Recommendation '{recommendation.RecommendationKey}' factors are required.");
            if (ContainsBannedSellerFacingWord(recommendation.Title)
                || ContainsBannedSellerFacingWord(recommendation.Reason))
            {
                errors.Add($"Recommendation '{recommendation.RecommendationKey}' contains internal terms.");
            }

            foreach (var factor in recommendation.Factors)
            {
                if (string.IsNullOrWhiteSpace(factor.Code))
                    errors.Add($"Recommendation '{recommendation.RecommendationKey}' has a factor without code.");
                if (string.IsNullOrWhiteSpace(factor.Label))
                    errors.Add($"Recommendation '{recommendation.RecommendationKey}' has a factor without label.");
                if (!AllowedDirections.Contains(factor.Direction))
                    errors.Add($"Recommendation '{recommendation.RecommendationKey}' has unsupported factor direction '{factor.Direction}'.");
                if (ContainsBannedSellerFacingWord(factor.Label))
                    errors.Add($"Recommendation '{recommendation.RecommendationKey}' has a factor label with internal terms.");
            }
        }

        if (response.Status != StatusCompleted && response.Recommendations.Count > 0)
            errors.Add("Non-completed responses must not contain recommendations.");

        return errors;
    }

    private async Task<MarketRecommendationRun> PersistRunAsync(
        MarketHotProductsSnapshot snapshot,
        Guid idUser,
        HotProductsIntelligenceRequest request,
        string inputSnapshotHash,
        string status,
        string algorithmVersion,
        string modelVersion,
        DateTime? completedAtUtc,
        DateTime? validUntilUtc,
        int recommendationsCount,
        IReadOnlyList<string> warnings,
        string? errorCode,
        string? errorMessage,
        IReadOnlyList<HotProductRecommendationDto> recommendations,
        CancellationToken cancellationToken)
    {
        var createdAtUtc = DateTime.UtcNow;
        var run = new MarketRecommendationRun(
            MarketRecommendationRun.HotProductsKind,
            idUser,
            snapshot.Marketplace,
            snapshot.SourceCategory,
            ToJsonDocument(snapshot.SourceSubcategories),
            snapshot.ProductParserRunId,
            snapshot.RankParserRunId,
            ToJsonDocument(snapshot.ReviewParserRunIds),
            request.RequestId,
            request.Options.Algorithm ?? _options.HotProductsAlgorithm,
            algorithmVersion,
            modelVersion,
            inputSnapshotHash,
            status,
            request.GeneratedAtUtc,
            completedAtUtc,
            validUntilUtc,
            snapshot.Products.Count,
            recommendationsCount,
            warnings.Count,
            errorCode,
            errorMessage,
            warnings.Count == 0 ? null : ToJsonDocument(warnings),
            ToJsonDocument(request.Options),
            createdAtUtc);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _dbContext.MarketRecommendationRuns.Add(run);
        foreach (var item in BuildRecommendationItems(snapshot, recommendations, run.Id))
            _dbContext.MarketHotProductRecommendations.Add(item);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return run;
    }

    private static IEnumerable<MarketHotProductRecommendation> BuildRecommendationItems(
        MarketHotProductsSnapshot snapshot,
        IReadOnlyList<HotProductRecommendationDto> recommendations,
        Guid runId)
    {
        var productsByKey = snapshot.Products.ToDictionary(x => x.Product.ProductKey, StringComparer.Ordinal);
        var rankOrder = 1;
        foreach (var recommendation in recommendations)
        {
            var product = productsByKey[recommendation.ProductKey];
            yield return new MarketHotProductRecommendation(
                runId,
                recommendation.RecommendationKey,
                recommendation.ProductKey,
                recommendation.WbProductId,
                recommendation.WbRootId,
                product.ParserProductRowId,
                product.Product.SourceCategory,
                product.Product.SourceSubcategory,
                product.ProductName,
                product.BrandName,
                product.SellerName,
                product.Price,
                product.PriceWithoutDiscount,
                product.WalletPrice,
                product.Rating,
                product.FeedbackCount,
                product.ParsedReviewCount,
                product.ParsedReplyCount,
                product.Position,
                product.PositionState,
                product.ObservedRangeLimit,
                product.TotalQuantity,
                recommendation.Score,
                recommendation.Confidence,
                recommendation.Title,
                recommendation.Reason,
                recommendation.InputSnapshotHash,
                recommendation.ValidUntilUtc!.Value,
                rankOrder++,
                ToJsonDocument(recommendation.Factors),
                DateTime.UtcNow);
        }
    }

    private static RecalculateHotProductsResponse MapRunSummary(MarketRecommendationRun run)
    {
        return new RecalculateHotProductsResponse(
            run.Id,
            run.Status,
            run.Algorithm,
            run.AlgorithmVersion,
            run.ModelVersion,
            run.InputSnapshotHash,
            run.ProductParserRunId,
            run.RankParserRunId,
            run.ProductCountSent,
            run.RecommendationsCount,
            run.WarningCount,
            run.ValidUntilUtc,
            run.CreatedAtUtc,
            ReadWarnings(run.RawWarnings));
    }

    private async Task<ServiceResult<HotProductsBatchResponse>> CalculateInBatchesAsync(
        RecalculateHotProductsRequest request,
        Guid userId,
        MarketHotProductsSnapshot snapshot,
        string requestId,
        string inputSnapshotHash,
        CancellationToken cancellationToken)
    {
        var chunkSize = Math.Max(_options.MaxProductsPerRequest, 1);
        var recommendations = new List<HotProductRecommendationDto>();
        var warnings = new List<string>();
        var computedAtUtc = DateTime.UtcNow;
        var algorithmVersion = AlgorithmVersion;
        var modelVersion = ModelVersion;
        var chunkIndex = 0;

        foreach (var chunk in snapshot.Products.Chunk(chunkSize))
        {
            chunkIndex++;
            var chunkProducts = chunk.ToList();
            var chunkSnapshot = snapshot with { Products = chunkProducts };
            var chunkRequest = BuildIntelligenceRequest(
                request,
                chunkSnapshot,
                $"{requestId}-batch-{chunkIndex}");

            var clientResult = await _intelligenceClient.CalculateHotProductsAsync(chunkRequest, cancellationToken);
            if (!clientResult.IsSuccess)
            {
                await PersistRunAsync(
                    snapshot,
                    userId,
                    chunkRequest,
                    inputSnapshotHash,
                    StatusFailed,
                    AlgorithmVersion,
                    ModelVersion,
                    completedAtUtc: DateTime.UtcNow,
                    validUntilUtc: null,
                    recommendationsCount: 0,
                    warnings,
                    errorCode: clientResult.Error!.Type == ServiceErrorType.Unavailable
                        ? "intelligence_unavailable"
                        : "intelligence_error",
                    errorMessage: clientResult.Error.Message,
                    recommendations: [],
                    cancellationToken);

                return clientResult.Error.Type == ServiceErrorType.BadRequest
                    ? ServiceResult<HotProductsBatchResponse>.BadRequest(clientResult.Error.Message)
                    : ServiceResult<HotProductsBatchResponse>.Unavailable(clientResult.Error.Message);
            }

            var response = clientResult.Value!;
            var validationErrors = ValidateResponse(response, chunkRequest, chunkSnapshot);
            if (validationErrors.Count > 0)
            {
                await PersistRunAsync(
                    snapshot,
                    userId,
                    chunkRequest,
                    inputSnapshotHash,
                    StatusFailed,
                    response.AlgorithmVersion,
                    response.ModelVersion,
                    response.ComputedAtUtc.Kind == DateTimeKind.Utc ? response.ComputedAtUtc : DateTime.UtcNow,
                    validUntilUtc: null,
                    recommendationsCount: 0,
                    warnings: response.Warnings,
                    errorCode: "intelligence_response_validation_failed",
                    errorMessage: string.Join("; ", validationErrors),
                    recommendations: [],
                    cancellationToken);

                return ServiceResult<HotProductsBatchResponse>.Unavailable(
                    "Intelligence service returned an invalid hot-products response.");
            }

            computedAtUtc = response.ComputedAtUtc.Kind == DateTimeKind.Utc ? response.ComputedAtUtc : computedAtUtc;
            algorithmVersion = response.AlgorithmVersion;
            modelVersion = response.ModelVersion;
            warnings.AddRange(response.Warnings);
            if (response.Status == StatusCompleted)
                recommendations.AddRange(response.Recommendations);
        }

        var maxRecommendations = request.MaxRecommendations ?? 200;
        recommendations = recommendations
            .GroupBy(x => x.ProductKey, StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(item => item.Score).First())
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Confidence)
            .Take(maxRecommendations)
            .ToList();

        return ServiceResult<HotProductsBatchResponse>.Success(new HotProductsBatchResponse(
            recommendations.Count == 0 ? StatusNotEnoughData : StatusCompleted,
            algorithmVersion,
            modelVersion,
            computedAtUtc,
            recommendations,
            warnings.Distinct(StringComparer.Ordinal).ToList()));
    }

    private sealed record HotProductsBatchResponse(
        string Status,
        string AlgorithmVersion,
        string ModelVersion,
        DateTime ComputedAtUtc,
        IReadOnlyList<HotProductRecommendationDto> Recommendations,
        IReadOnlyList<string> Warnings);

    private static IReadOnlyList<string> ReadWarnings(JsonDocument? warnings)
    {
        if (warnings is null || warnings.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return warnings.RootElement
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();
    }

    private static JsonDocument ToJsonDocument<T>(T value)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
    }

    private static bool ContainsBannedSellerFacingWord(string text)
    {
        return BannedSellerFacingWords.Any(word =>
            text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
