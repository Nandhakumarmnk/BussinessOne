using ERP.Application.Features.Coconut;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class CoconutBatchesController : ApiControllerBase
{
    [HttpGet("~/api/v1/coconut/batches")]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCoconutBatchesQuery(status), ct) });

    [HttpPost("~/api/v1/coconut/batches")]
    public async Task<IActionResult> Create([FromBody] CreateCoconutBatchCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("~/api/v1/coconut/batches/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCoconutBatchRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateCoconutBatchCommand(
            id, body.PurchaseDate, body.Quantity, body.PurchaseAmount, body.Status), ct));

    [HttpDelete("~/api/v1/coconut/batches/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteCoconutBatchCommand(id), ct));

    [HttpGet("~/api/v1/coconut/batches/{id:guid}/pnl")]
    public async Task<IActionResult> Pnl(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCoconutBatchPnlQuery(id), ct) });

    // ---- Labour charges ----
    [HttpGet("~/api/v1/coconut/batches/{id:guid}/labour-charges")]
    public async Task<IActionResult> Labour(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetLabourChargesQuery(id), ct) });

    [HttpPost("~/api/v1/coconut/batches/{id:guid}/labour-charges")]
    public async Task<IActionResult> AddLabour(Guid id, [FromBody] AddLabourChargeRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new AddLabourChargeCommand(id, body.LabourName, body.Amount, body.ChargeDate), ct));

    // ---- Transport charges ----
    [HttpGet("~/api/v1/coconut/batches/{id:guid}/transport-charges")]
    public async Task<IActionResult> Transport(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetTransportChargesQuery(id), ct) });

    [HttpPost("~/api/v1/coconut/batches/{id:guid}/transport-charges")]
    public async Task<IActionResult> AddTransport(Guid id, [FromBody] AddTransportChargeRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new AddTransportChargeCommand(id, body.Vehicle, body.Amount, body.ChargeDate), ct));

    // ---- Sales ----
    [HttpGet("~/api/v1/coconut/batches/{id:guid}/sales")]
    public async Task<IActionResult> Sales(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCoconutSalesQuery(id), ct) });

    [HttpPost("~/api/v1/coconut/batches/{id:guid}/sales")]
    public async Task<IActionResult> AddSale(Guid id, [FromBody] AddCoconutSaleRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new AddCoconutSaleCommand(id, body.SaleDate, body.SaleQuantity, body.SaleValue, body.CustomerId), ct));
}

public record UpdateCoconutBatchRequest(DateOnly PurchaseDate, decimal Quantity, decimal PurchaseAmount, string Status);
public record AddLabourChargeRequest(string? LabourName, decimal Amount, DateOnly ChargeDate);
public record AddTransportChargeRequest(string? Vehicle, decimal Amount, DateOnly ChargeDate);
public record AddCoconutSaleRequest(DateOnly SaleDate, decimal SaleQuantity, decimal SaleValue, Guid? CustomerId);
