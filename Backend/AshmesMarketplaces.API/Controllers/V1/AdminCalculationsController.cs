using AshmesMarketplaces.Application.AdminCalculations.Dtos;
using AshmesMarketplaces.Application.AdminCalculations.Services;
using AshmesMarketplaces.Application.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/admin/calculations")]
[Produces("application/json")]
public sealed class AdminCalculationsController : ControllerBase
{
    private readonly IAdminCalculationService _calculationService;

    public AdminCalculationsController(IAdminCalculationService calculationService)
    {
        _calculationService = calculationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminCalculationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AdminCalculationDto>>> GetCalculations(
        CancellationToken cancellationToken)
    {
        var result = await _calculationService.GetCalculationsAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{scheduleKey}/run")]
    [ProducesResponseType(typeof(AdminCalculationManualRunDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminCalculationManualRunDto>> RunCalculation(
        string scheduleKey,
        CancellationToken cancellationToken)
    {
        var result = await _calculationService.RequestManualRunAsync(scheduleKey, cancellationToken);
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
            ServiceErrorType.Forbidden => StatusCodes.Status403Forbidden,
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

    private static string GetProblemTitle(ServiceErrorType errorType) =>
        errorType switch
        {
            ServiceErrorType.BadRequest => "Invalid request",
            ServiceErrorType.NotFound => "Resource not found",
            ServiceErrorType.Conflict => "Conflict",
            ServiceErrorType.Forbidden => "Forbidden",
            ServiceErrorType.Unauthorized => "Unauthorized",
            ServiceErrorType.Unavailable => "Service unavailable",
            _ => "Unexpected error"
        };
}
