using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Workspaces.Dtos;
using AshmesMarketplaces.Application.Workspaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/management/workspaces")]
[Produces("application/json")]
public sealed class ManagementWorkspacesController : ControllerBase
{
    private readonly IManagementWorkspaceService _workspaceService;

    public ManagementWorkspacesController(IManagementWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManagementWorkspaceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<ManagementWorkspaceResponse>>> GetList(
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.GetListAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManagementWorkspaceRoleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<ManagementWorkspaceRoleResponse>>> GetRoles(
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.GetRolesAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ManagementWorkspaceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ManagementWorkspaceResponse>> Create(
        [FromBody] CreateManagementWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result);

        var response = result.Value!;
        return CreatedAtAction(nameof(GetList), new { id = response.Id }, response);
    }

    [HttpPut("{workspaceId:guid}")]
    [ProducesResponseType(typeof(ManagementWorkspaceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagementWorkspaceResponse>> Update(
        Guid workspaceId,
        [FromBody] UpdateManagementWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.UpdateAsync(workspaceId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{workspaceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.DeleteAsync(workspaceId, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result.Error!);

        return NoContent();
    }

    [HttpPost("{workspaceId:guid}/members")]
    [ProducesResponseType(typeof(ManagementWorkspaceMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ManagementWorkspaceMemberResponse>> AddMember(
        Guid workspaceId,
        [FromBody] AddManagementWorkspaceMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.AddMemberAsync(workspaceId, request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{workspaceId:guid}/members/{userId:guid}/role")]
    [ProducesResponseType(typeof(ManagementWorkspaceMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ManagementWorkspaceMemberResponse>> UpdateMemberRole(
        Guid workspaceId,
        Guid userId,
        [FromBody] UpdateManagementWorkspaceMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.UpdateMemberRoleAsync(workspaceId, userId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{workspaceId:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMember(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceService.DeleteMemberAsync(workspaceId, userId, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result.Error!);

        return NoContent();
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
