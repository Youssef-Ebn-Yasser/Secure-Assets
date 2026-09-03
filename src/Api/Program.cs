using System.Text;
using Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;
using Shared.Data;
using Shared.Messaging;
using Shared.Security;
using Shared.Storage;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure large file uploads (500MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
});
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 524_288_000;
});

// Database
var pgConn = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=vault;Username=postgres;Password=postgres";

builder.Services.AddDbContext<VaultDbContext>(options =>
{
    options.UseNpgsql(pgConn);
});

// Redis (optional connection)
string redisConn = builder.Configuration["Redis:Host"] ?? "localhost:6379";
IConnectionMultiplexer? redisMultiplexer = null;
try
{
    redisMultiplexer = ConnectionMultiplexer.Connect(redisConn);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);
}
catch
{
    // Redis connection is optional in dev, fallback to memory/direct checks
}

// Security & Tokens
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "default-super-secret-key-32-chars-long-secure-vault!";
builder.Services.AddSingleton<ITokenService>(sp => new TokenService(jwtSecret, redisMultiplexer));

// Storage (MinIO)
var minioEndpoint = builder.Configuration["Minio:Endpoint"] ?? "localhost:9000";
var minioUser = builder.Configuration["Minio:AccessKey"] ?? "minioadmin";
var minioPass = builder.Configuration["Minio:SecretKey"] ?? "minioadmin";
var minioUseSsl = bool.TryParse(builder.Configuration["Minio:UseSSL"], out var ssl) && ssl;

var minioClient = new MinioClient()
    .WithEndpoint(minioEndpoint)
    .WithCredentials(minioUser, minioPass);

if (minioUseSsl)
{
    minioClient = minioClient.WithSSL();
}

var builtMinioClient = minioClient.Build();
builder.Services.AddSingleton<IMinioClient>(builtMinioClient);
builder.Services.AddSingleton<IStorageService, MinioStorageService>();

// RabbitMQ Publisher
var rabbitHost = builder.Configuration["Rabbit:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["Rabbit:User"] ?? "guest";
var rabbitPass = builder.Configuration["Rabbit:Pass"] ?? "guest";
int rabbitPort = int.TryParse(builder.Configuration["Rabbit:Port"], out int p) ? p : 5672;
builder.Services.AddSingleton<IRabbitMqPublisher>(sp => new RabbitMqPublisher(rabbitHost, rabbitUser, rabbitPass, rabbitPort));

// Background consumer for completed jobs
builder.Services.AddHostedService<CompletedJobConsumerService>();

// JWT Authentication
var keyBytes = Encoding.UTF8.GetBytes(jwtSecret);
if (keyBytes.Length < 32) Array.Resize(ref keyBytes, 32);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.FromMinutes(5)
    };
});

builder.Services.AddAuthorization();

// CORS for Frontend & Gateway
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Secure Media Vault API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Ensure DB is migrated / created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VaultDbContext>();
    try
    {
        db.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DB initialization notice: {ex.Message}");
    }

    var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
    try
    {
        storage.EnsureBucketExistsAsync("vault-raw").GetAwaiter().GetResult();
        storage.EnsureBucketExistsAsync("vault-processed").GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"MinIO initialization notice: {ex.Message}");
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
