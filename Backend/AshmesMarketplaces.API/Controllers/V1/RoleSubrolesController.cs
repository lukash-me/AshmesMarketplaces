using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RoleSubroles.Dtos;
using AshmesMarketplaces.Application.RoleSubroles.Services;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Route("api/v1/role-subroles")]
[Produces("application/json")]
public sealed class RoleSubrolesController : ControllerBase
{
    private readonly IRoleSubroleService _roleSubroleService;

    public RoleSubrolesController(IRoleSubroleService roleSubroleService)
    {
        _roleSubroleService = roleSubroleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<RoleSubroleListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<RoleSubroleListItemResponse>>> GetList([FromQuery] RoleSubroleListQuery query, CancellationToken cancellationToken)
    {
        var result = await _roleSubroleService.GetListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{idRole:guid}/{idSubrole:guid}")]
    [ProducesResponseType(typeof(RoleSubroleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleSubroleResponse>> GetById(Guid idRole, Guid idSubrole, CancellationToken cancellationToken)
    {
        var result = await _roleSubroleService.GetByIdAsync(idRole, idSubrole, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RoleSubroleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleSubroleResponse>> Create([FromBody] CreateRoleSubroleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roleSubroleService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result);

        var response = result.Value!;
        return CreatedAtAction(nameof(GetById), new { idRole = response.IdRole, idSubrole = response.IdSubrole }, response);
    }

    [HttpDelete("{idRole:guid}/{idSubrole:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid idRole, Guid idSubrole, CancellationToken cancellationToken)
    {
        var result = await _roleSubroleService.DeleteAsync(idRole, idSubrole, cancellationToken);
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
