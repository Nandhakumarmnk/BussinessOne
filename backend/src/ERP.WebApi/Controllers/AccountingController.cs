using ERP.Application.Features.Accounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class AccountingController : ApiControllerBase   // → /api/v1/accounting
{
    [HttpGet("cash-book")]
    public async Task<IActionResult> CashBook([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCashBookQuery(from, to), ct) });

    [HttpGet("profit-loss")]
    public async Task<IActionResult> ProfitLoss([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetProfitLossQuery(from, to), ct) });

    [HttpGet("credit-tracking")]
    public async Task<IActionResult> CreditTracking(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCreditTrackingQuery(), ct) });

    [HttpGet("collection-tracking")]
    public async Task<IActionResult> CollectionTracking([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetCollectionTrackingQuery(from, to), ct) });

    // ---- General ledger ----
    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetAccountsQuery(), ct) });

    [HttpGet("journal")]
    public async Task<IActionResult> Journal([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetJournalQuery(from, to), ct) });

    [HttpGet("ledger")]
    public async Task<IActionResult> Ledger(
        [FromQuery] Guid? accountId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetLedgerQuery(accountId, from, to), ct) });

    [HttpPost("journal")]
    public async Task<IActionResult> PostJournal([FromBody] PostJournalCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));
}
