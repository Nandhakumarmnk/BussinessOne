using ERP.Application.Common.Interfaces;
using ERP.Domain.Sync;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public class EfIdempotencyStore : IIdempotencyStore
{
    private readonly AppDbContext _db;
    public EfIdempotencyStore(AppDbContext db) => _db = db;

    public async Task<IdempotencyResult?> GetAsync(Guid businessId, string key, CancellationToken ct = default)
    {
        var record = await _db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Key == key, ct);
        return record is null ? null : new IdempotencyResult(record.StatusCode, record.ResponseBody);
    }

    public async Task SaveAsync(Guid businessId, string key, int statusCode, string responseBody, CancellationToken ct = default)
    {
        // Ignore races: if another request stored the same key first, keep the original.
        if (await _db.IdempotencyRecords.AnyAsync(r => r.BusinessId == businessId && r.Key == key, ct))
            return;

        _db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            BusinessId = businessId,
            Key = key,
            StatusCode = statusCode,
            ResponseBody = responseBody,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
