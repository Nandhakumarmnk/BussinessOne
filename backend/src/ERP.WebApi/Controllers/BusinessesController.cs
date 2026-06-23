using ERP.Application.Features.Businesses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WebApi.Controllers;

[Authorize]
public class BusinessesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetBusinessesQuery(), ct) });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetBusinessQuery(id), ct) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBusinessCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBusinessRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(
            new UpdateBusinessCommand(id, body.Name, body.GstNumber, body.Address, body.IsActive), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteBusinessCommand(id), ct));

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> Members(Guid id, CancellationToken ct)
        => Ok(new { data = await Mediator.Send(new GetMembersQuery(id), ct) });

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest body, CancellationToken ct)
        => FromResult(await Mediator.Send(new AddMemberCommand(id, body.UserId, body.RoleCode), ct));

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
        => FromResult(await Mediator.Send(new RemoveMemberCommand(id, userId), ct));
}

public record UpdateBusinessRequest(string Name, string? GstNumber, string? Address, bool IsActive);
public record AddMemberRequest(Guid UserId, string RoleCode);
