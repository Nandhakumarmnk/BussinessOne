using ERP.Application.Features.Cctv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class ServiceController : ApiControllerBase
{
    [HttpGet("~/api/v1/cctv/service-complaints")]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetServiceComplaintsQuery(status), ct) });

    [HttpPost("~/api/v1/cctv/service-complaints")]
    public async Task<IActionResult> Create([FromBody] CreateServiceComplaintCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPatch("~/api/v1/cctv/service-complaints/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] ServiceStatusRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateServiceStatusCommand(id, body.Status), ct));

    [HttpPatch("~/api/v1/cctv/service-complaints/{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] ServiceAssignRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new AssignServiceComplaintCommand(id, body.EmployeeId), ct));
}

public record ServiceStatusRequest(string Status);
public record ServiceAssignRequest(Guid EmployeeId);
