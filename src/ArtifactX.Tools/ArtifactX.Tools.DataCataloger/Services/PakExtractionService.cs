using ArtifactX.Tools.DataCataloger.Models;

namespace ArtifactX.Tools.DataCataloger.Services;

public class PakExtractionService
{
    private const int DecompressedChunkSize = ChunkDecompressor.DecompressedChunkSize;

    public byte[]? ExtractEntryBytes(string pakPath, PakEntry entry, PakHeader header)
    {
        using var stream = File.OpenRead(pakPath);
        using var reader = new BinaryReader(stream);

        if (!header.IsCompressed)
        {
            stream.Seek(entry.Offset, SeekOrigin.Begin);
            return reader.ReadBytes((int)entry.Size);
        }

        long offset = entry.RelativeOffset; // offset into the decompressed chunk stream
        long size = entry.Size;

        int startChunk = DetermineStartChunk(offset, DecompressedChunkSize);
        int endChunk = DetermineEndChunk(offset + size, DecompressedChunkSize);

        if (startChunk < 0 || endChunk >= header.Chunks.Count || endChunk < startChunk)
        {
            LogService.Write($"  Chunk range [{startChunk},{endChunk}] invalid (ChunkCount={header.Chunks.Count}) for {entry.FileName}");
            return null;
        }

        using var buffer = new MemoryStream();
        for (int i = startChunk; i <= endChunk; i++)
        {
            byte[]? decompressed = ChunkDecompressor.DecompressChunk(stream, reader, header.Chunks[i]);
            if (decompressed == null)
            {
                LogService.Write($"  Failed to decompress chunk {i} for {entry.FileName}");
                return null;
            }
            buffer.Write(decompressed, 0, decompressed.Length);
        }

        byte[] full = buffer.ToArray();
        long sliceStart = offset - ((long)startChunk * DecompressedChunkSize);

        if (sliceStart < 0 || sliceStart + size > full.Length)
        {
            LogService.Write($"  Slice out of range for {entry.FileName} (start={sliceStart}, size={size}, buffer={full.Length})");
            return null;
        }

        byte[] result = new byte[size];
        Buffer.BlockCopy(full, (int)sliceStart, result, 0, (int)size);
        return result;
    }

    // ceil(numBytes / binSize)
    private static int DetermineBins(long numBytes, long binSize) => (int)((numBytes + binSize - 1) / binSize);

    private static int DetermineStartChunk(long offset, long chunkSize) =>
        offset % chunkSize == 0 ? DetermineBins(offset, chunkSize) : DetermineBins(offset, chunkSize) - 1;

    private static int DetermineEndChunk(long offsetPlusSize, long chunkSize) =>
        DetermineBins(offsetPlusSize, chunkSize) - 1;

}
