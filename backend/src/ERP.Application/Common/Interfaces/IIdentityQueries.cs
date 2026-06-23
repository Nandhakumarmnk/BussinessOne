using ERP.Application.Features.Auth.Common;
using ERP.Domain.Identity;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Read-side helpers for identity/auth/RBAC that keep the Application layer free of EF Core.
/// Implemented in Infrastructure against the shared scoped DbContext (so returned entities
/// are tracked and can be updated within the same unit of work).
/// </summary>
public interface IIdentityQueries
{
    Task<User?> FindByLoginAsync(string mobileOrEmail, CancellationToken ct = default);
    Task<User?> GetUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<MembershipDto>> GetMembershipsAsync(Guid userId, CancellationToken ct = default);

    Task<bool> IsMemberAsync(Guid userId, Guid businessId, CancellationToken ct = default);
    Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid userId, Guid businessId, CancellationToken ct = default);

    Task<bool> IsTenantOwnerAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Tracked fetch of a refresh token by its hash (for rotate/revoke).</summary>
    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Revokes all of a user's active refresh tokens (e.g. on password change).</summary>
    Task RevokeAllRefreshTokensAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Resolves the business's tenant (for tenant-scope authorization on route ops).</summary>
    Task<Guid?> GetBusinessTenantIdAsync(Guid businessId, CancellationToken ct = default);
}
