using K4os.Compression.LZ4;
using System.Diagnostics;

namespace NMS.Core;

public class SaveStreamProcessor
{
    // The magic bytes Hello Games uses to indicate a valid save block container
    private static readonly byte[] SteamMagicHeader = new byte[] { 0xE5, 0xA1, 0xED, 0xFE }; // "4E4D4D53" maps to "DNMS" 
    private const uint ExpectedMagic = 0xFEEDA1E5;
    /// <summary>
    /// Reads only the first 32 bytes of the save file to identify the container layout structure.
    /// </summary>
    public static async Task<string> InspectFileHeaderAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Target game save file not found: {filePath}");

        byte[] buffer = new byte[32];
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
        {
            await stream.ReadExactlyAsync(buffer, 0, 32);
        }
        return BitConverter.ToString(buffer).Replace("-", " ");
    }

    /// <summary>
    /// Strips the container header, reads the sequential block maps, and returns a raw uncompressed JSON data stream.
    /// </summary>
    public static async Task<Stream> DecompressSaveToStreamAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Target game save file not found: {filePath}");

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var decompressedOutputMemory = new MemoryStream();

        // 16-byte header buffer to parse: Magic (4B) + CompressedSize (4B) + DecompressedSize (8B)
        byte[] headerBuffer = new byte[16];

        Debug.WriteLine($"[NMS-CORE-LOOP] Starting stream read sequence. Total file size: {fileStream.Length} bytes.");

        while (fileStream.Position < fileStream.Length)
        {
            long currentBlockOffset = fileStream.Position;

            // Read the full 16-byte block metadata segment
            int bytesRead = await fileStream.ReadAsync(headerBuffer.AsMemory(0, 16), cancellationToken);
            if (bytesRead < 16)
            {
                Debug.WriteLine($"[NMS-CORE-LOOP] Stream reached trailing bytes space ({bytesRead} bytes left). End of blocks.");
                break;
            }

            uint magic = BitConverter.ToUInt32(headerBuffer, 0);
            int compressedSize = BitConverter.ToInt32(headerBuffer, 4);
            long decompressedSize = BitConverter.ToInt64(headerBuffer, 8); // FIXED: Read as 8-byte Int64

            Debug.WriteLine($"[NMS-CORE-LOOP] Parsing block at offset {currentBlockOffset}:");
            Debug.WriteLine($"  -> Compressed Size: {compressedSize} bytes");
            Debug.WriteLine($"  -> Decompressed Size: {decompressedSize} bytes");

            if (compressedSize <= 0 || decompressedSize <= 0)
            {
                Debug.WriteLine("[NMS-CORE-LOOP] Encountered zero or invalid sizing marker. Finalizing assembly stream loop.");
                break;
            }

            // Read the compressed block chunk payload from file stream
            byte[] compressedBuffer = new byte[compressedSize];
            byte[] decompressedBuffer = new byte[decompressedSize];

            await fileStream.ReadExactlyAsync(compressedBuffer, 0, compressedSize, cancellationToken);

            // Decompress the chunk using the LZ4 codec
            int actualDecompressedBytes = LZ4Codec.Decode(
                compressedBuffer, 0, compressedSize,
                decompressedBuffer, 0, (int)decompressedSize
            );

            if (actualDecompressedBytes != decompressedSize)
            {
                throw new InvalidDataException($"Extraction failure: LZ4 payload block parsing mismatch at offset {currentBlockOffset}.");
            }

            // Concat the uncompressed string data chunk directly into the final stream
            await decompressedOutputMemory.WriteAsync(decompressedBuffer.AsMemory(0, (int)decompressedSize), cancellationToken);
        }

        decompressedOutputMemory.Position = 0;
        Debug.WriteLine($"[NMS-CORE-LOOP] Stream extraction complete. Final raw JSON memory map footprint: {decompressedOutputMemory.Length} bytes.");
        return decompressedOutputMemory;
    }

    public static async Task<string> ExtractRawJsonAsync(string filePath)
    {
        using var jsonStream = await DecompressSaveToStreamAsync(filePath);
        using var reader = new StreamReader(jsonStream);
        return await reader.ReadToEndAsync();
    }

}