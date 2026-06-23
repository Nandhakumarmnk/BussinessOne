using ERP.Application.Features.Farm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class FarmBatchesController : ApiControllerBase
{
    [HttpGet("~/api/v1/farm/batches")]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetBatchesQuery(status), ct) });

    [HttpPost("~/api/v1/farm/batches")]
    public async Task<IActionResult> Create([FromBody] CreateBatchCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("~/api/v1/farm/batches/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBatchRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateBatchCommand(
            id, body.BatchName, body.AnimalType, body.StartDate, body.QuantityPurchased, body.PurchaseAmount, body.Status), ct));

    [HttpDelete("~/api/v1/farm/batches/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteBatchCommand(id), ct));

    [HttpGet("~/api/v1/farm/batches/{id:guid}/pnl")]
    public async Task<IActionResult> Pnl(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetBatchPnlQuery(id), ct) });

    // ---- Feed entries ----
    [HttpGet("~/api/v1/farm/batches/{id:guid}/feed-entries")]
    public async Task<IActionResult> FeedEntries(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetFeedEntriesQuery(id), ct) });

    [HttpPost("~/api/v1/farm/batches/{id:guid}/feed-entries")]
    public async Task<IActionResult> AddFeedEntry(Guid id, [FromBody] AddFeedEntryRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new AddFeedEntryCommand(id, body.FeedId, body.EntryDate, body.Quantity, body.Rate), ct));

    // ---- Medical ----
    [HttpGet("~/api/v1/farm/batches/{id:guid}/medical")]
    public async Task<IActionResult> Medical(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetMedicalRecordsQuery(id), ct) });

    [HttpPost("~/api/v1/farm/batches/{id:guid}/medical")]
    public async Task<IActionResult> AddMedical(Guid id, [FromBody] AddMedicalRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new AddMedicalRecordCommand(id, body.MedicineName, body.Amount, body.DoctorCharges, body.RecordDate), ct));

    // ---- Batch expenses ----
    [HttpGet("~/api/v1/farm/batches/{id:guid}/expenses")]
    public async Task<IActionResult> Expenses(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetBatchExpensesQuery(id), ct) });

    [HttpPost("~/api/v1/farm/batches/{id:guid}/expenses")]
    public async Task<IActionResult> AddExpense(Guid id, [FromBody] AddBatchExpenseRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new AddBatchExpenseCommand(id, body.ExpenseKind, body.Amount, body.ExpenseDate, body.Description), ct));

    // ---- Batch sales ----
    [HttpGet("~/api/v1/farm/batches/{id:guid}/sales")]
    public async Task<IActionResult> Sales(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetBatchSalesQuery(id), ct) });

    [HttpPost("~/api/v1/farm/batches/{id:guid}/sales")]
    public async Task<IActionResult> AddSale(Guid id, [FromBody] AddBatchSaleRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new AddBatchSaleCommand(id, body.SaleDate, body.SaleQuantity, body.TotalWeight, body.SaleAmount, body.CustomerId), ct));
}

public record UpdateBatchRequest(string? BatchName, string AnimalType, DateOnly StartDate, int QuantityPurchased, decimal PurchaseAmount, string Status);
public record AddFeedEntryRequest(Guid FeedId, DateOnly EntryDate, decimal Quantity, decimal Rate);
public record AddMedicalRequest(string MedicineName, decimal Amount, decimal DoctorCharges, DateOnly RecordDate);
public record AddBatchExpenseRequest(string ExpenseKind, decimal Amount, DateOnly ExpenseDate, string? Description);
public record AddBatchSaleRequest(DateOnly SaleDate, int SaleQuantity, decimal? TotalWeight, decimal SaleAmount, Guid? CustomerId);
