namespace ERP.Application.Common.Interfaces;

/// <summary>
/// One transaction boundary per request. Repositories share the same EF context so a single
/// <see cref="SaveChangesAsync"/> commits the whole use case atomically.
/// </summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : class;

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
