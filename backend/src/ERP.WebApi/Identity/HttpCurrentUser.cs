using ERP.Application.Common.Interfaces;

namespace ERP.WebApi.Identity;

/// <summary>
/// Request principal + per-business permission resolution. Permissions are resolved from the DB
/// (per user + active business), not packed into the JWT — see docs/10.
/// </summary>
public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IIdentityQueries _identity;

    public HttpCurrentUser(IHttpContextAccessor accessor, IIdentityQueries identity)
    {
        _accessor = accessor;
        _identity = identity;
    }

    private HttpContext? Ctx => _accessor.HttpContext;

    public Guid? UserId => ClaimReader.GetUserId(Ctx?.User);
    public Guid? TenantId => ClaimReader.GetTenantId(Ctx?.User);
    public bool IsSuperAdmin => Ctx?.User?.FindFirst("sa")?.Value == "true";
    public bool IsAuthenticated => Ctx?.User?.Identity?.IsAuthenticated == true;

    public Guid? BusinessId
    {
        get
        {
            var header = Ctx?.Request.Headers["X-Business-Id"].FirstOrDefault();
            return Guid.TryParse(header, out var id) ? id : null;
        }
    }

    public async Task<bool> IsMemberOfAsync(Guid businessId, CancellationToken ct = default)
        => UserId is { } userId && await _identity.IsMemberAsync(userId, businessId, ct);

    public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
    {
        if (BusinessId is not { } businessId) return Task.FromResult(IsSuperAdmin);
        return HasPermissionInBusinessAsync(permission, businessId, ct);
    }

    public async Task<bool> HasPermissionInBusinessAsync(string permission, Guid businessId, CancellationToken ct = default)
    {
        if (IsSuperAdmin) return true;
        if (UserId is not { } userId) return false;

        var permissions = await _identity.GetPermissionsAsync(userId, businessId, ct);
        return permissions.Contains(permission);
    }

    public async Task<bool> IsTenantOwnerAsync(CancellationToken ct = default)
    {
        if (IsSuperAdmin) return true;
        if (UserId is not { } userId || TenantId is not { } tenantId) return false;
        return await _identity.IsTenantOwnerAsync(userId, tenantId, ct);
    }
}
