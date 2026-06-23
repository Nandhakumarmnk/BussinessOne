using ERP.Application.Features.Reference;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

/// <summary>Read-only reference data for building UI (business types, roles, permissions).</summary>
[Authorize]
public class ReferenceController : ApiControllerBase
{
    [HttpGet("~/api/v1/business-types")]
    public async Task<IActionResult> BusinessTypes(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetBusinessTypesQuery(), ct) });

    [HttpGet("~/api/v1/roles")]
    public async Task<IActionResult> Roles(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetRolesQuery(), ct) });

    [HttpGet("~/api/v1/permissions")]
    public async Task<IActionResult> Permissions(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetPermissionsQuery(), ct) });
}
