using ERP.Application.Common.Interfaces;
using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps audit columns on save and converts hard deletes of <see cref="BaseEntity"/> into
/// soft deletes. Reads the acting user from <see cref="ITenantContext"/> (no DB access).
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenant;
    private readonly IDateTime _clock;

    public AuditableEntityInterceptor(ITenantContext tenant, IDateTime clock)
    {
        _tenant = tenant;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = _clock.UtcNow;
        var userId = _tenant.UserId;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy ??= userId;
                    StampBusinessId(entry);
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                case EntityState.Deleted:
                    // Never hard-delete business data.
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }

    // Safety net: stamp the tenant discriminator on insert so a handler can never forget it.
    private void StampBusinessId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.Entity is not IBusinessScoped) return;
        if (_tenant.BusinessId is not { } businessId) return;

        var property = entry.Property(nameof(IBusinessScoped.BusinessId));
        if (property.CurrentValue is Guid current && current == Guid.Empty)
            property.CurrentValue = businessId;
    }
}
