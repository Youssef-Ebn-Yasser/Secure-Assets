namespace Shared.Models;

public enum UserRole
{
    User = 0,
    Admin = 1
}

public enum MediaType
{
    Video = 1,
    Image = 2,
    Pdf = 3
}

public enum FileStatus
{
    Pending = 0,
    Processing = 1,
    Ready = 2,
    Failed = 3
}

public enum JobType
{
    TranscodeVideo = 1,
    TileImage = 2,
    ExtractPdfPages = 3
}

public enum JobStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
