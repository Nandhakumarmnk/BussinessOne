using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Common.Security;

/// <summary>
/// Reusable authorization checks for tenant- and business-scoped operations. Throws
/// <see cref="ForbiddenException"/> / <see cref="NotFoundException"/> so the API maps them
/// to 403 / 404 automatically.
/// </summary>
internal static class AccessGuard
{
    /// <summary>An active business (X-Business-Id) is required. Returns its id.</summary>
    public static Guid RequireBusiness(ICurrentUser user)
    {
        if (user.BusinessId is not { } businessId)
            throw new ForbiddenException("tenant.business_required", "An active business (X-Business-Id header) is required.");
        return businessId;
    }

    /// <summary>Caller must own their tenant. Returns the tenant id.</summary>
    public static async Task<Guid> RequireTenantOwnerAsync(ICurrentUser user, CancellationToken ct)
    {
        if (!await user.IsTenantOwnerAsync(ct))
            throw new ForbiddenException("auth.forbidden", "Tenant owner access required.");
        if (user.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant.required", "A tenant context is required.");
        return tenantId;
    }

    /// <summary>
    /// Caller may manage members of <paramref name="businessId"/>: super admin, the tenant owner,
    /// or a member holding <c>business.members.manage</c> in that business.
    /// </summary>
    public static async Task RequireCanManageMembersAsync(
        ICurrentUser user, IIdentityQueries identity, Guid businessId, CancellationToken ct)
    {
        if (user.IsSuperAdmin) return;

        var businessTenant = await identity.GetBusinessTenantIdAsync(businessId, ct);
        if (businessTenant is null || businessTenant != user.TenantId)
            throw new NotFoundException("Business not found.");

        if (await user.IsTenantOwnerAsync(ct)) return;
        if (await user.HasPermissionInBusinessAsync(Permissions.Business.MembersManage, businessId, ct)) return;

        throw new ForbiddenException("auth.forbidden", "You cannot manage members of this business.");
    }
}
