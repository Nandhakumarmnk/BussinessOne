using ERP.Application.Features.Transport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class DriversController : ApiControllerBase
{
    [HttpGet("~/api/v1/transport/drivers")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetDriversQuery(), ct) });

    [HttpPost("~/api/v1/transport/drivers")]
    public async Task<IActionResult> Create([FromBody] CreateDriverCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("~/api/v1/transport/drivers/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDriverRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateDriverCommand(
            id, body.Name, body.Mobile, body.DriverType, body.Salary, body.IsActive), ct));

    [HttpDelete("~/api/v1/transport/drivers/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteDriverCommand(id), ct));
}

public record UpdateDriverRequest(string Name, string? Mobile, string DriverType, decimal Salary, bool IsActive);
