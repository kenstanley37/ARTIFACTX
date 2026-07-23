namespace NMS.Tools.DataCataloger.Models;

public class PakEntry
{
    /// <summary>16-byte MD5 hash of the lowercased, forward-slashed file path.</summary>
    public byte[] Hash { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Raw start offset exactly as stored in the file index.
    /// For uncompressed PAKs this is an absolute file offset.
    /// For compressed PAKs this is an offset into the decompressed chunk
    /// stream that begins at PakHeader.DataOffset - use RelativeOffset instead.
    /// </summary>
    public long Offset { get; set; }

    /// <summary>Decompressed size of this file in bytes. There is no separate on-disk/"ZSize" per entry.</summary>
    public long Size { get; set; }

    /// <summary>
    /// Offset - header.DataOffset, computed at read time. This is what you index into
    /// the concatenated decompressed chunk stream with. Equal to Offset for uncompressed PAKs.
    /// </summary>
    public long RelativeOffset { get; set; }

    public string FileName { get; set; } = string.Empty;
}