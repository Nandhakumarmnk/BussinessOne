namespace ERP.Domain.Auditing;

/// <summary>
/// Append-only record of a create/update/delete (or login) on a business entity.
/// Written automatically by the audit-trail interceptor. Never updated or deleted.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? BusinessId { get; set; }
    public Guid? UserId { get; set; }
    public string Entity { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string Action { get; set; } = string.Empty;   // create | update | delete | login
    public string? OldValues { get; set; }                // JSON
    public string? NewValues { get; set; }                // JSON
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
