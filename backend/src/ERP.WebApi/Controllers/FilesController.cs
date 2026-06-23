using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
