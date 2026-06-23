using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Businesses;

// ---- List members ----

public record GetMembersQuery(Guid BusinessId) : IRequest<IReadOnlyList<MemberDto>>;

public class GetMembersQueryHandler : IRequestHandler<GetMembersQuery, IReadOnlyList<MemberDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityQueries _identity;
    private readonly IRepository<UserBusiness> _memberships;
    private readonly IRepository<User> _users;
    private readonly IRepository<Role> _roles;

    public GetMembersQueryHandler(
        ICurrentUser currentUser, IIdentityQueries identity, IRepository<UserBusiness> memberships,
        IRepository<User> users, IRepository<Role> roles)
    {
        _currentUser = currentUser;
        _identity = identity;
        _memberships = memberships;
        _users = users;
        _roles = roles;
    }

    public async Task<IReadOnlyList<MemberDto>> Handle(GetMembersQuery request, CancellationToken ct)
    {
        await AccessGuard.RequireCanManageMembersAsync(_currentUser, _identity, request.BusinessId, ct);

        return await (
            from m in _memberships.Query().Where(x => x.BusinessId == request.BusinessId)
            join u in _users.Query() on m.UserId equals u.Id
            join r in _roles.Query() on m.RoleId equals r.Id
            orderby u.FullName
            select new MemberDto(u.Id, u.FullName, u.Mobile, r.Code, r.Name)
        ).ToListAsync(ct);
    }
}

// ---- Add member ----

public record AddMemberCommand(Guid BusinessId, Guid UserId, string RoleCode) : IRequest<Result<MemberDto>>;

public class AddMemberCommandValidator : AbstractValidator<AddMemberCommand>
{
    public AddMemberCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RoleCode).NotEmpty();
    }
}

public class AddMemberCommandHandler : IRequestHandler<AddMemberCommand, Result<MemberDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityQueries _identity;
    private readonly IUnitOfWork _uow;

    public AddMemberCommandHandler(ICurrentUser currentUser, IIdentityQueries identity, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _identity = identity;
        _uow = uow;
    }

    public async Task<Result<MemberDto>> Handle(AddMemberCommand request, CancellationToken ct)
    {
        await AccessGuard.RequireCanManageMembersAsync(_currentUser, _identity, request.BusinessId, ct);

        var businessTenant = await _identity.GetBusinessTenantIdAsync(request.BusinessId, ct);
        var user = await _uow.Repository<User>().GetByIdAsync(request.UserId, ct);
        if (user is null || user.TenantId != businessTenant)
            return Result<MemberDto>.Fail("resource.not_found", "User not found in this tenant.");

        var role = await _uow.Repository<Role>().Query().FirstOrDefaultAsync(r => r.Code == request.RoleCode, ct);
        if (role is null)
            return Result<MemberDto>.Fail("resource.not_found", "Unknown role.");

        var already = await _uow.Repository<UserBusiness>().Query()
            .AnyAsync(m => m.BusinessId == request.BusinessId && m.UserId == request.UserId, ct);
        if (already)
            return Result<MemberDto>.Fail("resource.conflict", "User is already a member of this business.");

        await _uow.Repository<UserBusiness>().AddAsync(new UserBusiness
        {
            UserId = request.UserId,
            BusinessId = request.BusinessId,
            RoleId = role.Id
        }, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<MemberDto>.Ok(new MemberDto(user.Id, user.FullName, user.Mobile, role.Code, role.Name));
    }
}

// ---- Remove member ----

public record RemoveMemberCommand(Guid BusinessId, Guid UserId) : IRequest<Result>;

public class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand, Result>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityQueries _identity;
    private readonly IUnitOfWork _uow;

    public RemoveMemberCommandHandler(ICurrentUser currentUser, IIdentityQueries identity, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _identity = identity;
        _uow = uow;
    }

    public async Task<Result> Handle(RemoveMemberCommand request, CancellationToken ct)
    {
        await AccessGuard.RequireCanManageMembersAsync(_currentUser, _identity, request.BusinessId, ct);

        var membership = await _uow.Repository<UserBusiness>().Query()
            .FirstOrDefaultAsync(m => m.BusinessId == request.BusinessId && m.UserId == request.UserId, ct);
        if (membership is null)
            throw new NotFoundException("Membership not found.");

        // Re-fetch tracked for delete (Query() is no-tracking).
        var tracked = await _uow.Repository<UserBusiness>().GetByIdAsync(membership.Id, ct);
        if (tracked is not null)
        {
            _uow.Repository<UserBusiness>().Remove(tracked);
            await _uow.SaveChangesAsync(ct);
        }
        return Result.Ok();
    }
}
