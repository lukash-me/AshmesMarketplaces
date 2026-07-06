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
    private readonly IParserProxyManagementService _proxyManagementService;
    private readonly IParserInstanceConfigurationService _instanceConfigurationService;
    private readonly IParserLaunchRequestService _launchRequestService;
    private readonly IParserRunRollbackService _rollbackService;

    public AdminParserController(
        IParserAdminMonitoringService monitoringService,
        IParserProxyManagementService proxyManagementService,
        IParserInstanceConfigurationService instanceConfigurationService,
        IParserLaunchRequestService launchRequestService,
        IParserRunRollbackService rollbackService)
    {
        _monitoringService = monitoringService;
        _proxyManagementService = proxyManagementService;
        _instanceConfigurationService = instanceConfigurationService;
        _launchRequestService = launchRequestService;
        _rollbackService = rollbackService;
    }

    [HttpGet("instance-configurations")]
    [ProducesResponseType(typeof(IReadOnlyList<ParserInstanceConfigurationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ParserInstanceConfigurationDto>>> GetInstanceConfigurations(
        CancellationToken cancellationToken)
    {
        var result = await _instanceConfigurationService.GetAdminListAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("instance-configurations")]
    [ProducesResponseType(typeof(ParserInstanceConfigurationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParserInstanceConfigurationDto>> CreateInstanceConfiguration(
        [FromBody] CreateParserInstanceConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _instanceConfigurationService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result.Error!);

        return CreatedAtAction(nameof(GetInstanceConfigurations), result.Value);
    }

    [HttpPatch("instance-configurations/{id:guid}")]
    [ProducesResponseType(typeof(ParserInstanceConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParserInstanceConfigurationDto>> UpdateInstanceConfiguration(
        Guid id,
        [FromBody] UpdateParserInstanceConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _instanceConfigurationService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("instance-configurations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteInstanceConfiguration(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _instanceConfigurationService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result.Error!);

        return NoContent();
    }

    [HttpPost("instance-configurations/{id:guid}/launch")]
    [ProducesResponseType(typeof(ParserLaunchRequestDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParserLaunchRequestDto>> LaunchInstance(
        Guid id,
        [FromBody] CreateParserLaunchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _launchRequestService.RequestLaunchAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result.Error!);

        return Accepted(result.Value);
    }

    [HttpGet("proxies")]
    [ProducesResponseType(typeof(IReadOnlyList<ParserProxyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ParserProxyDto>>> GetProxies(
        CancellationToken cancellationToken)
    {
        var result = await _proxyManagementService.GetAdminListAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("proxies")]
    [ProducesResponseType(typeof(ParserProxyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ParserProxyDto>> CreateProxy(
        [FromBody] CreateParserProxyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _proxyManagementService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result.Error!);

        return CreatedAtAction(nameof(GetProxies), result.Value);
    }

    [HttpPatch("proxies/{id:guid}")]
    [ProducesResponseType(typeof(ParserProxyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParserProxyDto>> UpdateProxy(
        Guid id,
        [FromBody] UpdateParserProxyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _proxyManagementService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("proxies/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProxy(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _proxyManagementService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result.Error!);

        return NoContent();
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

    [HttpGet("journal/{id:guid}/rollback-preview")]
    [ProducesResponseType(typeof(ParserRunRollbackPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParserRunRollbackPreviewDto>> GetRollbackPreview(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _rollbackService.PreviewAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("journal/{id:guid}/rollback")]
    [ProducesResponseType(typeof(ParserRunRollbackResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParserRunRollbackResponseDto>> RollbackJournalRow(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _rollbackService.RollbackAsync(id, cancellationToken);
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
