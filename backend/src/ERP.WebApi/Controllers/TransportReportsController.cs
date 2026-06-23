using ERP.Application.Features.Transport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class TransportReportsController : ApiControllerBase
{
    [HttpGet("~/api/v1/transport/reports/vehicle-profit")]
    public async Task<IActionResult> VehicleProfit([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetVehicleProfitReportQuery(from, to), ct) });

    [HttpGet("~/api/v1/transport/reports/driver-profit")]
    public async Task<IActionResult> DriverProfit([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetDriverProfitReportQuery(from, to), ct) });

    [HttpGet("~/api/v1/transport/reports/profit")]
    public async Task<IActionResult> Profit(
        [FromQuery] string period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetTransportProfitReportQuery(period ?? "monthly", from, to), ct) });

    [HttpGet("~/api/v1/transport/reports/outstanding")]
    public async Task<IActionResult> Outstanding(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetTransportOutstandingQuery(), ct) });
}
