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
}
