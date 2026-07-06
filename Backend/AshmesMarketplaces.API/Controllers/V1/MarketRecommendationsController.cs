using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AshmesMarketplaces.API.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/market/recommendations")]
[Produces("application/json")]
public sealed class MarketRecommendationsController : ControllerBase
{
    [HttpGet("hot-products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public ActionResult GetHotProducts()
    {
        return RetiredHotProductsProblem();
    }

    [HttpPost("hot-products/recalculate")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public ActionResult RecalculateHotProducts()
    {
        return RetiredHotProductsProblem();
    }

    private ObjectResult RetiredHotProductsProblem()
    {
        return Problem(
            title: "Hot products flow retired",
            detail: "Старый расчет перспективных товаров отключен. Используйте страницу «Конструктор правил».",
            statusCode: StatusCodes.Status410Gone,
            type: "https://httpstatuses.com/410");
    }
}
