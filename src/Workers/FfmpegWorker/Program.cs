using FfmpegWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;
using Shared.Messaging;
using Shared.Storage;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var config = hostContext.Configuration;

        // MinIO
        var minioEndpoint = config["Minio:Endpoint"] ?? "localhost:9000";
        var minioUser = config["Minio:AccessKey"] ?? "minioadmin";
        var minioPass = config["Minio:SecretKey"] ?? "minioadmin";
        var minioUseSsl = bool.TryParse(config["Minio:UseSSL"], out var ssl) && ssl;

        var minioClient = new MinioClient()
            .WithEndpoint(minioEndpoint)
            .WithCredentials(minioUser, minioPass);
        if (minioUseSsl) minioClient = minioClient.WithSSL();

        services.AddSingleton<IMinioClient>(minioClient.Build());
        services.AddSingleton<IStorageService, MinioStorageService>();

        // RabbitMQ
        var rabbitHost = config["Rabbit:Host"] ?? "localhost";
        var rabbitUser = config["Rabbit:User"] ?? "guest";
        var rabbitPass = config["Rabbit:Pass"] ?? "guest";
        int rabbitPort = int.TryParse(config["Rabbit:Port"], out int p) ? p : 5672;
        services.AddSingleton<IRabbitMqPublisher>(new RabbitMqPublisher(rabbitHost, rabbitUser, rabbitPass, rabbitPort));

        services.AddHostedService<VideoWorkerService>();
    })
    .Build();

await host.RunAsync();
