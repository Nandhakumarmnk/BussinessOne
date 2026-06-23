namespace ERP.Application.Common.Interfaces;

public record IdempotencyResult(int StatusCode, string ResponseBody);

/// <summary>Persists/looks up responses by (business, Idempotency-Key) for safe write retries.</summary>
public interface IIdempotencyStore
{
    Task<IdempotencyResult?> GetAsync(Guid businessId, string key, CancellationToken ct = default);
    Task SaveAsync(Guid businessId, string key, int statusCode, string responseBody, CancellationToken ct = default);
}
