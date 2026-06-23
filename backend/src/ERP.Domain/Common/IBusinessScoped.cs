namespace ERP.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a single business (tenant discriminator).
/// The EF Core global query filter and the tenant-stamping interceptor key off this.
/// </summary>
public interface IBusinessScoped
{
    Guid BusinessId { get; }
}
