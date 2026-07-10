using ERP.Application.Features.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class ExpensesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? typeId, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetExpensesQuery(from, to, typeId), ct) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpenseRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateExpenseCommand(
            id, body.ExpenseTypeId, body.ExpenseDate, body.Amount, body.Description, body.AttachmentKey), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteExpenseCommand(id), ct));

    /// <summary>
    /// Returns a time-limited download URL for the expense's attachment. Ownership is enforced through
    /// the business query filter, so an expense in another business resolves as 404.
    /// </summary>
    [HttpGet("{id:guid}/attachment")]
    public async Task<IActionResult> Attachment(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new GetExpenseAttachmentUrlQuery(id), ct));

    [HttpGet("~/api/v1/reports/expenses")]
    public async Task<IActionResult> Report(
        [FromQuery] string period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetExpenseReportQuery(period ?? "daily", from, to), ct) });

    // ---- Expense types ----

    [HttpGet("~/api/v1/expense-types")]
    public async Task<IActionResult> Types(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetExpenseTypesQuery(), ct) });

    [HttpPost("~/api/v1/expense-types")]
    public async Task<IActionResult> CreateType([FromBody] CreateExpenseTypeCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpDelete("~/api/v1/expense-types/{id:guid}")]
    public async Task<IActionResult> DeleteType(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteExpenseTypeCommand(id), ct));
}

public record UpdateExpenseRequest(
    Guid? ExpenseTypeId, DateOnly ExpenseDate, decimal Amount, string? Description, string? AttachmentKey);
