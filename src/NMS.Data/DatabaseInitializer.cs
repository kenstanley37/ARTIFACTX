namespace NMS.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Ensures the local SQLite cache database file and schemas exist on application initialization.
    /// </summary>
    public static async Task InitializeAsync()
    {
        using var context = new NmsDbContext();

        // This automatically creates the local AnomalyCache.db file and applies tables
        // if they do not exist, ensuring a zero-setup start for the end-user.
        await context.Database.EnsureCreatedAsync();
    }
}