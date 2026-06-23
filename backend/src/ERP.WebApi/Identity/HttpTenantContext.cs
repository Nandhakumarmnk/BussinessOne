using System.Security.Claims;
using ERP.Application.Common.Interfaces;

namespace ERP.WebApi.Identity;

/// <summary>
/// Dependency-free <see cref="ITenantContext"/> built from JWT claims + the X-Business-Id header.
/// Used by the DbContext query filter and the audit interceptor (must not touch the DB).
/// </summary>
public class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Ctx => _accessor.HttpContext;

    public Guid? UserId => ClaimReader.GetUserId(Ctx?.User);

    public bool IsSuperAdmin => Ctx?.User?.FindFirst("sa")?.Value == "true";

    public Guid? BusinessId
    {
        get
        {
            var header = Ctx?.Request.Headers["X-Business-Id"].FirstOrDefault();
            return Guid.TryParse(header, out var id) ? id : null;
        }
    }
}

/// <summary>Shared claim-parsing helpers (sub may or may not be mapped to NameIdentifier).</summary>
internal static class ClaimReader
{
    public static Guid? GetUserId(ClaimsPrincipal? user)
    {
        var value = user?.FindFirst("sub")?.Value
                    ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static Guid? GetTenantId(ClaimsPrincipal? user)
        => Guid.TryParse(user?.FindFirst("tenant")?.Value, out var id) ? id : null;
}
