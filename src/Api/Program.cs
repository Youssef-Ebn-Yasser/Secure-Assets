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

// Ensure DB is migrated / created — retry until Postgres is ready
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VaultDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    const int maxRetries = 10;
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            Console.WriteLine("DB initialized successfully.");
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            Console.WriteLine($"DB not ready (attempt {attempt}/{maxRetries}): {ex.Message}. Retrying in 3s...");
            Thread.Sleep(3000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB initialization failed after {maxRetries} attempts: {ex.Message}");
        }
    }

    // ── Admin Seed ────────────────────────────────────────────────────────────
    // Reads from env vars ADMIN_EMAIL / ADMIN_PASSWORD (with safe defaults).
    // Skips if an admin already exists — fully idempotent on every restart.
    try
    {
        string adminEmail    = config["Seed:AdminEmail"]    ?? "admin@vault.local";
        string adminPassword = config["Seed:AdminPassword"] ?? "Admin@Vault123!";

        bool adminExists = db.Users.Any(u => u.Role == UserRole.Admin);
        if (!adminExists)
        {
            var admin = new User
            {
                Id           = Guid.NewGuid(),
                Email        = adminEmail.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role         = UserRole.Admin,
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(admin);
            db.SaveChanges();
            Console.WriteLine($"Admin user seeded: {admin.Email}");
        }
        else
        {
            Console.WriteLine("Admin user already exists — skipping seed.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Admin seed warning: {ex.Message}");
    }
    // ─────────────────────────────────────────────────────────────────────────

    var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            storage.EnsureBucketExistsAsync("vault-raw").GetAwaiter().GetResult();
            storage.EnsureBucketExistsAsync("vault-processed").GetAwaiter().GetResult();
            Console.WriteLine("MinIO buckets ensured.");
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            Console.WriteLine($"MinIO not ready (attempt {attempt}/{maxRetries}): {ex.Message}. Retrying in 3s...");
            Thread.Sleep(3000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MinIO initialization failed after {maxRetries} attempts: {ex.Message}");
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
