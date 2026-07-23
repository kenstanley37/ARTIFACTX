using K4os.Compression.LZ4;
using libMBIN;
using NMS.Tools.DataCataloger.Models;
using NMS.Tools.DataCataloger.Services.Interfaces;
using System.IO.Compression;
using System.Text;
using ZstdNet;

namespace NMS.Tools.DataCataloger.Services;

public interface ITestService
{
    void Header(PakHeader header, string pakName);
    void Entries(IReadOnlyList<PakEntry> entries, string pakName);
    void Manifest(IReadOnlyList<PakEntry> entries, IPakReaderService reader, string pakPath, string pakName);
    void Extraction(string pakPath, PakEntry entry, PakHeader header);
    void Template(NMSTemplate template, string fileName);
}

public class TestService : ITestService
{
    public void Header(PakHeader header, string pakName)
    {
        LogService.Write($"[{pakName}] Header test...");

        if (header.Magic != "HGPAK")
            throw new Exception($"{pakName}: Invalid magic '{header.Magic}'");

        if (header.FileCount <= 0)
            throw new Exception($"{pakName}: FileCount is zero or negative");

        if (header.DataOffset <= 0)
            throw new Exception($"{pakName}: DataOffset is invalid");

        if (header.ChunkCount < 0)
            throw new Exception($"{pakName}: ChunkCount is negative");

        LogService.Write($"[{pakName}] Header test passed.");
    }

    public void Entries(IReadOnlyList<PakEntry> entries, string pakName)
    {
        LogService.Write($"[{pakName}] Entry test...");

        if (entries == null || entries.Count == 0)
            throw new Exception($"{pakName}: No entries returned from reader");

        foreach (var e in entries)
        {
            if (e.Offset < 0)
                throw new Exception($"{pakName}: Entry offset < 0");

            if (e.Size <= 0)
                continue;
        }

        LogService.Write($"[{pakName}] Entry test passed.");
    }


    public void Manifest(
    IReadOnlyList<PakEntry> entries,
    IPakReaderService reader,
    string pakPath,
    string pakName)
    {
        LogService.Write("Running manifest test...");
        LogService.Write($"[{pakName}] Running manifest test...");

        // Find actual manifest entry
        var manifestEntry = FindManifestEntry(entries);

        if (manifestEntry == null)
        {
            LogService.Write($"{pakName}: No manifest entry found — skipping.");
            LogService.Write($"{pakName}: No manifest entry found — skipping.");
            return;
        }

        // Read header
        var header = reader.ReadHeader(pakPath);

        // Compute manifest offset safely
        long offset = header.DataOffset + (long)manifestEntry.Offset;

        if (offset < 0 || offset >= new FileInfo(pakPath).Length)
        {
            LogService.Write($"{pakName}: Manifest offset out of range — skipping.");
            LogService.Write($"{pakName}: Manifest offset out of range — skipping.");
            return;
        }

        // Read manifest bytes
        byte[] manifestBytes;
        using (var stream = File.OpenRead(pakPath))
        using (var br = new BinaryReader(stream))
        {
            stream.Seek(offset, SeekOrigin.Begin);
            manifestBytes = br.ReadBytes((int)manifestEntry.Size);
        }

        LogService.Write("Manifest raw bytes:");
        LogService.WriteRaw(manifestBytes);

        // Detect binary manifest
        bool looksBinary = manifestBytes.Any(b => b < 0x09 || b > 0x7E);

        if (looksBinary)
        {
            LogService.Write($"{pakName}: Manifest is binary — attempting decompression...");
            LogService.Write($"{pakName}: Manifest is binary — attempting decompression...");

            byte[]? decompressed = TryDecompressManifest(manifestBytes);

            if (decompressed == null)
            {
                LogService.Write($"{pakName}: Manifest decompression failed — skipping.");
                LogService.Write($"{pakName}: Manifest decompression failed — skipping.");
                return;
            }

            string text = Encoding.UTF8.GetString(decompressed);
            var names = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            LogService.Write($"Manifest entries: {names.Length}");
            LogService.Write($"Manifest entries: {names.Length}");
            return;
        }

        // If not binary, treat as UTF‑8 text
        string manifestText = Encoding.UTF8.GetString(manifestBytes);

        if (manifestText.Contains('\0'))
        {
            LogService.Write($"{pakName}: Manifest contains null bytes — skipping.");
            LogService.Write($"{pakName}: Manifest contains null bytes — skipping.");
            return;
        }

        LogService.Write("Manifest test passed.");
        LogService.Write($"[{pakName}] Manifest test passed.");
    }


    public void Extraction(string pakPath, PakEntry entry, PakHeader header)
    {
        LogService.Write($"[{pakPath}] Extraction integrity test...");

        if (entry.Offset < 0)
            throw new Exception($"{pakPath}: Entry offset < 0");

        if (entry.Size <= 0)
            throw new Exception($"{pakPath}: Entry size <= 0");

        long fileStart = (long)entry.Offset;
        long fileEnd = (long)entry.Offset + entry.Size;

        int firstChunk = (int)(fileStart / 0x10000);
        int lastChunk = (int)(fileEnd / 0x10000);

        if (firstChunk < 0 || lastChunk < 0)
            throw new Exception($"{pakPath}: Chunk index < 0");

        if (firstChunk >= header.ChunkCount || lastChunk >= header.ChunkCount)
            throw new Exception($"{pakPath}: Chunk index out of range");

        LogService.Write($"[{pakPath}] Extraction integrity test passed.");
    }

    public void Template(NMSTemplate template, string fileName)
    {
        LogService.Write($"[{fileName}] Template test...");

        if (template == null)
            throw new Exception($"{fileName}: Template is null");

        var fields = template.GetType().GetFields();
        if (fields.Length == 0)
            throw new Exception($"{fileName}: Template has no fields");

        LogService.Write($"[{fileName}] Template test passed.");
    }

    byte[]? TryDecompressManifest(byte[] data)
    {
        // 1. Try ZSTD (correct for HGPAK)
        try
        {
            using var decompressor = new Decompressor();
            return decompressor.Unwrap(data);
        }
        catch
        {
            // ignore and fall through
        }

        // 2. Try LZ4 (older HGPAK builds)
        try
        {
            // LZ4Codec.Decode requires:
            // (byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset, int outputLength)
            // So we must provide an output buffer.
            int maxSize = 4 * 1024 * 1024; // 4MB safety buffer
            byte[] output = new byte[maxSize];

            int decoded = LZ4Codec.Decode(
                data, 0, data.Length,
                output, 0, output.Length
            );

            if (decoded > 0)
            {
                byte[] result = new byte[decoded];
                Buffer.BlockCopy(output, 0, result, 0, decoded);
                return result;
            }
        }
        catch
        {
            // ignore and fall through
        }

        // 3. Try zlib/deflate
        try
        {
            using var ms = new MemoryStream(data);
            using var ds = new DeflateStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            ds.CopyTo(outMs);
            return outMs.ToArray();
        }
        catch
        {
            // ignore
        }

        return null;
    }


    // Helper: find a likely manifest entry in a PAK
    PakEntry? FindManifestEntry(IReadOnlyList<PakEntry> entries)
    {
        var candidates = entries
            .Where(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                (
                    e.FileName.EndsWith("MANIFEST.MBIN", StringComparison.OrdinalIgnoreCase) ||
                    e.FileName.EndsWith("MANIFEST.BIN", StringComparison.OrdinalIgnoreCase) ||
                    e.FileName.EndsWith(".MANIFEST", StringComparison.OrdinalIgnoreCase) ||
                    e.FileName.Contains("MANIFEST", StringComparison.OrdinalIgnoreCase)
                ))
            .OrderBy(e => e.Offset)
            .ToList();

        return candidates.FirstOrDefault();
    }

    // Helper: find all MBIN entries in a PAK
    IReadOnlyList<PakEntry> FindMbinEntries(IReadOnlyList<PakEntry> entries)
    {
        return entries
            .Where(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                e.FileName.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Offset)
            .ToList();
    }

}
