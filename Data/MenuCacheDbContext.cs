using Microsoft.EntityFrameworkCore;

namespace StVrainToICSFunctionApp.Data;

public sealed class MenuCacheDbContext : DbContext
{
    public MenuCacheDbContext(DbContextOptions<MenuCacheDbContext> options)
        : base(options)
    {
    }

    public DbSet<MenuCacheEntry> MenuCacheEntries => Set<MenuCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuCacheEntry>(entity =>
        {
            entity.ToTable("MenuCache");
            entity.HasKey(e => e.CacheKey);
            entity.Property(e => e.CacheKey).HasMaxLength(256);
            entity.Property(e => e.MenuJson).IsRequired();
        });
    }
}
