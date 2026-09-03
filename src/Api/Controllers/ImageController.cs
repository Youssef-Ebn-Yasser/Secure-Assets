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
public class ImageController : ControllerBase
{
    private readonly VaultDbContext _db;
    private readonly IStorageService _storage;
    private readonly ITokenService _tokenService;
    private const string ProcessedBucket = "vault-processed";

    public ImageController(VaultDbContext db, IStorageService storage, ITokenService tokenService)
    {
        _db = db;
        _storage = storage;
        _tokenService = tokenService;
    }

    [HttpGet("{fileId:guid}/manifest")]
    public async Task<IActionResult> GetImageManifest(Guid fileId)
    {
        var file = await _db.Files
            .Include(f => f.Manifest)
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file == null || file.Manifest == null || file.Status != FileStatus.Ready)
        {
            return NotFound(new { message = "Image manifest not ready or not found." });
        }

        var data = JsonSerializer.Deserialize<ImageManifestData>(file.Manifest.ManifestJson);
        if (data == null || data.Tiles == null)
        {
            return BadRequest(new { message = "Invalid tile manifest." });
        }

        var tileDtos = data.Tiles.Select(t =>
        {
            var token = _tokenService.GenerateChunkToken(fileId, t.TileId, TimeSpan.FromSeconds(60));
            return new TileItemDto(
                t.Row,
                t.Col,
                t.TileId,
                $"/api/image/{fileId}/tile/{t.TileId}?token={token}"
            );
        }).ToList();

        var manifestDto = new ImageTileManifestDto(
            fileId,
            data.GridRows,
            data.GridCols,
            data.OriginalWidth,
            data.OriginalHeight,
            data.TileWidth,
            data.TileHeight,
            tileDtos
        );

        return Ok(manifestDto);
    }

    [HttpGet("{fileId:guid}/tile/{tileId}")]
    public async Task<IActionResult> GetTile(Guid fileId, string tileId, [FromQuery] string token, [FromQuery] long exp = 0)
    {
        bool isValid = await _tokenService.ValidateChunkTokenAsync(fileId, tileId, token, exp);
        if (!isValid)
        {
            return StatusCode(403, new { message = "Invalid or expired tile token." });
        }

        var storagePath = $"image/{fileId:N}/{tileId}.webp";
        try
        {
            var tileStream = await _storage.GetObjectAsync(ProcessedBucket, storagePath);
            Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate");
            Response.Headers.Append("Pragma", "no-cache");
            return File(tileStream, "image/webp");
        }
        catch
        {
            return NotFound(new { message = "Tile chunk not found." });
        }
    }
}

public class ImageManifestData
{
    public int GridRows { get; set; } = 4;
    public int GridCols { get; set; } = 4;
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int TileWidth { get; set; }
    public int TileHeight { get; set; }
    public List<ImageTileItem> Tiles { get; set; } = new();
}

public class ImageTileItem
{
    public int Row { get; set; }
    public int Col { get; set; }
    public string TileId { get; set; } = string.Empty;
}
