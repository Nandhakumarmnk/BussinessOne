using ERP.Application.Features.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class ReportsController : ApiControllerBase
{
    /// <summary>
    /// Generate a report as PDF or Excel and download it. Supported keys:
    /// expenses, collections, credit-outstanding, profit-loss.
    /// </summary>
    [HttpPost("~/api/v1/reports/export")]
    public async Task<IActionResult> Export([FromBody] ExportReportRequest body, CancellationToken ct)
    {
        var file = await Mediator.Send(
            new ExportReportQuery(body.ReportKey, body.Format, body.From, body.To), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}

public record ExportReportRequest(string ReportKey, string Format, DateOnly? From, DateOnly? To);
