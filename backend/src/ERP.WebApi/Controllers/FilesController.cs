using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ERP.WebApi.Controllers;

/// <summary>Attachment upload. Returns the object key to store on the owning record (e.g. an expense).</summary>
[Authorize]
public class FilesController : ApiControllerBase
{
    private readonly IFileStorage _storage;
    public FilesController(IFileStorage storage) => _storage = storage;

    [HttpPost("~/api/v1/files")]
    [RequestSizeLimit(10_000_000)]   // 10 MB
    public async Task<IActionResult> Upload(IFormFile? file, [FromQuery] string? folder, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = new { code = "file.empty", message = "No file uploaded." } });

        await using var stream = file.OpenReadStream();
        var objectKey = await _storage.SaveAsync(stream, file.FileName, file.ContentType, folder ?? "expenses", ct);
        return Ok(new { data = new { objectKey } });
    }

    /// <summary>
    /// Streams a locally-stored file. Only used by the local (pilot) storage provider — Cloud Storage
    /// serves objects directly via signed URLs. Access is gated by <c>[Authorize]</c> plus a
    /// path-traversal guard; owning records still resolve the key through their own RBAC-scoped
    /// endpoint (e.g. <c>GET /api/v1/expenses/{id}/attachment</c>) before this URL is handed out.
    /// </summary>
    [HttpGet("~/api/v1/files/content")]
    public IActionResult Content([FromQuery] string? key, [FromServices] IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = new { code = "file.key_required", message = "key is required." } });

        var root = Path.GetFullPath(
            config["Files:Root"] ?? Path.Combine(AppContext.BaseDirectory, "_files"));
        var full = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));

        // Reject anything that escapes the storage root (e.g. key="../../secret").
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !System.IO.File.Exists(full))
            return NotFound();

        return PhysicalFile(full, "application/octet-stream");
    }
}
