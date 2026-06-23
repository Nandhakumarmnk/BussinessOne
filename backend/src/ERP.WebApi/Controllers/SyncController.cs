using ERP.Application.Features.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

/// <summary>
/// Mobile offline sync. Writes are replayed against the normal create endpoints carrying an
/// <c>Idempotency-Key</c> (deduped by the IdempotencyFilter); this endpoint pulls changed picker
/// masters so the device can refresh its offline caches.
/// </summary>
[Authorize]
public class SyncController : ApiControllerBase
{
    [HttpGet("~/api/v1/sync/pull")]
    public async Task<IActionResult> Pull([FromQuery] DateTime? since, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new SyncPullQuery(since), ct) });
}
