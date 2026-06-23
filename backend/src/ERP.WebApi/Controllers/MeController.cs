using ERP.Application.Features.Auth.Me;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
[Route("api/v1/me")]
public class MeController : ApiControllerBase
{
    /// <summary>The authenticated user's profile, businesses and resolved permissions.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetMeQuery(), ct);
        return FromResult(result);
    }
}
