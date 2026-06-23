using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Businesses;

public record GetBusinessesQuery : IRequest<IReadOnlyList<BusinessDto>>;
public record GetBusinessQuery(Guid Id) : IRequest<BusinessDto>;

public class GetBusinessesQueryHandler : IRequestHandler<GetBusinessesQuery, IReadOnlyList<BusinessDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IRepository<Business> _businesses;
    private readonly IRepository<BusinessType> _types;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<UserBusiness> _memberships;

    public GetBusinessesQueryHandler(
        ICurrentUser currentUser, IRepository<Business> businesses, IRepository<BusinessType> types,
        IRepository<Role> roles, IRepository<UserBusiness> memberships)
    {
        _currentUser = currentUser;
        _businesses = businesses;
        _types = types;
        _roles = roles;
        _memberships = memberships;
    }

    public async Task<IReadOnlyList<BusinessDto>> Handle(GetBusinessesQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? Guid.Empty;
        var query = _businesses.Query();

        if (_currentUser.IsSuperAdmin)
        {
            // all businesses
        }
        else if (await _currentUser.IsTenantOwnerAsync(ct))
        {
            query = query.Where(b => b.TenantId == _currentUser.TenantId);
        }
        else
        {
            var memberBusinessIds = _memberships.Query().Where(m => m.UserId == userId).Select(m => m.BusinessId);
            query = query.Where(b => memberBusinessIds.Contains(b.Id));
        }

        return await Project(query, _types, _roles, _memberships, userId).ToListAsync(ct);
    }

    internal static IQueryable<BusinessDto> Project(
        IQueryable<Business> query, IRepository<BusinessType> types, IRepository<Role> roles,
        IRepository<UserBusiness> memberships, Guid userId)
        => query
            .OrderBy(b => b.Name)
            .Select(b => new BusinessDto(
                b.Id,
                b.Name,
                types.Query().Where(t => t.Id == b.BusinessTypeId).Select(t => t.Code).First(),
                types.Query().Where(t => t.Id == b.BusinessTypeId).Select(t => t.Name).First(),
                b.GstNumber,
                b.Address,
                b.IsActive,
                memberships.Query()
                    .Where(m => m.BusinessId == b.Id && m.UserId == userId)
                    .Join(roles.Query(), m => m.RoleId, r => r.Id, (m, r) => r.Code)
                    .FirstOrDefault()));
}

public class GetBusinessQueryHandler : IRequestHandler<GetBusinessQuery, BusinessDto>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityQueries _identity;
    private readonly IRepository<Business> _businesses;
    private readonly IRepository<BusinessType> _types;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<UserBusiness> _memberships;

    public GetBusinessQueryHandler(
        ICurrentUser currentUser, IIdentityQueries identity, IRepository<Business> businesses,
        IRepository<BusinessType> types, IRepository<Role> roles, IRepository<UserBusiness> memberships)
    {
        _currentUser = currentUser;
        _identity = identity;
        _businesses = businesses;
        _types = types;
        _roles = roles;
        _memberships = memberships;
    }

    public async Task<BusinessDto> Handle(GetBusinessQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? Guid.Empty;

        if (!_currentUser.IsSuperAdmin)
        {
            var tenant = await _identity.GetBusinessTenantIdAsync(request.Id, ct);
            if (tenant is null || tenant != _currentUser.TenantId)
                throw new NotFoundException("Business not found.");

            var isOwner = await _currentUser.IsTenantOwnerAsync(ct);
            var isMember = await _currentUser.IsMemberOfAsync(request.Id, ct);
            if (!isOwner && !isMember)
                throw new ForbiddenException("auth.forbidden", "You cannot access this business.");
        }

        var dto = await GetBusinessesQueryHandler
            .Project(_businesses.Query().Where(b => b.Id == request.Id), _types, _roles, _memberships, userId)
            .FirstOrDefaultAsync(ct);

        return dto ?? throw new NotFoundException("Business not found.");
    }
}
