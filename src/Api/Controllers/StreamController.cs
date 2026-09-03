using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Models;
using Shared.Security;
using Shared.Storage;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StreamController : ControllerBase
{
    private readonly VaultDbContext _db;
    private readonly IStorageService _storage;
    private readonly ITokenService _tokenService;
    private const string ProcessedBucket = "vault-processed";

    public StreamController(VaultDbContext db, IStorageService storage, ITokenService tokenService)
    {
        _db = db;
        _storage = storage;
        _tokenService = tokenService;
    }

    [HttpGet("{fileId:guid}/manifest.m3u8")]
    public async Task<IActionResult> GetDynamicManifest(Guid fileId, [FromQuery] string? token, [FromQuery] long? exp)
    {
        // Manifest can be requested with Bearer auth or a file-level view token
        var file = await _db.Files
            .Include(f => f.Manifest)
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file == null || file.Manifest == null || file.Status != FileStatus.Ready)
        {
            return NotFound(new { message = "Stream not ready or not found." });
        }

        var manifestData = JsonSerializer.Deserialize<VideoManifestData>(file.Manifest.ManifestJson);
        if (manifestData == null || manifestData.Segments == null)
        {
            return BadRequest(new { message = "Invalid manifest data." });
        }

        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:3");
        sb.AppendLine($"#EXT-X-TARGETDURATION:{manifestData.TargetDuration}");
        sb.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");

        // Key URI token
        if (!string.IsNullOrWhiteSpace(manifestData.KeyPath))
        {
            var keyToken = _tokenService.GenerateChunkToken(fileId, "key", TimeSpan.FromSeconds(60));
            sb.AppendLine($"#EXT-X-KEY:METHOD=AES-128,URI=\"/api/stream/{fileId}/key?token={keyToken}\"");
        }

        foreach (var seg in manifestData.Segments)
        {
            var segToken = _tokenService.GenerateChunkToken(fileId, seg.SegmentName, TimeSpan.FromSeconds(60));
            sb.AppendLine($"#EXTINF:{seg.Duration:F3},");
            sb.AppendLine($"/api/stream/{fileId}/segment/{seg.SegmentName}?token={segToken}");
        }

        sb.AppendLine("#EXT-X-ENDLIST");

        return Content(sb.ToString(), "application/vnd.apple.mpegurl", Encoding.UTF8);
    }

    [HttpGet("{fileId:guid}/key")]
    public async Task<IActionResult> GetKey(Guid fileId, [FromQuery] string token, [FromQuery] long exp = 0)
    {
        bool isValid = await _tokenService.ValidateChunkTokenAsync(fileId, "key", token, exp);
        if (!isValid)
        {
            return StatusCode(403, new { message = "Invalid or expired key token." });
        }

        var file = await _db.Files
            .Include(f => f.Manifest)
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file?.Manifest == null) return NotFound();

        var manifestData = JsonSerializer.Deserialize<VideoManifestData>(file.Manifest.ManifestJson);
        if (manifestData == null || string.IsNullOrWhiteSpace(manifestData.KeyPath))
        {
            return NotFound();
        }

        try
        {
            var keyStream = await _storage.GetObjectAsync(ProcessedBucket, manifestData.KeyPath);
            Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate");
            Response.Headers.Append("Pragma", "no-cache");
            return File(keyStream, "application/octet-stream");
        }
        catch
        {
            return NotFound();
        }
    }

    [HttpGet("{fileId:guid}/segment/{segmentName}")]
    public async Task<IActionResult> GetSegment(Guid fileId, string segmentName, [FromQuery] string token, [FromQuery] long exp = 0)
    {
        bool isValid = await _tokenService.ValidateChunkTokenAsync(fileId, segmentName, token, exp);
        if (!isValid)
        {
            return StatusCode(403, new { message = "Invalid or expired segment token." });
        }

        var storagePath = $"video/{fileId:N}/{segmentName}";
        try
        {
            var segmentStream = await _storage.GetObjectAsync(ProcessedBucket, storagePath);
            Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate");
            Response.Headers.Append("Pragma", "no-cache");
            return File(segmentStream, "video/MP2T");
        }
        catch
        {
            return NotFound(new { message = "Segment not found." });
        }
    }
}

public class VideoManifestData
{
    public int TargetDuration { get; set; } = 4;
    public string KeyPath { get; set; } = string.Empty;
    public List<VideoSegmentItem> Segments { get; set; } = new();
}

public class VideoSegmentItem
{
    public string SegmentName { get; set; } = string.Empty;
    public double Duration { get; set; } = 4.0;
}
