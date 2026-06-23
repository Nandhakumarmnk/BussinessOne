namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Lightweight, dependency-free request context (reads JWT claims + X-Business-Id only).
/// Consumed by the DbContext global query filter and the audit interceptor, so it must NOT
/// touch the database (that would create a dependency cycle with the context).
/// </summary>
public interface ITenantContext
{
    Guid? UserId { get; }
    Guid? BusinessId { get; }
    bool IsSuperAdmin { get; }
}
