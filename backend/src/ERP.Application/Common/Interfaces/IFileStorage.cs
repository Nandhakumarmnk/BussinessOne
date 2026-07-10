namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Object storage abstraction. Pilot uses local disk; production swaps in Cloud Storage
/// (see docs/01) with no change to callers. The DB stores only the returned object key.
/// </summary>
public interface IFileStorage
{
    Task<string> SaveAsync(
        Stream content, string fileName, string contentType, string? folder, CancellationToken ct = default);

    /// <summary>
    /// Returns a URL the caller can use to fetch a stored object. For Cloud Storage this is a
    /// short-lived, signed HTTPS URL; for the local pilot it is a relative API path that the
    /// authenticated <c>files/content</c> endpoint streams from disk.
    /// </summary>
    Task<string> GetDownloadUrlAsync(string objectKey, CancellationToken ct = default);
}
