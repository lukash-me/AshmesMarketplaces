using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RuleConstructor.Dtos;
using AshmesMarketplaces.Application.RuleConstructor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[AllowAnonymous]
[Route("api/v1/rule-constructor")]
[Produces("application/json")]
public sealed class RuleConstructorController : ControllerBase
{
    private readonly IRuleConstructorService _service;

    public RuleConstructorController(IRuleConstructorService service)
    {
        _service = service;
    }

    [HttpGet("filters")]
    [ProducesResponseType(typeof(IReadOnlyList<RuleConstructorFilterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RuleConstructorFilterDto>>> GetFilters(CancellationToken cancellationToken)
    {
        var result = await _service.GetFiltersAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("search")]
    [ProducesResponseType(typeof(RuleConstructorSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RuleConstructorSearchResponse>> Search(
        [FromBody] RuleConstructorSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("counts")]
    [ProducesResponseType(typeof(RuleConstructorCountsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RuleConstructorCountsResponse>> GetCounts(
        [FromBody] RuleConstructorCountsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetCountsAsync(request, cancellationToken);
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
            ServiceErrorType.Forbidden => StatusCodes.Status403Forbidden,
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
            ServiceErrorType.Forbidden => "Forbidden",
            ServiceErrorType.Unavailable => "Service unavailable",
            _ => "Unexpected error"
        };
    }
}
