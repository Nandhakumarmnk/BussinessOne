using ERP.Application.Features.Transport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class VehiclesController : ApiControllerBase
{
    [HttpGet("~/api/v1/transport/vehicles")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetVehiclesQuery(), ct) });

    [HttpPost("~/api/v1/transport/vehicles")]
    public async Task<IActionResult> Create([FromBody] CreateVehicleCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("~/api/v1/transport/vehicles/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVehicleRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateVehicleCommand(
            id, body.VehicleNumber, body.VehicleType, body.Model, body.FuelType,
            body.RcDetails, body.InsuranceDetails, body.InsuranceExpiry, body.IsActive), ct));

    [HttpDelete("~/api/v1/transport/vehicles/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteVehicleCommand(id), ct));
}

public record UpdateVehicleRequest(
    string VehicleNumber, string? VehicleType, string? Model, string? FuelType,
    string? RcDetails, string? InsuranceDetails, DateOnly? InsuranceExpiry, bool IsActive);
