namespace NMS.Tools.DataCataloger.Models;

public class PakChunk
{
    /// <summary>Absolute byte offset of this compressed chunk within the .pak file.</summary>
    public long Offset { get; set; }

    /// <summary>Compressed (on-disk) size of this chunk, as stored in the chunk-size table.</summary>
    public long Size { get; set; }
}