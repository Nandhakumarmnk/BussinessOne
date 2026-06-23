namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Object storage abstraction. Pilot uses local disk; production swaps in Cloud Storage
/// (see docs/01) with no change to callers. The DB stores only the returned object key.
/// </summary>
public interface IFileStorage
{
    Task<string> SaveAsync(
        Stream content, string fileName, string contentType, string? folder, CancellationToken ct = default);
}
