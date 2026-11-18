namespace ArtifactX.GameData.Services;

using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class PakReaderService : IPakReaderService
{
    private const int HeaderSize = 48;
    private const int IndexEntrySize = 32; // 16-byte hash + UInt64 offset + UInt64 size
    private const int ChunkSizeEntrySize = 8; // UInt64 compressed size, per chunk

    /// <summary>
    /// Each PAK's header and file list, read once per build: a dozen catalog steps scan every PAK themselves (97 PAKs,
    /// ~1 s and ~900 log lines a scan; nine scans in the first two minutes of a build, 2026-11-17). Keyed by path, size
    /// and write time, so a PAK the game replaced is read again. Entries are only written while a PAK is read here, so
    /// sharing them between steps is safe. Cleared with the other build caches (CatalogSteps.ClearCache).
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Length, DateTime Written, PakHeader? Header, IReadOnlyList<PakEntry>? Entries)> Memo
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Forgets every PAK read (after a build).</summary>
    public static void ClearMemo() => Memo.Clear();

    private static (long Length, DateTime Written) Stamp(string pakPath)
    {
        var info = new FileInfo(pakPath);
        return (info.Length, info.LastWriteTimeUtc);
    }

    public IReadOnlyList<PakEntry> Read(string pakPath)
    {
        ValidatePakExists(pakPath);
        var stamp = Stamp(pakPath);
        if (Memo.TryGetValue(pakPath, out var known) && known.Length == stamp.Length && known.Written == stamp.Written
            && known.Entries is { } entries) return entries;
        var read = ReadUncached(pakPath);
        Memo.AddOrUpdate(pakPath, _ => (stamp.Length, stamp.Written, null, read),
            (_, old) => old.Length == stamp.Length && old.Written == stamp.Written ? old with { Entries = read } : (stamp.Length, stamp.Written, null, read));
        return read;
    }

    private IReadOnlyList<PakEntry> ReadUncached(string pakPath)
    {

        var header = ReadHeader(pakPath);
        PrintHeaderInfo(header, pakPath);

        if (header.FileCount <= 0)
            return Empty($"No file entries in {pakPath}");

        using var stream = File.OpenRead(pakPath);
        using var reader = new BinaryReader(stream);

        stream.Seek(HeaderSize, SeekOrigin.Begin);
        var entries = ReadIndexEntries(reader, header, pakPath);

        if (entries.Count == 0)
            return Empty($"Index entries missing in {pakPath}");

        // Entry[0] is always the manifest: a CRLF-separated list of every real
        // filename in the pak, stored/compressed exactly like any other file.
        var manifestEntry = entries[0];
        string[]? manifestNames = null;

        try
        {
            manifestNames = ReadManifestNames(stream, reader, header, manifestEntry, pakPath);
        }
        catch (Exception ex)
        {
            LogService.Write($"{Path.GetFileName(pakPath)}: Manifest read failed: {ex.Message}");
        }

        if (manifestNames != null && manifestNames.Length > 0)
        {
            MapNamesToEntries(entries, manifestNames);
        }
        else
        {
            LogService.Write($"{Path.GetFileName(pakPath)}: No manifest names recovered - keeping raw index entries.");
        }

        var result = entries.Skip(1).ToList(); // drop the manifest pseudo-entry
        LogService.Write($"Cataloged {result.Count} entries from {Path.GetFileName(pakPath)}.");
        return result;
    }

    public PakHeader ReadHeader(string pakPath)
    {
        ValidatePakExists(pakPath);
        var stamp = Stamp(pakPath);
        if (Memo.TryGetValue(pakPath, out var known) && known.Length == stamp.Length && known.Written == stamp.Written
            && known.Header is { } header) return header;
        var read = ReadHeaderUncached(pakPath);
        Memo.AddOrUpdate(pakPath, _ => (stamp.Length, stamp.Written, read, null),
            (_, old) => old.Length == stamp.Length && old.Written == stamp.Written ? old with { Header = read } : (stamp.Length, stamp.Written, read, null));
        return read;
    }

    private PakHeader ReadHeaderUncached(string pakPath)
    {

        using var stream = File.OpenRead(pakPath);
        using var reader = new BinaryReader(stream);

        var header = ReadHeaderCore(reader);

        if (header.IsCompressed && header.ChunkCount > 0)
        {
            // The chunk-size table sits immediately after the file index - NOT
            // computed backward from DataOffset.
            long chunkTableOffset = HeaderSize + ((long)header.FileCount * IndexEntrySize);
            stream.Seek(chunkTableOffset, SeekOrigin.Begin);
            ReadChunkTable(reader, header);
        }

        return header;
    }

    // -------------------------------------------------------------------------
    // Header (48 bytes total)
    // -------------------------------------------------------------------------

    private static PakHeader ReadHeaderCore(BinaryReader reader)
    {
        //  0: char[5]  Magic ("HGPAK")
        //  5: 3 bytes  padding
        //  8: UInt64   Version           (2 for the current format)
        // 16: UInt64   FileCount
        // 24: UInt64   ChunkCount
        // 32: byte     IsCompressed
        // 33: 7 bytes  padding
        // 40: UInt64   DataOffset

        string magic = Encoding.ASCII.GetString(reader.ReadBytes(5));
        reader.ReadBytes(3);

        ulong version = reader.ReadUInt64();
        ulong fileCount = reader.ReadUInt64();
        ulong chunkCount = reader.ReadUInt64();
        byte isCompressedByte = reader.ReadByte();
        reader.ReadBytes(7);
        ulong dataOffset = reader.ReadUInt64();

        if (magic != "HGPAK")
            throw new InvalidDataException($"Not an HGPAK file (magic was '{magic}').");

        return new PakHeader
        {
            Magic = magic,
            Version = version,
            FileCount = (int)fileCount,
            ChunkCount = (int)chunkCount,
            IsCompressed = isCompressedByte != 0,
            DataOffset = (long)dataOffset
        };
    }

    private static void PrintHeaderInfo(PakHeader header, string pakPath)
    {
        LogService.Write($"Header for {Path.GetFileName(pakPath)}:");
        LogService.Write($"  Magic:        {header.Magic}");
        LogService.Write($"  Version:      {header.Version}");
        LogService.Write($"  FileCount:    {header.FileCount}");
        LogService.Write($"  ChunkCount:   {header.ChunkCount}");
        LogService.Write($"  IsCompressed: {header.IsCompressed}");
        LogService.Write($"  DataOffset:   {header.DataOffset}");
        LogService.Write("");
    }

    // -------------------------------------------------------------------------
    // File index: FileCount entries, 32 bytes each
    // (16-byte hash + UInt64 offset + UInt64 decompressed size)
    // -------------------------------------------------------------------------

    private List<PakEntry> ReadIndexEntries(BinaryReader reader, PakHeader header, string pakPath)
    {
        var entries = new List<PakEntry>(header.FileCount);

        for (int i = 0; i < header.FileCount; i++)
        {
            try
            {
                entries.Add(ReadIndexEntry(reader, header));
            }
            catch (Exception ex)
            {
                LogService.Write($"Error reading index entry {i} in {Path.GetFileName(pakPath)}: {ex.Message}");
                return new List<PakEntry>();
            }
        }

        return entries;
    }

    private static PakEntry ReadIndexEntry(BinaryReader reader, PakHeader header)
    {
        byte[] hash = reader.ReadBytes(16);
        long offset = (long)reader.ReadUInt64();
        long size = (long)reader.ReadUInt64();

        return new PakEntry
        {
            Hash = hash,
            Offset = offset,
            Size = size,
            RelativeOffset = header.IsCompressed ? offset - header.DataOffset : offset,
            FileName = string.Empty
        };
    }

    // -------------------------------------------------------------------------
    // Chunk-size table (compressed PAKs only): ChunkCount x UInt64 compressed sizes.
    // Offsets aren't stored - derive them by walking sizes, 16-byte aligned,
    // starting at header.DataOffset.
    // -------------------------------------------------------------------------

    private static void ReadChunkTable(BinaryReader reader, PakHeader header)
    {
        header.Chunks.Clear();

        var sizes = new long[header.ChunkCount];
        for (int i = 0; i < header.ChunkCount; i++)
            sizes[i] = (long)reader.ReadUInt64();

        long cursor = header.DataOffset;
        foreach (long size in sizes)
        {
            header.Chunks.Add(new PakChunk { Offset = cursor, Size = size });
            cursor += RoundUpTo16(size);
        }
    }

    private static long RoundUpTo16(long size) => (size + 15) / 16 * 16;

    // -------------------------------------------------------------------------
    // Manifest (entry 0): CRLF-separated real filenames. For compressed PAKs
    // this text itself lives in the chunk-compressed data stream at relative
    // offset 0, so it must be decompressed chunk-by-chunk like any other file.
    // -------------------------------------------------------------------------

    private string[]? ReadManifestNames(
        Stream stream, BinaryReader reader, PakHeader header, PakEntry manifestEntry, string pakPath)
    {
        if (manifestEntry.Size <= 0)
        {
            LogService.Write($"{Path.GetFileName(pakPath)}: Manifest size <= 0");
            return null;
        }

        byte[] manifestBytes;

        if (!header.IsCompressed)
        {
            stream.Seek(manifestEntry.Offset, SeekOrigin.Begin);
            manifestBytes = reader.ReadBytes((int)manifestEntry.Size);
        }
        else
        {
            int chunksNeeded = (int)((manifestEntry.Size + ChunkDecompressor.DecompressedChunkSize - 1)
                                      / ChunkDecompressor.DecompressedChunkSize);

            if (chunksNeeded > header.Chunks.Count)
            {
                LogService.Write($"{Path.GetFileName(pakPath)}: Manifest needs {chunksNeeded} chunks but only {header.Chunks.Count} exist.");
                return null;
            }

            using var buffer = new MemoryStream();
            for (int i = 0; i < chunksNeeded; i++)
            {
                byte[]? decompressed = ChunkDecompressor.DecompressChunk(stream, reader, header.Chunks[i]);
                if (decompressed == null)
                {
                    LogService.Write($"{Path.GetFileName(pakPath)}: Failed to decompress manifest chunk {i}.");
                    return null;
                }
                buffer.Write(decompressed, 0, decompressed.Length);
            }

            byte[] all = buffer.ToArray();
            if (all.Length < manifestEntry.Size)
            {
                LogService.Write($"{Path.GetFileName(pakPath)}: Decompressed manifest data shorter than expected.");
                return null;
            }

            manifestBytes = new byte[manifestEntry.Size];
            Buffer.BlockCopy(all, 0, manifestBytes, 0, (int)manifestEntry.Size);
        }

        string manifestText = Encoding.UTF8.GetString(manifestBytes).TrimEnd('\r', '\n');
        var names = manifestText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (names.Length != header.FileCount - 1)
        {
            LogService.Write(
                $"{Path.GetFileName(pakPath)}: manifest name count ({names.Length}) != FileCount-1 ({header.FileCount - 1}) - names may be misaligned.");
        }

        return names;
    }

    private static void MapNamesToEntries(List<PakEntry> entries, string[] names)
    {
        for (int i = 1; i < entries.Count; i++)
        {
            int nameIndex = i - 1;
            entries[i].FileName = nameIndex < names.Length ? names[nameIndex] : $"<unknown_{i}>";
        }
    }

    // -------------------------------------------------------------------------

    private static void ValidatePakExists(string pakPath)
    {
        if (!File.Exists(pakPath))
            throw new FileNotFoundException($"PAK file not found: {pakPath}");
    }

    private static IReadOnlyList<PakEntry> Empty(string reason)
    {
        LogService.Write(reason);
        return Array.Empty<PakEntry>();
    }
}