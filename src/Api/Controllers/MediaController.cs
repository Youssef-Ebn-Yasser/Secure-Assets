using System.Security.Claims;
using Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Messaging;
using Shared.Models;
using Shared.Storage;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly VaultDbContext _db;
    private readonly IStorageService _storage;
    private readonly IRabbitMqPublisher _publisher;
    private const string RawBucket = "vault-raw";

    public MediaController(VaultDbContext db, IStorageService storage, IRabbitMqPublisher publisher)
    {
        _db = db;
        _storage = storage;
        _publisher = publisher;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserFiles()
    {
        var userId = GetCurrentUserId();
        var files = await _db.Files
            .Include(f => f.Manifest)
            .Where(f => f.OwnerId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new MediaFileDto(
                f.Id,
                f.OriginalName,
                f.MediaType,
                f.Status,
                f.FileSizeBytes,
                f.CreatedAt,
                f.ProcessedAt,
                f.Manifest != null ? f.Manifest.ChunkCount : 0
            ))
            .ToListAsync();

        return Ok(files);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetFileById(Guid id)
    {
        var userId = GetCurrentUserId();
        var file = await _db.Files
            .Include(f => f.Manifest)
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId);

        if (file == null) return NotFound(new { message = "File not found." });

        return Ok(new MediaFileDto(
            file.Id,
            file.OriginalName,
            file.MediaType,
            file.Status,
            file.FileSizeBytes,
            file.CreatedAt,
            file.ProcessedAt,
            file.Manifest != null ? file.Manifest.ChunkCount : 0
        ));
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetFileStatus(Guid id)
    {
        var userId = GetCurrentUserId();
        var file = await _db.Files
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId);

        if (file == null) return NotFound();

        var job = await _db.ProcessingJobs
            .Where(j => j.FileId == id)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            fileId = file.Id,
            status = file.Status.ToString(),
            processedAt = file.ProcessedAt,
            jobStatus = job?.Status.ToString(),
            lastError = job?.LastError
        });
    }

    [HttpPost("upload")]
    [RequestSizeLimit(500_000_000)] // 500MB
    public async Task<IActionResult> Upload([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        var userId = GetCurrentUserId();
        var originalName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalName).ToLowerInvariant();

        MediaType mediaType;
        JobType jobType;
        string queueName;

        switch (extension)
        {
            case ".mp4":
            case ".mov":
            case ".mkv":
            case ".avi":
            case ".webm":
                mediaType = MediaType.Video;
                jobType = JobType.TranscodeVideo;
                queueName = QueueNames.VideoQueue;
                break;

            case ".jpg":
            case ".jpeg":
            case ".png":
            case ".webp":
            case ".bmp":
            case ".tiff":
                mediaType = MediaType.Image;
                jobType = JobType.TileImage;
                queueName = QueueNames.ImageQueue;
                break;

            case ".pdf":
                mediaType = MediaType.Pdf;
                jobType = JobType.ExtractPdfPages;
                queueName = QueueNames.PdfQueue;
                break;

            default:
                return BadRequest(new { message = $"Unsupported file extension '{extension}'." });
        }

        var fileId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var storagePath = $"raw/{fileId:N}/{Guid.NewGuid():N}{extension}";

        // Upload original raw file to private MinIO storage
        using (var stream = file.OpenReadStream())
        {
            await _storage.PutObjectAsync(RawBucket, storagePath, stream, file.Length, file.ContentType);
        }

        var mediaFile = new MediaFile
        {
            Id = fileId,
            OwnerId = userId,
            OriginalName = originalName,
            MediaType = mediaType,
            Status = FileStatus.Processing,
            StoragePath = storagePath,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            CreatedAt = DateTime.UtcNow
        };

        var job = new ProcessingJob
        {
            Id = jobId,
            FileId = fileId,
            JobType = jobType,
            Status = JobStatus.Processing,
            Attempts = 1,
            CreatedAt = DateTime.UtcNow
        };

        _db.Files.Add(mediaFile);
        _db.ProcessingJobs.Add(job);
        await _db.SaveChangesAsync();

        // Dispatch job to RabbitMQ queue
        switch (mediaType)
        {
            case MediaType.Video:
                _publisher.Publish(queueName, new VideoUploadedMessage(fileId, jobId, storagePath, RawBucket, originalName));
                break;
            case MediaType.Image:
                _publisher.Publish(queueName, new ImageUploadedMessage(fileId, jobId, storagePath, RawBucket, originalName));
                break;
            case MediaType.Pdf:
                _publisher.Publish(queueName, new PdfUploadedMessage(fileId, jobId, storagePath, RawBucket, originalName));
                break;
        }

        return Accepted(new UploadResponseDto(
            fileId,
            originalName,
            mediaType,
            FileStatus.Processing,
            "File uploaded successfully and processing started."
        ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteFile(Guid id)
    {
        var userId = GetCurrentUserId();
        var file = await _db.Files.FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == userId);
        if (file == null) return NotFound();

        _db.Files.Remove(file);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var idVal = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idVal, out var guid) ? guid : Guid.Empty;
    }
}
