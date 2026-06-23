using ERP.Application.Features.Farm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class FarmController : ApiControllerBase
{
    // ---- Feed master ----
    [HttpGet("~/api/v1/farm/feeds")]
    public async Task<IActionResult> Feeds(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetFeedsQuery(), ct) });

    [HttpPost("~/api/v1/farm/feeds")]
    public async Task<IActionResult> CreateFeed([FromBody] CreateFeedCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpDelete("~/api/v1/farm/feeds/{id:guid}")]
    public async Task<IActionResult> DeleteFeed(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteFeedCommand(id), ct));

    // ---- Wallet ----
    [HttpGet("~/api/v1/farm/wallet")]
    public async Task<IActionResult> Wallet(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetWalletQuery(), ct) });

    [HttpGet("~/api/v1/farm/wallet/transactions")]
    public async Task<IActionResult> WalletTransactions(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetWalletTransactionsQuery(), ct) });

    [HttpPost("~/api/v1/farm/wallet/add")]
    public async Task<IActionResult> AddMoney([FromBody] WalletAmountRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new AddWalletMoneyCommand(body.Amount, body.Reason, body.Date), ct));

    [HttpPost("~/api/v1/farm/wallet/use")]
    public async Task<IActionResult> UseMoney([FromBody] WalletAmountRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new UseWalletMoneyCommand(body.Amount, body.Reason, body.Date), ct));

    // ---- Reports ----
    [HttpGet("~/api/v1/farm/reports/batch-profit")]
    public async Task<IActionResult> BatchProfit([FromQuery] string? status, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetBatchProfitReportQuery(status), ct) });

    [HttpGet("~/api/v1/farm/reports/feed-consumption")]
    public async Task<IActionResult> FeedConsumption([FromQuery] Guid? batchId, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetFeedConsumptionQuery(batchId), ct) });

    [HttpGet("~/api/v1/farm/reports/summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetFarmProfitSummaryQuery(), ct) });
}

public record WalletAmountRequest(decimal Amount, string? Reason, DateOnly? Date);
