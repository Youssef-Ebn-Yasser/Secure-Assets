using Shared.Models;

namespace Api.DTOs;

public record MediaFileDto(
    Guid Id,
    string OriginalName,
    MediaType MediaType,
    FileStatus Status,
    long FileSizeBytes,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    int? ChunkCount
);

public record UploadResponseDto(
    Guid FileId,
    string OriginalName,
    MediaType MediaType,
    FileStatus Status,
    string Message
);

public record ImageTileManifestDto(
    Guid FileId,
    int GridRows,
    int GridCols,
    int OriginalWidth,
    int OriginalHeight,
    int TileWidth,
    int TileHeight,
    List<TileItemDto> Tiles
);

public record TileItemDto(
    int Row,
    int Col,
    string TileId,
    string Url
);

public record PdfManifestDto(
    Guid FileId,
    int TotalPages,
    List<PdfPageDto> Pages
);

public record PdfPageDto(
    int PageNumber,
    string Url
);
