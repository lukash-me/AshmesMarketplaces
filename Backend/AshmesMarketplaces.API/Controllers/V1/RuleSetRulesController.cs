using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RuleSetRules.Dtos;
using AshmesMarketplaces.Application.RuleSetRules.Services;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Route("api/v1/rule-set-rules")]
[Produces("application/json")]
public sealed class RuleSetRulesController : ControllerBase
{
    private readonly IRuleSetRuleService _ruleSetRuleService;

    public RuleSetRulesController(IRuleSetRuleService ruleSetRuleService)
    {
        _ruleSetRuleService = ruleSetRuleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<RuleSetRuleListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<RuleSetRuleListItemResponse>>> GetList([FromQuery] RuleSetRuleListQuery query, CancellationToken cancellationToken)
    {
        var result = await _ruleSetRuleService.GetListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{idSet:guid}/{idRule:guid}")]
    [ProducesResponseType(typeof(RuleSetRuleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuleSetRuleResponse>> GetById(Guid idSet, Guid idRule, CancellationToken cancellationToken)
    {
        var result = await _ruleSetRuleService.GetByIdAsync(idSet, idRule, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RuleSetRuleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleSetRuleResponse>> Create([FromBody] CreateRuleSetRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await _ruleSetRuleService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result);

        var response = result.Value!;
        return CreatedAtAction(nameof(GetById), new { idSet = response.IdSet, idRule = response.IdRule }, response);
    }

    [HttpDelete("{idSet:guid}/{idRule:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid idSet, Guid idRule, CancellationToken cancellationToken)
    {
        var result = await _ruleSetRuleService.DeleteAsync(idSet, idRule, cancellationToken);
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
