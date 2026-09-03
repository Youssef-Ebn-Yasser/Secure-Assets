using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MediaFile> Files { get; set; } = new List<MediaFile>();
}

public class MediaFile
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public User? Owner { get; set; }

    [Required]
    [MaxLength(512)]
    public string OriginalName { get; set; } = string.Empty;

    public MediaType MediaType { get; set; }

    public FileStatus Status { get; set; } = FileStatus.Pending;

    [MaxLength(1024)]
    public string StoragePath { get; set; } = string.Empty;

    [MaxLength(128)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public ChunkManifest? Manifest { get; set; }

    public ICollection<ProcessingJob> Jobs { get; set; } = new List<ProcessingJob>();
}

public class ChunkManifest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FileId { get; set; }

    [ForeignKey(nameof(FileId))]
    public MediaFile? File { get; set; }

    [Required]
    public string ManifestJson { get; set; } = "{}";

    public int ChunkCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProcessingJob
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FileId { get; set; }

    [ForeignKey(nameof(FileId))]
    public MediaFile? File { get; set; }

    public JobType JobType { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class AccessTokenLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FileId { get; set; }

    [Required]
    [MaxLength(256)]
    public string ChunkId { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public int UsedCount { get; set; }
}
