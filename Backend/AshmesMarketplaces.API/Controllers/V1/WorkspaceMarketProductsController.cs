using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.WorkspaceMarketProducts.Dtos;
using AshmesMarketplaces.Application.WorkspaceMarketProducts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/workspaces/{workspaceId:guid}/market-products")]
[Produces("application/json")]
public sealed class WorkspaceMarketProductsController : ControllerBase
{
    private readonly IWorkspaceMarketProductService _service;

    public WorkspaceMarketProductsController(IWorkspaceMarketProductService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<WorkspaceMarketProductListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<WorkspaceMarketProductListItemResponse>>> GetList(
        Guid workspaceId,
        [FromQuery] WorkspaceMarketProductListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetListAsync(workspaceId, query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceMarketProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceMarketProductResponse>> GetById(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(workspaceId, id, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceMarketProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(WorkspaceMarketProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkspaceMarketProductResponse>> Add(
        Guid workspaceId,
        [FromBody] CreateWorkspaceMarketProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.AddAsync(workspaceId, request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result);

        var response = result.Value!;
        return CreatedAtAction(nameof(GetById), new { workspaceId, id = response.Id }, response);
    }

    [HttpPost("demo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(WorkspaceMarketProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkspaceMarketProductResponse>> AddDemo(
        Guid workspaceId,
        [FromForm] CreateDemoWorkspaceMarketProductRequest request,
        [FromForm] List<IFormFile> media,
        CancellationToken cancellationToken)
    {
        var uploads = media
            .Where(file => file.Length > 0)
            .Select(file => new WorkspaceMarketProductUpload(
                file.OpenReadStream(),
                file.FileName,
                file.ContentType,
                file.Length))
            .ToList();

        try
        {
            var result = await _service.AddDemoAsync(workspaceId, request, uploads, cancellationToken);
            if (!result.IsSuccess)
                return ToActionResult(result);

            var response = result.Value!;
            return CreatedAtAction(nameof(GetById), new { workspaceId, id = response.Id }, response);
        }
        finally
        {
            foreach (var upload in uploads)
            {
                await upload.Content.DisposeAsync();
            }
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceMarketProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceMarketProductResponse>> Update(
        Guid workspaceId,
        Guid id,
        [FromBody] UpdateWorkspaceMarketProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(workspaceId, id, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(workspaceId, id, cancellationToken);
        if (result.IsSuccess)
            return NoContent();

        return ToActionResult(result);
    }

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(WorkspaceMarketProductHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceMarketProductHistoryResponse>> GetHistory(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetHistoryAsync(workspaceId, id, cancellationToken);
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
            ServiceErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ServiceErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ServiceErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
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
            ServiceErrorType.Forbidden => "Forbidden",
            ServiceErrorType.Unauthorized => "Unauthorized",
            ServiceErrorType.Unavailable => "Service unavailable",
            _ => "Unexpected error"
        };
    }
}
