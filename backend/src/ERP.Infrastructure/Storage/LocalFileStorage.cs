using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Storage;

/// <summary>
/// Development/pilot file storage that writes under a local root folder ("Files:Root").
/// Production replaces this with a Cloud Storage implementation of <see cref="IFileStorage"/>.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration config)
        => _root = config["Files:Root"] ?? Path.Combine(AppContext.BaseDirectory, "_files");

    public async Task<string> SaveAsync(
        Stream content, string fileName, string contentType, string? folder, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);
        var key = $"{folder ?? "misc"}/{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);

        return key;
    }

    /// <summary>
    /// Local files are not publicly reachable, so we return a relative API path that the
    /// authenticated <c>GET /api/v1/files/content</c> endpoint streams from disk. (We deliberately
    /// do not enable static-file middleware, which would expose the whole root unauthenticated.)
    /// </summary>
    public Task<string> GetDownloadUrlAsync(string objectKey, CancellationToken ct = default)
        => Task.FromResult($"/api/v1/files/content?key={Uri.EscapeDataString(objectKey)}");
}
