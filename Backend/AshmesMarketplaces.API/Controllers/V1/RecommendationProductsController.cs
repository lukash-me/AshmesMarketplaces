using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RecommendationProducts.Dtos;
using AshmesMarketplaces.Application.RecommendationProducts.Services;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Route("api/v1/recommendation-products")]
[Produces("application/json")]
public sealed class RecommendationProductsController : ControllerBase
{
    private readonly IRecommendationProductService _recommendationProductService;

    public RecommendationProductsController(IRecommendationProductService recommendationProductService)
    {
        _recommendationProductService = recommendationProductService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<RecommendationProductListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<RecommendationProductListItemResponse>>> GetList([FromQuery] RecommendationProductListQuery query, CancellationToken cancellationToken)
    {
        var result = await _recommendationProductService.GetListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{idRecommendation:guid}/{idProduct:guid}")]
    [ProducesResponseType(typeof(RecommendationProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecommendationProductResponse>> GetById(Guid idRecommendation, Guid idProduct, CancellationToken cancellationToken)
    {
        var result = await _recommendationProductService.GetByIdAsync(idRecommendation, idProduct, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RecommendationProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecommendationProductResponse>> Create([FromBody] CreateRecommendationProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _recommendationProductService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result);

        var response = result.Value!;
        return CreatedAtAction(nameof(GetById), new { idRecommendation = response.IdRecommendation, idProduct = response.IdProduct }, response);
    }

    [HttpDelete("{idRecommendation:guid}/{idProduct:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid idRecommendation, Guid idProduct, CancellationToken cancellationToken)
    {
        var result = await _recommendationProductService.DeleteAsync(idRecommendation, idProduct, cancellationToken);
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
