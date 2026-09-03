namespace Shared.Messaging;

public record VideoUploadedMessage(
    Guid FileId,
    Guid JobId,
    string StoragePath,
    string BucketName,
    string OriginalName
);

public record ImageUploadedMessage(
    Guid FileId,
    Guid JobId,
    string StoragePath,
    string BucketName,
    string OriginalName
);

public record PdfUploadedMessage(
    Guid FileId,
    Guid JobId,
    string StoragePath,
    string BucketName,
    string OriginalName
);

public record JobCompletedMessage(
    Guid FileId,
    Guid JobId,
    bool Success,
    string? ErrorMessage,
    int ChunkCount,
    string ManifestJson
);

public static class QueueNames
{
    public const string VideoQueue = "vault.video.queue";
    public const string ImageQueue = "vault.image.queue";
    public const string PdfQueue = "vault.pdf.queue";
    public const string CompletedQueue = "vault.completed.queue";
}
