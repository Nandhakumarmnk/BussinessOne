namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Generic persistence abstraction. Reads use <see cref="Query"/> (no-tracking projections);
/// writes load aggregates and persist via the <see cref="IUnitOfWork"/>.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    IQueryable<T> Query();
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
