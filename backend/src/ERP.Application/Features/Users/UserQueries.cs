using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Users;

public record UserDto(Guid Id, string FullName, string Mobile, string? Email, bool IsActive, bool IsSuperAdmin);

public record GetUsersQuery : IRequest<IReadOnlyList<UserDto>>;
public record GetUserQuery(Guid Id) : IRequest<UserDto>;

internal static class UserGuards
{
    public static Task<Guid> EnsureTenantOwnerAsync(ICurrentUser user, CancellationToken ct)
        => AccessGuard.RequireTenantOwnerAsync(user, ct);
}

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IRepository<User> _users;

    public GetUsersQueryHandler(ICurrentUser currentUser, IRepository<User> users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<IReadOnlyList<UserDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var tenantId = await UserGuards.EnsureTenantOwnerAsync(_currentUser, ct);

        return await _users.Query()
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(u.Id, u.FullName, u.Mobile, u.Email, u.IsActive, u.IsSuperAdmin))
            .ToListAsync(ct);
    }
}

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto>
{
    private readonly ICurrentUser _currentUser;
    private readonly IRepository<User> _users;

    public GetUserQueryHandler(ICurrentUser currentUser, IRepository<User> users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct)
    {
        var tenantId = await UserGuards.EnsureTenantOwnerAsync(_currentUser, ct);

        return await _users.Query()
            .Where(u => u.Id == request.Id && u.TenantId == tenantId)
            .Select(u => new UserDto(u.Id, u.FullName, u.Mobile, u.Email, u.IsActive, u.IsSuperAdmin))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("User not found.");
    }
}
