using ERP.Application.Features.Cctv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class CctvReportsController : ApiControllerBase
{
    [HttpGet("~/api/v1/cctv/reports/item-sales")]
    public async Task<IActionResult> ItemSales([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetItemSalesReportQuery(from, to), ct) });

    [HttpGet("~/api/v1/cctv/reports/revenue")]
    public async Task<IActionResult> Revenue(
        [FromQuery] string period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCctvRevenueReportQuery(period ?? "monthly", from, to), ct) });

    [HttpGet("~/api/v1/cctv/reports/service")]
    public async Task<IActionResult> Service(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetServiceReportQuery(), ct) });

    [HttpGet("~/api/v1/cctv/reports/credit-outstanding")]
    public async Task<IActionResult> Outstanding(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCctvOutstandingQuery(), ct) });
}
