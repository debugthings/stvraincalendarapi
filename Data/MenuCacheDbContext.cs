using Microsoft.EntityFrameworkCore;

namespace StVrainToICSFunctionApp.Data;

public sealed class MenuCacheDbContext : DbContext
{
    public MenuCacheDbContext(DbContextOptions<MenuCacheDbContext> options)
        : base(options)
    {
    }

    public DbSet<MenuCacheEntry> MenuCacheEntries => Set<MenuCacheEntry>();

    public DbSet<FastLinkEntry> FastLinks => Set<FastLinkEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuCacheEntry>(entity =>
        {
            entity.ToTable("MenuCache");
            entity.HasKey(e => e.CacheKey);
            entity.Property(e => e.CacheKey).HasMaxLength(256);
            entity.Property(e => e.MenuJson).IsRequired();
        });

        modelBuilder.Entity<FastLinkEntry>(entity =>
        {
            entity.ToTable("FastLinks");
            entity.HasKey(e => e.Slug);
            entity.Property(e => e.Slug).HasMaxLength(64);
            entity.Property(e => e.BuildingId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.DistrictId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.SchoolName).HasMaxLength(256);
            entity.Property(e => e.Session).HasMaxLength(32);
            entity.Property(e => e.IncludedPlansJson).IsRequired();
        });
    }
}
