using Microsoft.EntityFrameworkCore;
using ArtifactX.Tools.DataCataloger.Models;

namespace ArtifactX.Tools.DataCataloger.Data;

public class CatalogDbContext : DbContext
{
    public DbSet<CatalogCategory> Categories => Set<CatalogCategory>();
    public DbSet<CatalogItem> Items => Set<CatalogItem>();
    public DbSet<IconAsset> Icons => Set<IconAsset>();
    public DbSet<IconBlob> IconBlobs => Set<IconBlob>();
    public DbSet<LocalizedText> LocalizedTexts => Set<LocalizedText>();

    private readonly string _dbPath;

    public CatalogDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={_dbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogCategory>()
            .HasMany(c => c.Items)
            .WithOne(i => i.Category!)
            .HasForeignKey(i => i.CategoryId);

        modelBuilder.Entity<CatalogItem>()
            .HasMany(i => i.Icons)
            .WithOne(a => a.Item!)
            .HasForeignKey(a => a.ItemId);

        modelBuilder.Entity<IconAsset>()
            .HasOne(a => a.IconBlob)
            .WithMany(b => b.Assets)
            .HasForeignKey(a => a.IconBlobId);

        modelBuilder.Entity<IconBlob>()
            .HasIndex(b => b.SourceDdsPath)
            .IsUnique();

        modelBuilder.Entity<CatalogItem>()
            .HasIndex(i => new { i.CategoryId, i.GameId });
        // NOT unique: some source tables (particularly procedural-generation
        // tables like GcProceduralTechnologyTable) legitimately reuse the same
        // GameId across multiple rows - that's a property of the game data, not
        // a bug in extraction. A regular index is still useful for lookups.

        modelBuilder.Entity<LocalizedText>()
            .HasIndex(l => new { l.LocKey, l.Language })
            .IsUnique();
    }
}