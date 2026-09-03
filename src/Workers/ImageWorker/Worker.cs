using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Messaging;
using Shared.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ImageWorker;

public class ImageWorkerService : BackgroundService
{
    private readonly ILogger<ImageWorkerService> _logger;
    private readonly IConfiguration _config;
    private readonly IStorageService _storage;
    private readonly IRabbitMqPublisher _publisher;
    private IConnection? _connection;
    private IModel? _channel;
    private const string ProcessedBucket = "vault-processed";

    public ImageWorkerService(
        ILogger<ImageWorkerService> logger,
        IConfiguration config,
        IStorageService storage,
        IRabbitMqPublisher publisher)
    {
        _logger = logger;
        _config = config;
        _storage = storage;
        _publisher = publisher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        string host = _config["Rabbit:Host"] ?? "localhost";
        string user = _config["Rabbit:User"] ?? "guest";
        string pass = _config["Rabbit:Pass"] ?? "guest";
        int port = int.TryParse(_config["Rabbit:Port"], out int p) ? p : 5672;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = host,
                    UserName = user,
                    Password = pass,
                    Port = port,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.QueueDeclare(
                    queue: QueueNames.ImageQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    try
                    {
                        var msg = JsonSerializer.Deserialize<ImageUploadedMessage>(json);
                        if (msg != null)
                        {
                            await ProcessImageAsync(msg, stoppingToken);
                        }
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process image tiling job.");
                        _channel.BasicNack(ea.DeliveryTag, false, false);
                    }
                };

                _channel.BasicConsume(
                    queue: QueueNames.ImageQueue,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("ImageWorker listening on {Queue}", QueueNames.ImageQueue);

                while (!stoppingToken.IsCancellationRequested && _channel.IsOpen)
                {
                    await Task.Delay(2000, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("RabbitMQ connection error in ImageWorker: {Message}. Retrying...", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessImageAsync(ImageUploadedMessage msg, CancellationToken ct)
    {
        _logger.LogInformation("Starting image tiling for FileId: {FileId}", msg.FileId);

        try
        {
            using var rawStream = await _storage.GetObjectAsync(msg.BucketName, msg.StoragePath, ct);
            using var image = await Image.LoadAsync(rawStream, ct);

            // Strip metadata (EXIF/ICC)
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            int origWidth = image.Width;
            int origHeight = image.Height;

            // Determine grid: 4x4 default
            int gridRows = 4;
            int gridCols = 4;

            int tileWidth = (int)Math.Ceiling((double)origWidth / gridCols);
            int tileHeight = (int)Math.Ceiling((double)origHeight / gridRows);

            var tiles = new List<object>();

            var webpEncoder = new WebpEncoder
            {
                Quality = 85,
                FileFormat = WebpFileFormatType.Lossy
            };

            for (int r = 0; r < gridRows; r++)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    int x = c * tileWidth;
                    int y = r * tileHeight;
                    int w = Math.Min(tileWidth, origWidth - x);
                    int h = Math.Min(tileHeight, origHeight - y);

                    if (w <= 0 || h <= 0) continue;

                    string tileId = Guid.NewGuid().ToString("N");
                    var cropRect = new Rectangle(x, y, w, h);

                    using var tileClone = image.Clone(ctx => ctx.Crop(cropRect));
                    using var ms = new MemoryStream();
                    await tileClone.SaveAsync(ms, webpEncoder, ct);
                    ms.Position = 0;

                    string tilePath = $"image/{msg.FileId:N}/{tileId}.webp";
                    await _storage.PutObjectAsync(ProcessedBucket, tilePath, ms, ms.Length, "image/webp", ct);

                    tiles.Add(new
                    {
                        Row = r,
                        Col = c,
                        TileId = tileId
                    });
                }
            }

            var manifestData = new
            {
                GridRows = gridRows,
                GridCols = gridCols,
                OriginalWidth = origWidth,
                OriginalHeight = origHeight,
                TileWidth = tileWidth,
                TileHeight = tileHeight,
                Tiles = tiles
            };

            string manifestJson = JsonSerializer.Serialize(manifestData);

            _publisher.Publish(QueueNames.CompletedQueue, new JobCompletedMessage(
                msg.FileId,
                msg.JobId,
                true,
                null,
                tiles.Count,
                manifestJson
            ));

            _logger.LogInformation("Successfully tiled image for FileId: {FileId}, Tiles: {Count}", msg.FileId, tiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed image tiling job for FileId: {FileId}", msg.FileId);
            _publisher.Publish(QueueNames.CompletedQueue, new JobCompletedMessage(
                msg.FileId,
                msg.JobId,
                false,
                ex.Message,
                0,
                "{}"
            ));
        }
    }
}
