using Microsoft.EntityFrameworkCore;
using NMS.Data.Models;

namespace NMS.Data;

public class NmsDbContext : DbContext
{
    public DbSet<SaveSession> SaveSessions => Set<SaveSession>();
    public DbSet<PlayerState> PlayerStates => Set<PlayerState>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    //public DbSet<InventorySlot> InventorySlots => Set<InventorySlot>();

    public NmsDbContext()
    {
        // Ensure the database is created and ready for use
        Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Directs EF Core to instantiate an auto-contained local SQLite cache file inside our project layout
        string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AnomalyCache.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enforce cascading deletes so if a session is closed or cleared, the cache drops cleanly
        modelBuilder.Entity<SaveSession>()
            .HasOne(s => s.PlayerState)
            .WithOne(p => p.SaveSession)
            .HasForeignKey<PlayerState>(p => p.SaveSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure cascade deletion for inventory items
        modelBuilder.Entity<InventoryItem>()
            .HasOne(i => i.Session)
            .WithMany()
            .HasForeignKey(i => i.SaveSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure cascade deletion for polymorphic inventory slots
        /*
        modelBuilder.Entity<InventorySlot>()
            .HasOne(slot => slot.SaveSession)
            .WithMany(session => session.InventorySlots)
            .HasForeignKey(slot => slot.SaveSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        */
    }
}