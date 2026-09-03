using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace Shared.Data;

public class VaultDbContext : DbContext
{
    public VaultDbContext(DbContextOptions<VaultDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<MediaFile> Files => Set<MediaFile>();
    public DbSet<ChunkManifest> ChunkManifests => Set<ChunkManifest>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();
    public DbSet<AccessTokenLog> AccessTokenLogs => Set<AccessTokenLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasOne(f => f.Owner)
                  .WithMany(u => u.Files)
                  .HasForeignKey(f => f.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.Manifest)
                  .WithOne(m => m.File)
                  .HasForeignKey<ChunkManifest>(m => m.FileId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProcessingJob>(entity =>
        {
            entity.HasOne(j => j.File)
                  .WithMany(f => f.Jobs)
                  .HasForeignKey(j => j.FileId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessTokenLog>(entity =>
        {
            entity.HasIndex(t => new { t.FileId, t.ChunkId });
        });
    }
}
