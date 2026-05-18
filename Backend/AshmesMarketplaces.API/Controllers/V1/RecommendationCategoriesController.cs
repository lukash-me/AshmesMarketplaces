using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.RecommendationCategories.Dtos;
using AshmesMarketplaces.Application.RecommendationCategories.Services;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Route("api/v1/recommendation-categories")]
[Produces("application/json")]
public sealed class RecommendationCategoriesController : ControllerBase
{
    private readonly IRecommendationCategoryService _recommendationCategoryService;

    public RecommendationCategoriesController(IRecommendationCategoryService recommendationCategoryService)
    {
        _recommendationCategoryService = recommendationCategoryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<RecommendationCategoryListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<RecommendationCategoryListItemResponse>>> GetList([FromQuery] RecommendationCategoryListQuery query, CancellationToken cancellationToken)
    {
        var result = await _recommendationCategoryService.GetListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{idRecommendation:guid}/{idCategory:guid}")]
    [ProducesResponseType(typeof(RecommendationCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecommendationCategoryResponse>> GetById(Guid idRecommendation, Guid idCategory, CancellationToken cancellationToken)
    {
        var result = await _recommendationCategoryService.GetByIdAsync(idRecommendation, idCategory, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RecommendationCategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecommendationCategoryResponse>> Create([FromBody] CreateRecommendationCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _recommendationCategoryService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result);

        var response = result.Value!;
        return CreatedAtAction(nameof(GetById), new { idRecommendation = response.IdRecommendation, idCategory = response.IdCategory }, response);
    }

    [HttpDelete("{idRecommendation:guid}/{idCategory:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid idRecommendation, Guid idCategory, CancellationToken cancellationToken)
    {
        var result = await _recommendationCategoryService.DeleteAsync(idRecommendation, idCategory, cancellationToken);
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
