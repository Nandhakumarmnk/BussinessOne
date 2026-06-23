using ERP.Application.Features.Cctv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class SalesController : ApiControllerBase
{
    [HttpGet("~/api/v1/cctv/sales")]
    public async Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetSalesQuery(from, to), ct) });

    [HttpGet("~/api/v1/cctv/sales/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetSaleQuery(id), ct) });

    [HttpPost("~/api/v1/cctv/sales")]
    public async Task<IActionResult> Create([FromBody] CreateSaleCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));
}
