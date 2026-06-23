using ERP.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class DashboardController : ApiControllerBase
{
    /// <summary>KPI summary for the active business: income/expense (today + month), profit, pending amounts.</summary>
    [HttpGet("~/api/v1/dashboard/summary")]
    public async Task<IActionResult> Summary([FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetDashboardSummaryQuery(asOf), ct) });
}
