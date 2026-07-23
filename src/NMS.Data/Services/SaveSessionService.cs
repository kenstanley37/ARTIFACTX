using Microsoft.EntityFrameworkCore;
using NMS.Core;
using NMS.Data.Models;
using System.Diagnostics;

namespace NMS.Data.Services;

public class SaveSessionService
{
    /// <summary>
    /// Coordinates the full extraction pipeline: decompresses the raw .hg file, 
    /// streams tokens via Utf8JsonReader, and flattens data into SQLite tables.
    /// </summary>
    public async Task<SaveSession> LoadAndIngestSaveAsync(string filePath)
    {
        // --- PATH TRACE DIAGNOSTIC ---
        Debug.WriteLine($"\n[NMS-FILE-SYSTEM] CRITICAL: Ingestion service is reading file from path: {Path.GetFullPath(filePath)}\n");

        // 1. Unpack raw LZ4 container blocks via our stateless Core library
        using Stream rawJsonStream = await SaveStreamProcessor.DecompressSaveToStreamAsync(filePath);

        // 2. Process data stream chunk-by-chunk directly into our local SQLite relational store
        await SaveIngestionService.IngestSaveStreamAsync(rawJsonStream, filePath);

        // 3. Fetch and return the newly materialized database record mapping
        var session = await GetActiveSessionAsync();
        return session ?? throw new InvalidOperationException("Failed to initialize game save database transaction context.");
    }

    /// <summary>
    /// Retreives the current loaded save profile data straight from the SQLite local cache.
    /// </summary>
    public async Task<SaveSession?> GetActiveSessionAsync()
    {
        using var context = new NmsDbContext();
        return await context.SaveSessions
            .Include(s => s.PlayerState)
            .FirstOrDefaultAsync();
    }
}