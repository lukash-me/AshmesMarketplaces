using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketRecommendations.Dtos;
using AshmesMarketplaces.Application.MarketRecommendations.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/market/recommendations")]
[Produces("application/json")]
public sealed class MarketRecommendationsController : ControllerBase
{
    private readonly IMarketHotProductsRecalculationService _recalculationService;
    private readonly IMarketHotProductsReadService _readService;

    public MarketRecommendationsController(
        IMarketHotProductsRecalculationService recalculationService,
        IMarketHotProductsReadService readService)
    {
        _recalculationService = recalculationService;
        _readService = readService;
    }

    [HttpGet("hot-products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(HotProductsListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HotProductsListResponse>> GetHotProducts(
        [FromQuery] HotProductsListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _readService.GetHotProductsAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("hot-products/recalculate")]
    [ProducesResponseType(typeof(RecalculateHotProductsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RecalculateHotProductsResponse>> RecalculateHotProducts(
        [FromBody] RecalculateHotProductsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _recalculationService.RecalculatePublicAsync(request, cancellationToken);
        return ToActionResult(result);
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
