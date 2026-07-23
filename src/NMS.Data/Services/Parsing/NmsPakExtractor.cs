using K4os.Compression.LZ4;
using System.Text;

namespace NMS.Data.Services.Parsing;

public class NmsPakExtractor
{
    public class PakFileEntry
    {
        public string Path { get; set; } = string.Empty;
        public long Offset { get; set; }
        public int CompressedSize { get; set; }
        public int DecompressedSize { get; set; }
    }

    /// <summary>
    /// Natively opens a .PAK archive, scans its metadata allocation header table, and extracts target localization strings directly into memory buffers.
    /// </summary>
    public static byte[] ExtractTargetAsset(string pakPath, string internalTargetFile)
    {
        using var stream = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        // 1. Verify Header File Identity Cookie
        uint magic = reader.ReadUInt32();
        // Note: Validate against the game version's explicit PAK magic tag identification sequence

        // 2. Read Allocation Table Count Metrics
        int totalFilesCount = reader.ReadInt32();
        var indexTable = new List<PakFileEntry>();

        for (int i = 0; i < totalFilesCount; i++)
        {
            // Read internal paths (fixed-length byte sequences or prefixed string length blocks)
            string fileVirtualPath = Encoding.UTF8.GetString(reader.ReadBytes(128)).TrimEnd('\0');
            long dataOffset = reader.ReadInt64();
            int compSize = reader.ReadInt32();
            int decompSize = reader.ReadInt32();

            if (fileVirtualPath.Equals(internalTargetFile, StringComparison.OrdinalIgnoreCase))
            {
                // Isolate target item mapping match instance
                stream.Seek(dataOffset, SeekOrigin.Begin);
                byte[] compressedBytes = reader.ReadBytes(compSize);
                byte[] decompressedOutput = new byte[decompSize];

                // 3. Perform In-Memory Decompression via Type-Agnostic Block Decoding
                LZ4Codec.Decode(compressedBytes, 0, compSize, decompressedOutput, 0, decompSize);
                return decompressedOutput;
            }
        }

        throw new FileNotFoundException($"Target asset file '{internalTargetFile}' was not found within archive metadata mappings.");
    }
}