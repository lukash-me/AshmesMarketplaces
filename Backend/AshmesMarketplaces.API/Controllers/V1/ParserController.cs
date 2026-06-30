using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ParserBatches.Dtos;
using AshmesMarketplaces.Application.ParserBatches.Services;
using AshmesMarketplaces.Application.ParserObservability.Dtos;
using AshmesMarketplaces.Application.ParserObservability.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Route("api/v1/parser")]
[Produces("application/json")]
public sealed class ParserController : ControllerBase
{
    private readonly IParserProductReadService _productReadService;
    private readonly IParserReviewReadService _reviewReadService;
    private readonly IParserRunReadService _runReadService;
    private readonly IParserObservedStockDecreaseReadService _observedStockDecreaseReadService;
    private readonly IParserObservedMarketEventReadService _observedMarketEventReadService;
    private readonly IPublicProductAvailabilityReadService _productAvailabilityReadService;
    private readonly IParserBatchQueueService _parserBatchQueueService;
    private readonly IParserProxyRunService _parserProxyRunService;

    public ParserController(
        IParserProductReadService productReadService,
        IParserReviewReadService reviewReadService,
        IParserRunReadService runReadService,
        IParserObservedStockDecreaseReadService observedStockDecreaseReadService,
        IParserObservedMarketEventReadService observedMarketEventReadService,
        IPublicProductAvailabilityReadService productAvailabilityReadService,
        IParserBatchQueueService parserBatchQueueService,
        IParserProxyRunService parserProxyRunService)
    {
        _productReadService = productReadService;
        _reviewReadService = reviewReadService;
        _runReadService = runReadService;
        _observedStockDecreaseReadService = observedStockDecreaseReadService;
        _observedMarketEventReadService = observedMarketEventReadService;
        _productAvailabilityReadService = productAvailabilityReadService;
        _parserBatchQueueService = parserBatchQueueService;
        _parserProxyRunService = parserProxyRunService;
    }

    [HttpPost("proxy-runs/start")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserProxyRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParserProxyRunResponse>> StartProxyRun(
        [FromBody] ParserProxyRunStartRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _parserProxyRunService.StartAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result.Error!);

        return Accepted(result.Value);
    }

    [HttpPatch("proxy-runs/{externalProxyRunId}/progress")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserProxyRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParserProxyRunResponse>> UpdateProxyRunProgress(
        string externalProxyRunId,
        [FromBody] ParserProxyRunProgressRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _parserProxyRunService.UpdateProgressAsync(
            externalProxyRunId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("proxy-runs/{externalProxyRunId}/finish")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserProxyRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParserProxyRunResponse>> FinishProxyRun(
        string externalProxyRunId,
        [FromBody] ParserProxyRunFinishRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _parserProxyRunService.FinishAsync(
            externalProxyRunId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("batches")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserBatchSubmitResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParserBatchSubmitResponse>> SubmitBatch(
        [FromBody] ParserBatchSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _parserBatchQueueService.SubmitAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return ToActionResult(result.Error!);

        return Accepted(result.Value);
    }

    [HttpGet("batches/{externalBatchId}/status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserBatchStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParserBatchStatusResponse>> GetBatchStatus(
        string externalBatchId,
        CancellationToken cancellationToken)
    {
        var result = await _parserBatchQueueService.GetStatusAsync(externalBatchId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("instances/{parserInstanceId}/pending-acks")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserPendingAckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParserPendingAckResponse>> GetPendingAcks(
        string parserInstanceId,
        CancellationToken cancellationToken)
    {
        var result = await _parserBatchQueueService.GetPendingAcksAsync(parserInstanceId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResponse<ParserProductListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ParserProductListItemDto>>> GetProducts(
        [FromQuery] ParserProductListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _productReadService.GetListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("products/filter-options")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserProductFilterOptionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParserProductFilterOptionsDto>> GetProductFilterOptions(
        [FromQuery] ParserProductFilterOptionsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _productReadService.GetFilterOptionsAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("products/demo-card-options")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserDemoCardOptionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParserDemoCardOptionsDto>> GetDemoCardOptions(
        [FromQuery] bool? includeCharacteristics,
        CancellationToken cancellationToken)
    {
        var result = await _productReadService.GetDemoCardOptionsAsync(includeCharacteristics ?? true, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("products/demo-card-options/characteristics")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserDemoCardCharacteristicsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParserDemoCardCharacteristicsDto>> GetDemoCardCharacteristics(
        [FromQuery] string subcategory,
        CancellationToken cancellationToken)
    {
        var result = await _productReadService.GetDemoCardCharacteristicsAsync(subcategory, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("products/logistics-summary")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserProductLogisticsSummaryAggregateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParserProductLogisticsSummaryAggregateDto>> GetProductLogisticsSummary(
        [FromQuery] ParserProductLogisticsSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _productReadService.GetLogisticsSummaryAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("logistics/observed-stock-decreases")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserObservedStockDecreaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParserObservedStockDecreaseResponse>> GetObservedStockDecreases(
        [FromQuery] ParserObservedStockDecreaseQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _observedStockDecreaseReadService.GetListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("logistics/observed-events")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserObservedMarketEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParserObservedMarketEventResponse>> GetObservedMarketEvents(
        [FromQuery] ParserObservedMarketEventQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _observedMarketEventReadService.GetListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("products/availability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResponse<ParserProductListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ParserProductListItemDto>>> GetProductAvailability(
        [FromQuery] ParserProductListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _productAvailabilityReadService.GetAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("products/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParserProductDetailDto>> GetProduct(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _productReadService.GetByIdAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResponse<ParserReviewListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ParserReviewListItemDto>>> GetReviews(
        [FromQuery] ParserReviewListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _reviewReadService.GetListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("reviews/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParserReviewDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParserReviewDetailDto>> GetReview(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _reviewReadService.GetByIdAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("reviews/{id:guid}/replies")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResponse<ParserReviewReplyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ParserReviewReplyDto>>> GetReviewReplies(
        Guid id,
        [FromQuery] ParserReviewReplyListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _reviewReadService.GetRepliesAsync(id, query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("runs")]
    [ProducesResponseType(typeof(PagedResponse<ParserRunSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ParserRunSummaryDto>>> GetRuns(
        [FromQuery] ParserRunListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _runReadService.GetListAsync(query, cancellationToken);
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
            _ => StatusCodes.Status500InternalServerError
        };

        return Problem(
            title: GetProblemTitle(error.Type),
            detail: error.Message,
            statusCode: statusCode,
            type: $"https://httpstatuses.com/{statusCode}");
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
