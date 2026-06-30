using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Application.ParserBatches.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/admin/parser")]
[Produces("application/json")]
public sealed class AdminParserController : ControllerBase
{
    private readonly IParserAdminMonitoringService _monitoringService;

    public AdminParserController(IParserAdminMonitoringService monitoringService)
    {
        _monitoringService = monitoringService;
    }

    [HttpGet("instances")]
    [ProducesResponseType(typeof(IReadOnlyList<ParserAdminInstanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ParserAdminInstanceDto>>> GetInstances(
        CancellationToken cancellationToken)
    {
        var result = await _monitoringService.GetInstancesAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("journal")]
    [ProducesResponseType(typeof(IReadOnlyList<ParserAdminProxyRunJournalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ParserAdminProxyRunJournalDto>>> GetJournal(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _monitoringService.GetJournalAsync(page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("batches")]
    [ProducesResponseType(typeof(IReadOnlyList<ParserAdminBatchListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ParserAdminBatchListItemDto>>> GetBatches(
        [FromQuery] string? status,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _monitoringService.GetBatchesAsync(status, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("batches/{id:guid}")]
    [ProducesResponseType(typeof(ParserAdminBatchDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParserAdminBatchDetailDto>> GetBatch(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _monitoringService.GetBatchAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("niches")]
    [ProducesResponseType(typeof(IReadOnlyList<ParserAdminNicheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ParserAdminNicheDto>>> GetNiches(
        CancellationToken cancellationToken)
    {
        var result = await _monitoringService.GetNichesAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("errors")]
    [ProducesResponseType(typeof(IReadOnlyList<ParserAdminErrorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ParserAdminErrorDto>>> GetErrors(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _monitoringService.GetErrorsAsync(page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("batches/{id:guid}/retry")]
    [ProducesResponseType(typeof(ParserAdminRetryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParserAdminRetryResponse>> RetryBatch(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _monitoringService.RetryBatchAsync(id, cancellationToken);
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
