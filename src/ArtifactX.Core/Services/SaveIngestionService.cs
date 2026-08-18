using ArtifactX.Core.NmsModels;
using System.Diagnostics;
using System.Text.Json;

namespace ArtifactX.Core.Services;

public class SaveIngestionService
{
    public async Task<NmsSaveFile?> ProcessSaveFileAsync(string filePath)
    {
        try
        {
            // 1. Load the raw bytes (Assuming you have your decompression layer ready)
            // byte[] decryptedBytes = await DecryptAndDecompressAsync(filePath);

            // 2. Use JsonDocument for a read-only, non-crashing pass
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(filePath));
            var root = doc.RootElement;

            // 3. Navigate the Ancestry Anchors
            // Path: root -> "h0" (PlayerState)
            if (root.TryGetProperty("h0", out JsonElement playerElement))
            {
                Debug.WriteLine("[ArtifactX-CORE] Found Anchor: h0 (PlayerState)");

                // 4. Map only the specific branch we want
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<NmsSaveFile>(playerElement.GetRawText(), options);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArtifactX-CORE-ERROR] Failed to ingest save: {ex.Message}");
        }

        return null;
    }
}