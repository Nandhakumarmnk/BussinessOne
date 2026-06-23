using System.Text.Json;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auditing;
using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Writes an append-only <see cref="AuditLog"/> row for every create/update/delete of a
/// <see cref="BaseEntity"/>. Runs after the audit-stamp interceptor, so soft-deletes (which the
/// stamp interceptor rewrites to Modified) are detected and recorded as "delete".
/// </summary>
public class AuditTrailInterceptor : SaveChangesInterceptor
{
    private static readonly string[] SensitiveProperties = { "PasswordHash", "TokenHash" };

    private readonly ITenantContext _tenant;
    private readonly IDateTime _clock;

    public AuditTrailInterceptor(ITenantContext tenant, IDateTime clock)
    {
        _tenant = tenant;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        var now = _clock.UtcNow;
        var userId = _tenant.UserId;
        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var action = entry.State == EntityState.Added ? "create"
                       : IsSoftDelete(entry) || entry.State == EntityState.Deleted ? "delete"
                       : "update";

            logs.Add(new AuditLog
            {
                BusinessId = entry.Entity is IBusinessScoped scoped ? scoped.BusinessId : null,
                UserId = userId,
                Entity = entry.Metadata.ClrType.Name,
                EntityId = entry.Entity.Id,
                Action = action,
                OldValues = action == "create" ? null : Serialize(entry, original: true),
                NewValues = action == "delete" ? null : Serialize(entry, original: false),
                CreatedAt = now
            });
        }

        if (logs.Count > 0)
            context.Set<AuditLog>().AddRange(logs);
    }

    private static bool IsSoftDelete(EntityEntry entry)
    {
        if (entry.State != EntityState.Modified) return false;
        var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == nameof(BaseEntity.IsDeleted));
        return prop is { IsModified: true } && prop.CurrentValue is true && prop.OriginalValue is false;
    }

    private static string Serialize(EntityEntry entry, bool original)
    {
        var data = new Dictionary<string, object?>();
        foreach (var p in entry.Properties)
        {
            var name = p.Metadata.Name;
            if (name is nameof(BaseEntity.CreatedAt) or nameof(BaseEntity.UpdatedAt)) continue;
            if (SensitiveProperties.Contains(name)) continue;
            data[name] = original ? p.OriginalValue : p.CurrentValue;
        }
        return JsonSerializer.Serialize(data);
    }
}
