using NMS.Core; // Ensure this references the project containing SaveStreamProcessor
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NMS.WinUI3.Services;

public static class SaveSessionManager
{
    public static event EventHandler? ActiveSessionChanged;

    // This is the "warehouse" for the decrypted JSON string
    private static string? _rawJsonPayload;

    public static string? GetRawData() => _rawJsonPayload;

    public static bool IsSaveLoaded => !string.IsNullOrEmpty(_rawJsonPayload);

    /// <summary>
    /// Decompresses the .hg file into a raw JSON string for memory storage.
    /// </summary>
    public static async Task LoadActiveSessionContextAsync(string targetFilePath)
    {
        if (!File.Exists(targetFilePath)) return;

        try
        {
            // 1. DECOMPRESS: Use your existing processor to get the raw JSON stream
            using Stream decompressedStream = await SaveStreamProcessor.DecompressSaveToStreamAsync(targetFilePath);

            // 2. READ: Convert the stream directly to a string
            using StreamReader reader = new StreamReader(decompressedStream, Encoding.UTF8);
            _rawJsonPayload = await reader.ReadToEndAsync();

            // 3. NOTIFY: Everything is ready in the "warehouse"
            ActiveSessionChanged?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // Log the error so you know if decryption failed
            System.Diagnostics.Debug.WriteLine($"[SaveSessionManager] Decryption/Read Failed: {ex.Message}");
            throw; // Re-throw to handle in the UI layer
        }
    }

    public static void ClearSession()
    {
        _rawJsonPayload = null;
    }
}