using ArtifactX.Core;
using System.Text;
using System.Text.Json;

namespace ArtifactX.Tools.SaveInspector.Services;

/// <summary>
/// Read-only format investigation: reports a file's raw byte layout, checks it
/// against the known save-container magic number, and attempts the standard
/// block-decompress pipeline to see whether the result is valid JSON. Never
/// modifies the source file — at most writes a sibling *.json when --extract
/// is passed and decompression succeeds.
/// </summary>
public static class FileInspectionService
{
    public static async Task InspectAsync(string path, bool dumpFull, bool extract)
    {
        if (!File.Exists(path))
        {
            ConsoleStyle.Error($"File not found: {path}");
            return;
        }

        var fi = new FileInfo(path);
        ConsoleStyle.Header($"Inspecting: {fi.FullName}");
        ConsoleStyle.Info($"Size: {fi.Length:N0} bytes");
        LogService.Write(string.Empty);

        byte[] rawBytes = await File.ReadAllBytesAsync(path);

        if (rawBytes.Length >= 4)
        {
            uint magic = BitConverter.ToUInt32(rawBytes, 0);
            bool matchesSaveMagic = magic == SaveStreamProcessor.ExpectedMagic;

            ConsoleStyle.Info($"First 4 bytes as uint32: 0x{magic:X8} " +
                (matchesSaveMagic ? "(matches standard save container magic)" : "(does NOT match standard save container magic)"));
            LogService.Write(string.Empty);
        }

        int dumpLength = dumpFull ? rawBytes.Length : Math.Min(rawBytes.Length, 256);
        ConsoleStyle.Header($"Raw bytes (first {dumpLength} of {rawBytes.Length}):");
        PrintHexDump(rawBytes, dumpLength);
        LogService.Write(string.Empty);

        ConsoleStyle.Header("Attempting standard save-container decompression...");
        try
        {
            using Stream decompressed = await SaveStreamProcessor.DecompressSaveToStreamAsync(path);
            using var ms = new MemoryStream();
            await decompressed.CopyToAsync(ms);
            byte[] jsonBytes = ms.ToArray();

            ConsoleStyle.Success($"Decompressed successfully: {jsonBytes.Length:N0} bytes.");

            string text = Encoding.UTF8.GetString(jsonBytes).TrimEnd('\0', ' ', '\r', '\n');

            if (TryParseJson(text, out string jsonError))
            {
                ConsoleStyle.Success("Decompressed content is valid JSON.");

                if (extract)
                {
                    string outPath = path + ".json";
                    await File.WriteAllTextAsync(outPath, text);
                    ConsoleStyle.Success($"Wrote decompressed JSON to: {outPath}");
                }
            }
            else
            {
                ConsoleStyle.Warning($"Decompressed content is NOT valid JSON: {jsonError}");
                ConsoleStyle.Info("Preview of decompressed bytes as text:");
                LogService.Write(text.Length > 200 ? text[..200] + "..." : text);
            }
        }
        catch (Exception ex)
        {
            ConsoleStyle.Warning($"Standard decompression failed: {ex.Message}");
            ConsoleStyle.Info("This file likely uses a different container format than save*.hg files.");
        }
    }

    private static bool TryParseJson(string text, out string error)
    {
        try
        {
            using var _ = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            error = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void PrintHexDump(byte[] bytes, int length)
    {
        const int bytesPerLine = 16;

        for (int offset = 0; offset < length; offset += bytesPerLine)
        {
            int lineLength = Math.Min(bytesPerLine, length - offset);
            var hexPart = new StringBuilder();
            var asciiPart = new StringBuilder();

            for (int i = 0; i < bytesPerLine; i++)
            {
                if (i < lineLength)
                {
                    byte b = bytes[offset + i];
                    hexPart.Append(b.ToString("X2")).Append(' ');
                    asciiPart.Append(b is >= 32 and < 127 ? (char)b : '.');
                }
                else
                {
                    hexPart.Append("   ");
                }

                if (i == 7) hexPart.Append(' ');
            }

            LogService.Write($"{offset:X8}  {hexPart}  {asciiPart}");
        }
    }
}