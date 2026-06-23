using ERP.Application.Features.Transport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class LoadsController : ApiControllerBase
{
    [HttpGet("~/api/v1/transport/loads")]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? vehicleId, [FromQuery] Guid? driverId, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetLoadsQuery(from, to, vehicleId, driverId), ct) });

    [HttpGet("~/api/v1/transport/loads/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetLoadQuery(id), ct) });

    [HttpPost("~/api/v1/transport/loads")]
    public async Task<IActionResult> Create([FromBody] CreateLoadCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("~/api/v1/transport/loads/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLoadRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateLoadCommand(
            id, body.LoadName, body.VehicleId, body.DriverId, body.Source, body.Destination, body.LoadDate,
            body.LoadAmount, body.LoadmanCharges, body.FuelExpense, body.MaintenanceExpense,
            body.DriverCharges, body.OtherExpense, body.Status), ct));

    [HttpDelete("~/api/v1/transport/loads/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteLoadCommand(id), ct));

    // ---- Credits ----

    [HttpGet("~/api/v1/transport/credits")]
    public async Task<IActionResult> Credits([FromQuery] string? status, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCreditsQuery(status), ct) });

    [HttpPatch("~/api/v1/transport/credits/{id:guid}/payment")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] CreditPaymentRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new RecordCreditPaymentCommand(id, body.Amount, body.Mode, body.PaymentDate), ct));
}

public record UpdateLoadRequest(
    string? LoadName, Guid? VehicleId, Guid? DriverId, string? Source, string? Destination, DateOnly LoadDate,
    decimal LoadAmount, decimal LoadmanCharges, decimal FuelExpense, decimal MaintenanceExpense,
    decimal DriverCharges, decimal OtherExpense, string Status);

public record CreditPaymentRequest(decimal Amount, string Mode, DateOnly? PaymentDate);
