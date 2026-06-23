using ERP.Application.Features.Cctv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class ItemsController : ApiControllerBase
{
    [HttpGet("~/api/v1/cctv/items")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetItemsQuery(), ct) });

    [HttpPost("~/api/v1/cctv/items")]
    public async Task<IActionResult> Create([FromBody] CreateItemCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("~/api/v1/cctv/items/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateItemRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateItemCommand(
            id, body.ItemCode, body.ItemName, body.Uom, body.HsnCode, body.Rate, body.TaxPercentage,
            body.ReorderLevel, body.IsActive), ct));

    [HttpDelete("~/api/v1/cctv/items/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteItemCommand(id), ct));

    // ---- Suppliers ----

    [HttpGet("~/api/v1/cctv/suppliers")]
    public async Task<IActionResult> Suppliers(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetSuppliersQuery(), ct) });

    [HttpPost("~/api/v1/cctv/suppliers")]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("~/api/v1/cctv/suppliers/{id:guid}")]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] UpdateSupplierRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateSupplierCommand(id, body.Name, body.Mobile, body.GstNumber, body.Address), ct));

    [HttpDelete("~/api/v1/cctv/suppliers/{id:guid}")]
    public async Task<IActionResult> DeleteSupplier(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteSupplierCommand(id), ct));
}

public record UpdateItemRequest(
    string ItemCode, string ItemName, string Uom, string? HsnCode,
    decimal Rate, decimal TaxPercentage, decimal ReorderLevel, bool IsActive);
public record UpdateSupplierRequest(string Name, string? Mobile, string? GstNumber, string? Address);
