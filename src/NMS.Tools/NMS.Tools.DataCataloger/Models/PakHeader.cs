namespace NMS.Tools.DataCataloger.Models;

public class PakHeader
{
    public string Magic { get; set; } = "";
    public ulong Version { get; set; }
    public int FileCount { get; set; }
    public int ChunkCount { get; set; }
    public bool IsCompressed { get; set; }

    /// <summary>
    /// Absolute byte offset in the .pak file where the (possibly chunk-compressed)
    /// data section begins. Header + file index + chunk-size table all live before this.
    /// </summary>
    public long DataOffset { get; set; }

    /// <summary>
    /// Populated only for compressed PAKs. Each chunk decompresses to exactly
    /// 0x10000 bytes (Windows/zstd), except possibly the final chunk in the file.
    /// Offsets here are computed (not stored) - see PakReaderService.ReadChunkTable.
    /// </summary>
    public List<PakChunk> Chunks { get; set; } = new();
}