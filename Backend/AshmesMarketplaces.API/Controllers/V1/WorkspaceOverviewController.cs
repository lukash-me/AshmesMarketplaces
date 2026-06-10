using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.WorkspaceOverview.Dtos;
using AshmesMarketplaces.Application.WorkspaceOverview.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/workspaces/{workspaceId:guid}/overview")]
[Produces("application/json")]
public sealed class WorkspaceOverviewController : ControllerBase
{
    private readonly IWorkspaceOverviewService _service;

    public WorkspaceOverviewController(IWorkspaceOverviewService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(WorkspaceOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceOverviewResponse>> Get(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(workspaceId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("recalculate")]
    [ProducesResponseType(typeof(WorkspaceOverviewRecalculateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WorkspaceOverviewRecalculateResponse>> Recalculate(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var result = await _service.RecalculateAsync(workspaceId, cancellationToken);
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

    private static string GetProblemTitle(ServiceErrorType errorType)
    {
        return errorType switch
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
}
