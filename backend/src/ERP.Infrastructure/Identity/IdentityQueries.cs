using ERP.Application.Common.Interfaces;
using ERP.Application.Features.Auth.Common;
using ERP.Domain.Identity;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Identity;

public class IdentityQueries : IIdentityQueries
{
    private readonly AppDbContext _db;

    public IdentityQueries(AppDbContext db) => _db = db;

    public Task<User?> FindByLoginAsync(string mobileOrEmail, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Mobile == mobileOrEmail || u.Email == mobileOrEmail, ct);

    public Task<User?> GetUserAsync(Guid userId, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

    public async Task<IReadOnlyList<MembershipDto>> GetMembershipsAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await (
            from ub in _db.UserBusinesses.AsNoTracking()
            join b in _db.Businesses on ub.BusinessId equals b.Id
            join bt in _db.BusinessTypes on b.BusinessTypeId equals bt.Id
            join r in _db.Roles on ub.RoleId equals r.Id
            where ub.UserId == userId
            select new { ub.BusinessId, BusinessName = b.Name, TypeCode = bt.Code, RoleCode = r.Code, r.Id }
        ).ToListAsync(ct);

        var result = new List<MembershipDto>(rows.Count);
        foreach (var row in rows)
        {
            var permissions = await _db.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == row.Id)
                .Join(_db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
                .ToListAsync(ct);

            result.Add(new MembershipDto(row.BusinessId, row.BusinessName, row.TypeCode, row.RoleCode, permissions));
        }
        return result;
    }

    public Task<bool> IsMemberAsync(Guid userId, Guid businessId, CancellationToken ct = default)
        => _db.UserBusinesses.AnyAsync(ub => ub.UserId == userId && ub.BusinessId == businessId, ct);

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        Guid userId, Guid businessId, CancellationToken ct = default)
    {
        var roleId = await _db.UserBusinesses
            .Where(ub => ub.UserId == userId && ub.BusinessId == businessId)
            .Select(ub => (Guid?)ub.RoleId)
            .FirstOrDefaultAsync(ct);

        if (roleId is null) return Array.Empty<string>();

        return await _db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Join(_db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
            .ToListAsync(ct);
    }

    public Task<bool> IsTenantOwnerAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        => _db.Tenants.AnyAsync(t => t.Id == tenantId && t.OwnerUserId == userId, ct);

    public async Task<Guid?> GetBusinessTenantIdAsync(Guid businessId, CancellationToken ct = default)
    {
        var tenantId = await _db.Businesses
            .Where(b => b.Id == businessId)
            .Select(b => (Guid?)b.TenantId)
            .FirstOrDefaultAsync(ct);
        return tenantId;
    }

    // Tracked (no AsNoTracking) so the handler can revoke/rotate within the unit of work.
    public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken ct = default)
        => _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

    public async Task RevokeAllRefreshTokensAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, nowUtc), ct);
    }
}
