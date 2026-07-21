using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketIntelligence.Dtos;
using AshmesMarketplaces.Application.MarketIntelligence.Services;
using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Route("api/v1/market-intelligence")]
[Produces("application/json")]
public sealed class MarketIntelligenceController : ControllerBase
{
    private readonly IPublicMarketIntelligenceSnapshotReadService _readService;
    private readonly IPublicMarketConcentrationReadService _concentrationReadService;
    private readonly IPublicTopForecastReadService _topForecastReadService;
    private readonly IPublicAnalysisRefreshScheduler _publicAnalysisRefreshScheduler;

    public MarketIntelligenceController(
        IPublicMarketIntelligenceSnapshotReadService readService,
        IPublicMarketConcentrationReadService concentrationReadService,
        IPublicTopForecastReadService topForecastReadService,
        IPublicAnalysisRefreshScheduler publicAnalysisRefreshScheduler)
    {
        _readService = readService;
        _concentrationReadService = concentrationReadService;
        _topForecastReadService = topForecastReadService;
        _publicAnalysisRefreshScheduler = publicAnalysisRefreshScheduler;
    }

    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicMarketIntelligenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicMarketIntelligenceDto>> GetPublic(
        [FromQuery] PublicMarketIntelligenceQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _readService.GetAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("public/contexts")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PublicMarketIntelligenceContextAvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicMarketIntelligenceContextAvailabilityDto>>> GetPublicContexts(
        CancellationToken cancellationToken)
    {
        var contexts = await _readService.GetAvailableContextsAsync(cancellationToken);
        return Ok(contexts);
    }

    [HttpGet("public/concentration")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicMarketConcentrationSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicMarketConcentrationSnapshotDto>> GetPublicConcentration(
        [FromQuery] PublicMarketIntelligenceQuery request,
        [FromQuery] bool includePoints,
        CancellationToken cancellationToken)
    {
        var result = await _concentrationReadService.GetAsync(request, includePoints, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("public/concentration/contexts")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PublicMarketIntelligenceContextAvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicMarketIntelligenceContextAvailabilityDto>>> GetPublicConcentrationContexts(
        CancellationToken cancellationToken)
    {
        var contexts = await _concentrationReadService.GetAvailableContextsAsync(cancellationToken);
        return Ok(contexts);
    }

    [HttpGet("public/concentration/products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicMarketConcentrationProductsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicMarketConcentrationProductsDto>> GetPublicConcentrationProducts(
        [FromQuery] PublicMarketIntelligenceQuery request,
        [FromQuery] string kind,
        [FromQuery] string key,
        CancellationToken cancellationToken)
    {
        var result = await _concentrationReadService.GetProductsAsync(request, kind, key, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("public/top-forecast")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicTopForecastResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PublicTopForecastResponse>> GetPublicTopForecast(
        [FromQuery] PublicTopForecastQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _topForecastReadService.GetAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("top-forecast/recalculate")]
    [Authorize]
    [ProducesResponseType(typeof(SchedulePublicAnalysisRefreshResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SchedulePublicAnalysisRefreshResponse>> RecalculateTopForecast(
        CancellationToken cancellationToken)
    {
        var result = await _publicAnalysisRefreshScheduler.RequestRunAsync(
            PublicAnalysisSchedule.TopForecastScheduleKey,
            cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result.Error!);

        return Accepted(result.Value);
    }

    private ActionResult<T> ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return ToActionResult(result.Error!);
    }

    private ObjectResult ToActionResult(ServiceError error)
    {
        var statusCode = error.Type switch
        {
            ServiceErrorType.BadRequest => StatusCodes.Status400BadRequest,
            ServiceErrorType.NotFound => StatusCodes.Status404NotFound,
            ServiceErrorType.Conflict => StatusCodes.Status409Conflict,
            ServiceErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ServiceErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problem(
            title: GetProblemTitle(error.Type),
            detail: error.Message,
            statusCode: statusCode,
            type: $"https://httpstatuses.com/{statusCode}");
    }

    private static string GetProblemTitle(ServiceErrorType errorType)
    {
        return errorType switch
        {
            ServiceErrorType.BadRequest => "Invalid request",
            ServiceErrorType.NotFound => "Resource not found",
            ServiceErrorType.Conflict => "Conflict",
            ServiceErrorType.Unauthorized => "Unauthorized",
            ServiceErrorType.Unavailable => "Service unavailable",
            _ => "Unexpected error"
        };
    }
}
