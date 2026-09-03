using Minio;
using Minio.DataModel.Args;

namespace Shared.Storage;

public interface IStorageService
{
    Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default);
    Task PutObjectAsync(string bucketName, string objectName, Stream dataStream, long size, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default);
    Task CopyObjectAsync(string fromBucket, string fromObject, string toBucket, string toObject, CancellationToken cancellationToken = default);
    Task DeleteObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default);
    Task<bool> ObjectExistsAsync(string bucketName, string objectName, CancellationToken cancellationToken = default);
}

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _client;

    public MinioStorageService(IMinioClient client)
    {
        _client = client;
    }

    public async Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        bool exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), cancellationToken);
        if (!exists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), cancellationToken);
        }
    }

    public async Task PutObjectAsync(string bucketName, string objectName, Stream dataStream, long size, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(bucketName, cancellationToken);
        
        var args = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(dataStream)
            .WithObjectSize(size)
            .WithContentType(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        await _client.PutObjectAsync(args, cancellationToken);
    }

    public async Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();
        var args = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(memoryStream);
            });

        await _client.GetObjectAsync(args, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task CopyObjectAsync(string fromBucket, string fromObject, string toBucket, string toObject, CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(toBucket, cancellationToken);
        var cpSrcArgs = new CopySourceObjectArgs()
            .WithBucket(fromBucket)
            .WithObject(fromObject);

        var args = new CopyObjectArgs()
            .WithBucket(toBucket)
            .WithObject(toObject)
            .WithCopyObjectSource(cpSrcArgs);

        await _client.CopyObjectAsync(args, cancellationToken);
    }

    public async Task DeleteObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);

        await _client.RemoveObjectAsync(args, cancellationToken);
    }

    public async Task<bool> ObjectExistsAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            var stat = await _client.StatObjectAsync(args, cancellationToken);
            return stat != null;
        }
        catch
        {
            return false;
        }
    }
}
