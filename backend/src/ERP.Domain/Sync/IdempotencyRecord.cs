namespace ERP.Domain.Sync;

/// <summary>
/// Records the result of a processed write keyed by its client-supplied Idempotency-Key, so a
/// retried (e.g. offline-replayed) request returns the original response instead of re-applying.
/// </summary>
public class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public string Key { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
