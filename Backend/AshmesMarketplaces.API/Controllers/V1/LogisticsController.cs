using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Logistics.Dtos;
using AshmesMarketplaces.Application.Logistics.Services;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Route("api/v1/logistics")]
[Produces("application/json")]
public sealed class LogisticsController : ControllerBase
{
    private readonly ILogisticService _logisticService;

    public LogisticsController(ILogisticService logisticService)
    {
        _logisticService = logisticService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<LogisticListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<LogisticListItemResponse>>> GetList([FromQuery] LogisticListQuery query, CancellationToken cancellationToken)
    {
        var result = await _logisticService.GetListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LogisticResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LogisticResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _logisticService.GetByIdAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LogisticResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LogisticResponse>> Create([FromBody] CreateLogisticRequest request, CancellationToken cancellationToken)
    {
        var result = await _logisticService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result);

        var response = result.Value!;
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LogisticResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LogisticResponse>> Update(Guid id, [FromBody] UpdateLogisticRequest request, CancellationToken cancellationToken)
    {
        var result = await _logisticService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _logisticService.DeleteAsync(id, cancellationToken);
        if (result.IsSuccess)
            return NoContent();

        return ToActionResult(result);
    }

    private ActionResult<T> ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return ToActionResult(result.Error!);
    }

    private IActionResult ToActionResult(ServiceResult result) => ToActionResult(result.Error!);

    private ObjectResult ToActionResult(ServiceError error)
    {
        var statusCode = error.Type switch
        {
            ServiceErrorType.BadRequest => StatusCodes.Status400BadRequest,
            ServiceErrorType.NotFound => StatusCodes.Status404NotFound,
            ServiceErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problem(title: GetProblemTitle(error.Type), detail: error.Message, statusCode: statusCode, type: $"https://httpstatuses.com/{statusCode}");
    }

    private static string GetProblemTitle(ServiceErrorType errorType)
    {
        return errorType switch
        {
            ServiceErrorType.BadRequest => "Invalid request",
            ServiceErrorType.NotFound => "Resource not found",
            ServiceErrorType.Conflict => "Conflict",
            _ => "Unexpected error"
        };
    }
}
