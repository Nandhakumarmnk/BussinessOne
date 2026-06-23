namespace ERP.Application.Common.Interfaces;

/// <summary>
/// The principal + tenant context for the current request, resolved from the JWT and the
/// <c>X-Business-Id</c> header. Permissions are resolved per-business (not packed in the JWT).
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    bool IsSuperAdmin { get; }
    bool IsAuthenticated { get; }

    /// <summary>Active business from the X-Business-Id header (null if not supplied).</summary>
    Guid? BusinessId { get; }

    Task<bool> IsMemberOfAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>Permission check against the active business (X-Business-Id header).</summary>
    Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default);

    /// <summary>Permission check against a specific business (route-driven, header-independent).</summary>
    Task<bool> HasPermissionInBusinessAsync(string permission, Guid businessId, CancellationToken ct = default);

    /// <summary>True if the caller owns their tenant (tenant-level management gate).</summary>
    Task<bool> IsTenantOwnerAsync(CancellationToken ct = default);
}
