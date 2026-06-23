using ERP.Application.Features.Coconut;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class CoconutController : ApiControllerBase
{
    // ---- Product master ----
    [HttpGet("~/api/v1/coconut/products")]
    public async Task<IActionResult> Products(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetProductsQuery(), ct) });

    [HttpPost("~/api/v1/coconut/products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpDelete("~/api/v1/coconut/products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteProductCommand(id), ct));

    // ---- Reports ----
    [HttpGet("~/api/v1/coconut/reports/batch-profit")]
    public async Task<IActionResult> BatchProfit([FromQuery] string? status, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCoconutBatchProfitReportQuery(status), ct) });

    [HttpGet("~/api/v1/coconut/reports/product-profit")]
    public async Task<IActionResult> ProductProfit(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetProductProfitReportQuery(), ct) });

    [HttpGet("~/api/v1/coconut/reports/profit")]
    public async Task<IActionResult> Profit([FromQuery] string period, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCoconutProfitByPeriodQuery(period ?? "monthly"), ct) });
}
