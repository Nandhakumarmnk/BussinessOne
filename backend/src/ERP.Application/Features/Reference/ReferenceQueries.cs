using ERP.Application.Common.Interfaces;
using ERP.Domain.Auditing;
using ERP.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Reference;

// Read-only reference data for building UI (pickers, role lists). Any authenticated user may read.

public record BusinessTypeDto(Guid Id, string Code, string Name);
public record RoleDto(Guid Id, string Code, string Name);
public record PermissionDto(string Code, string? Description);

public record GetBusinessTypesQuery : IRequest<IReadOnlyList<BusinessTypeDto>>;
public record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;
public record GetPermissionsQuery : IRequest<IReadOnlyList<PermissionDto>>;

public class GetBusinessTypesQueryHandler : IRequestHandler<GetBusinessTypesQuery, IReadOnlyList<BusinessTypeDto>>
{
    private readonly IRepository<BusinessType> _repo;
    public GetBusinessTypesQueryHandler(IRepository<BusinessType> repo) => _repo = repo;

    public async Task<IReadOnlyList<BusinessTypeDto>> Handle(GetBusinessTypesQuery request, CancellationToken ct)
        => await _repo.Query().Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new BusinessTypeDto(x.Id, x.Code, x.Name))
            .ToListAsync(ct);
}

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IRepository<Role> _repo;
    public GetRolesQueryHandler(IRepository<Role> repo) => _repo = repo;

    public async Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken ct)
        => await _repo.Query()
            .OrderBy(x => x.Name)
            .Select(x => new RoleDto(x.Id, x.Code, x.Name))
            .ToListAsync(ct);
}

public class GetPermissionsQueryHandler : IRequestHandler<GetPermissionsQuery, IReadOnlyList<PermissionDto>>
{
    private readonly IRepository<Permission> _repo;
    public GetPermissionsQueryHandler(IRepository<Permission> repo) => _repo = repo;

    public async Task<IReadOnlyList<PermissionDto>> Handle(GetPermissionsQuery request, CancellationToken ct)
        => await _repo.Query()
            .OrderBy(x => x.Code)
            .Select(x => new PermissionDto(x.Code, x.Description))
            .ToListAsync(ct);
}
