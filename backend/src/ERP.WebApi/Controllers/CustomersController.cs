using ERP.Application.Features.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class CustomersController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCustomersQuery(), ct) });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCustomerQuery(id), ct) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateCustomerCommand(
            id, body.Name, body.Mobile, body.Address, body.GstNumber, body.CreditLimit), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteCustomerCommand(id), ct));

    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> Ledger(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCustomerLedgerQuery(id, from, to), ct) });

    [HttpGet("{id:guid}/collections")]
    public async Task<IActionResult> CustomerCollections(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCollectionsQuery(null, null), ct) });

    [HttpPost("{id:guid}/collections")]
    public async Task<IActionResult> RecordCollection(Guid id, [FromBody] RecordCollectionRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new RecordCollectionCommand(
            id, body.CollectionDate, body.Amount, body.Mode, body.Reference), ct));

    [HttpGet("~/api/v1/collections")]
    public async Task<IActionResult> Collections([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCollectionsQuery(from, to), ct) });

    [HttpGet("~/api/v1/reports/outstanding")]
    public async Task<IActionResult> Outstanding(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetOutstandingQuery(), ct) });
}

public record UpdateCustomerRequest(string Name, string? Mobile, string? Address, string? GstNumber, decimal CreditLimit);
public record RecordCollectionRequest(DateOnly CollectionDate, decimal Amount, string Mode, string? Reference);
