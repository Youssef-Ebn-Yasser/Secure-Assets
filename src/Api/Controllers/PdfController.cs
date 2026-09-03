using System.Text.Json;
using Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Models;
using Shared.Security;
using Shared.Storage;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfController : ControllerBase
{
    private readonly VaultDbContext _db;
    private readonly IStorageService _storage;
    private readonly ITokenService _tokenService;
    private const string ProcessedBucket = "vault-processed";

    public PdfController(VaultDbContext db, IStorageService storage, ITokenService tokenService)
    {
        _db = db;
        _storage = storage;
        _tokenService = tokenService;
    }

    [HttpGet("{fileId:guid}/manifest")]
    public async Task<IActionResult> GetPdfManifest(Guid fileId)
    {
        var file = await _db.Files
            .Include(f => f.Manifest)
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file == null || file.Manifest == null || file.Status != FileStatus.Ready)
        {
            return NotFound(new { message = "PDF manifest not ready or not found." });
        }

        var data = JsonSerializer.Deserialize<PdfManifestData>(file.Manifest.ManifestJson);
        if (data == null || data.Pages == null)
        {
            return BadRequest(new { message = "Invalid PDF manifest." });
        }

        var pageDtos = data.Pages.Select(p =>
        {
            var token = _tokenService.GenerateChunkToken(fileId, $"page-{p.PageNumber}", TimeSpan.FromSeconds(60));
            return new PdfPageDto(
                p.PageNumber,
                $"/api/pdf/{fileId}/page/{p.PageNumber}?token={token}"
            );
        }).ToList();

        var manifestDto = new PdfManifestDto(
            fileId,
            data.TotalPages,
            pageDtos
        );

        return Ok(manifestDto);
    }

    [HttpGet("{fileId:guid}/page/{pageNum:int}")]
    public async Task<IActionResult> GetPage(Guid fileId, int pageNum, [FromQuery] string token, [FromQuery] long exp = 0)
    {
        bool isValid = await _tokenService.ValidateChunkTokenAsync(fileId, $"page-{pageNum}", token, exp);
        if (!isValid)
        {
            return StatusCode(403, new { message = "Invalid or expired page token." });
        }

        var file = await _db.Files
            .Include(f => f.Manifest)
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file?.Manifest == null) return NotFound();

        var data = JsonSerializer.Deserialize<PdfManifestData>(file.Manifest.ManifestJson);
        var pageItem = data?.Pages.FirstOrDefault(p => p.PageNumber == pageNum);
        if (pageItem == null) return NotFound(new { message = "Page not found." });

        var storagePath = $"pdf/{fileId:N}/{data!.FolderGuid}/page-{pageNum:D4}.webp";
        try
        {
            var pageStream = await _storage.GetObjectAsync(ProcessedBucket, storagePath);
            Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate");
            Response.Headers.Append("Pragma", "no-cache");
            Response.Headers.Append("Content-Disposition", "inline");
            return File(pageStream, "image/webp");
        }
        catch
        {
            return NotFound(new { message = "Page content not found." });
        }
    }
}

public class PdfManifestData
{
    public string FolderGuid { get; set; } = string.Empty;
    public int TotalPages { get; set; }
    public List<PdfPageItem> Pages { get; set; } = new();
}

public class PdfPageItem
{
    public int PageNumber { get; set; }
    public string RelativePath { get; set; } = string.Empty;
}
