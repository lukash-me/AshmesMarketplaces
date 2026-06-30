using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.MarketplaceCategories.Dtos;
using AshmesMarketplaces.Application.MarketplaceCategories.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Route("api/v1/marketplace-categories")]
[Produces("application/json")]
public sealed class MarketplaceCategoriesController : ControllerBase
{
    private readonly IWildberriesCategoryCatalogService _wildberriesCategoryCatalogService;

    public MarketplaceCategoriesController(IWildberriesCategoryCatalogService wildberriesCategoryCatalogService)
    {
        _wildberriesCategoryCatalogService = wildberriesCategoryCatalogService;
    }

    [HttpGet("wildberries/tree")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WildberriesCategoryCatalogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WildberriesCategoryCatalogDto>> GetWildberriesTree(CancellationToken cancellationToken)
    {
        var result = await _wildberriesCategoryCatalogService.GetTreeAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("wildberries/leaves")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<WildberriesCategoryNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<WildberriesCategoryNodeDto>>> GetWildberriesLeaves(
        CancellationToken cancellationToken)
    {
        var result = await _wildberriesCategoryCatalogService.GetLeavesAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("wildberries/search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<WildberriesCategoryNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<WildberriesCategoryNodeDto>>> SearchWildberriesLeaves(
        [FromQuery] WildberriesCategorySearchQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _wildberriesCategoryCatalogService.SearchAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    private ActionResult<T> ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        var error = result.Error!;
        var statusCode = error.Type switch
        {
            ServiceErrorType.BadRequest => StatusCodes.Status400BadRequest,
            ServiceErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problem(
            title: error.Type == ServiceErrorType.BadRequest ? "Invalid request" : "Marketplace categories unavailable",
            detail: error.Message,
            statusCode: statusCode,
            type: $"https://httpstatuses.com/{statusCode}");
    }
}
