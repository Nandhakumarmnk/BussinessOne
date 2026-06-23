using ERP.Application.Features.Cctv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class PurchaseOrdersController : ApiControllerBase
{
    [HttpGet("~/api/v1/cctv/purchase-orders")]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetPurchaseOrdersQuery(status), ct) });

    [HttpGet("~/api/v1/cctv/purchase-orders/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetPurchaseOrderQuery(id), ct) });

    [HttpPost("~/api/v1/cctv/purchase-orders")]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPost("~/api/v1/cctv/purchase-orders/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new SubmitPurchaseOrderCommand(id), ct));

    [HttpPost("~/api/v1/cctv/purchase-orders/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new ApprovePurchaseOrderCommand(id), ct));

    [HttpPost("~/api/v1/cctv/purchase-orders/{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new ReceivePurchaseOrderCommand(id), ct));

    [HttpPost("~/api/v1/cctv/purchase-orders/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new CancelPurchaseOrderCommand(id), ct));
}
