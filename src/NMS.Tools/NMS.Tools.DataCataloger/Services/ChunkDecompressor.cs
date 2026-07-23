using NMS.Tools.DataCataloger.Models;
using ZstdNet;

namespace NMS.Tools.DataCataloger.Services;

/// <summary>
/// Decompresses a single HGPAK data chunk. Each chunk is an independent zstd frame
/// (written with content-size embedded), so chunks must be decompressed one at a
/// time - concatenating raw compressed bytes across chunks before decompressing
/// does NOT work.
/// </summary>
public static class ChunkDecompressor
{
    public const int DecompressedChunkSize = 0x10000; // Windows/zstd chunk size

    public static byte[]? DecompressChunk(Stream stream, BinaryReader reader, PakChunk chunk)
    {
        stream.Seek(chunk.Offset, SeekOrigin.Begin);
        byte[] compressed = reader.ReadBytes((int)chunk.Size);

        try
        {
            using var decompressor = new Decompressor();
            return decompressor.Unwrap(compressed);
        }
        catch
        {
            // Rare: a chunk that didn't compress well may be stored raw at full chunk size.
            return compressed.Length == DecompressedChunkSize ? compressed : null;
        }
    }
}
