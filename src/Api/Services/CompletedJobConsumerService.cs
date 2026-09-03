using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Data;
using Shared.Messaging;
using Shared.Models;

namespace Api.Services;

public class CompletedJobConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<CompletedJobConsumerService> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public CompletedJobConsumerService(
        IServiceProvider serviceProvider,
        IConfiguration config,
        ILogger<CompletedJobConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
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
                    queue: QueueNames.CompletedQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var messageJson = Encoding.UTF8.GetString(body);

                    try
                    {
                        var completedMsg = JsonSerializer.Deserialize<JobCompletedMessage>(messageJson);
                        if (completedMsg != null)
                        {
                            await ProcessJobCompletedAsync(completedMsg);
                        }
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling JobCompletedMessage: {Message}", messageJson);
                        _channel.BasicNack(ea.DeliveryTag, false, false);
                    }
                };

                _channel.BasicConsume(
                    queue: QueueNames.CompletedQueue,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("CompletedJobConsumerService started listening on {Queue}", QueueNames.CompletedQueue);

                // Wait until cancellation requested or connection closed
                while (!stoppingToken.IsCancellationRequested && _channel.IsOpen)
                {
                    await Task.Delay(2000, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("RabbitMQ connection error in CompletedJobConsumer: {Message}. Retrying in 5 seconds...", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessJobCompletedAsync(JobCompletedMessage msg)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VaultDbContext>();

        var file = await db.Files.Include(f => f.Manifest).FirstOrDefaultAsync(f => f.Id == msg.FileId);
        var job = await db.ProcessingJobs.FirstOrDefaultAsync(j => j.Id == msg.JobId);

        if (file != null)
        {
            file.Status = msg.Success ? FileStatus.Ready : FileStatus.Failed;
            file.ProcessedAt = DateTime.UtcNow;

            if (msg.Success)
            {
                if (file.Manifest == null)
                {
                    file.Manifest = new ChunkManifest
                    {
                        Id = Guid.NewGuid(),
                        FileId = file.Id,
                        ManifestJson = msg.ManifestJson,
                        ChunkCount = msg.ChunkCount,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.ChunkManifests.Add(file.Manifest);
                }
                else
                {
                    file.Manifest.ManifestJson = msg.ManifestJson;
                    file.Manifest.ChunkCount = msg.ChunkCount;
                }
            }
        }

        if (job != null)
        {
            job.Status = msg.Success ? JobStatus.Completed : JobStatus.Failed;
            job.LastError = msg.ErrorMessage;
            job.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Processed completion for FileId: {FileId}, Status: {Status}", msg.FileId, file?.Status);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
