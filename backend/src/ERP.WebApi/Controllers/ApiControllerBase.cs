using ERP.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>Wraps a successful value in the { data } envelope; maps failures to status + { error }.</summary>
    protected IActionResult FromResult<T>(Result<T> result)
    {
        if (result.Succeeded)
            return Ok(new { data = result.Value });

        var status = MapStatus(result.Code);
        return StatusCode(status, new { error = new { code = result.Code, message = result.Message } });
    }

    /// <summary>Non-generic result → 204 No Content on success, error envelope otherwise.</summary>
    protected IActionResult FromResult(Result result)
    {
        if (result.Succeeded)
            return NoContent();

        var status = MapStatus(result.Code);
        return StatusCode(status, new { error = new { code = result.Code, message = result.Message } });
    }

    private static int MapStatus(string? code) => code switch
    {
        "auth.invalid_credentials" => StatusCodes.Status401Unauthorized,
        "auth.forbidden" => StatusCodes.Status403Forbidden,
        "resource.not_found" => StatusCodes.Status404NotFound,
        "resource.conflict" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
