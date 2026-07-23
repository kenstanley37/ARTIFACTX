using Microsoft.EntityFrameworkCore;
using NMS.Data.Models;
using NMS.Data.Services.Parsing;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace NMS.Data.Services;

public class SaveIngestionService
{
    private static long ExtractUnsignedCurrency(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out int signedRawValue)) return (long)(uint)signedRawValue;
            if (element.TryGetInt64(out long longVal)) return longVal;
            return (long)Math.Round(element.GetDouble());
        }
        return 0;
    }

    public static async Task IngestSaveStreamAsync(Stream uncompressedJsonStream, string originalPath)
    {
        // 1. Wipe database staging layer immediately prior to ingestion processing
        using (var dbClean = new NmsDbContext())
        {
            var oldSessions = await dbClean.SaveSessions.Include(s => s.InventorySlots).ToListAsync();
            if (oldSessions.Count > 0)
            {
                dbClean.RemoveRange(oldSessions);
                await dbClean.SaveChangesAsync();
            }
        }

        using var ms = new MemoryStream();
        await uncompressedJsonStream.CopyToAsync(ms);
        byte[] rawJsonBytes = ms.ToArray();

        int validLength = rawJsonBytes.Length;
        while (validLength > 0 && (rawJsonBytes[validLength - 1] == 0x00 || rawJsonBytes[validLength - 1] <= 0x20))
        {
            validLength--;
        }

        var playerState = new PlayerState();
        var occupiedSlots = new List<InventorySlot>();
        var unlockedCoordinates = new HashSet<(int X, int Y)>();

        try
        {
            string fullJson = Encoding.UTF8.GetString(rawJsonBytes.AsSpan(0, validLength));
            using var doc = JsonDocument.Parse(fullJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("8>q", out JsonElement versionToken) && versionToken.ValueKind == JsonValueKind.String)
            {
                playerState.GameVersionToken = versionToken.GetString() ?? "Unknown";
            }

            // Extract Primary Currency Block via verified ancestry: ROOT -> vLc -> 6f=
            if (root.TryGetProperty("vLc", out JsonElement vLcElement) &&
                vLcElement.TryGetProperty("6f=", out JsonElement currencyContainer))
            {
                if (currencyContainer.TryGetProperty("wGS", out JsonElement unitsToken))
                    playerState.Units = ExtractUnsignedCurrency(unitsToken);

                if (currencyContainer.TryGetProperty("7QL", out JsonElement nanitesToken))
                    playerState.Nanites = ExtractUnsignedCurrency(nanitesToken);

                if (currencyContainer.TryGetProperty("kN;", out JsonElement quicksilverToken))
                    playerState.Quicksilver = ExtractUnsignedCurrency(quicksilverToken);
            }

            // Extract Inventory Data via standard <h0> blocks
            if (root.TryGetProperty("<h0", out JsonElement h0))
            {
                foreach (JsonProperty prop in h0.EnumerateObject())
                {
                    string key = prop.Name;
                    JsonElement value = prop.Value;

                    if ((key == "A1f" || key == ":No") && value.ValueKind == JsonValueKind.Array)
                    {
                        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(value.GetRawText()));
                        var parsedList = InventoryParser.ParseInventoryArray(ref reader, key);

                        bool isFreighterBlock = parsedList.Any(slot =>
                            slot.ItemId != null && slot.ItemId.Contains("FREIGHT", StringComparison.OrdinalIgnoreCase));

                        if (!isFreighterBlock && parsedList.Count > occupiedSlots.Count)
                            occupiedSlots = parsedList;
                    }

                    if (key == "Fv4" && value.ValueKind == JsonValueKind.Array)
                    {
                        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(value.GetRawText()));
                        var coords = InventoryParser.ParseUnlockedCoordinates(ref reader);

                        if (coords.Count > unlockedCoordinates.Count)
                            unlockedCoordinates = coords;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Ingestion error: {ex.Message}");
        }

        // Generate Exosuit Layout Grid
        var gridMatrixCanvas = new Dictionary<(int X, int Y), InventorySlot>();
        for (int row = 0; row < 12; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                bool unlocked = unlockedCoordinates.Contains((col, row));
                string slotGroupType = (row <= 1) ? "Technology" : "Cargo";

                gridMatrixCanvas[(col, row)] = new InventorySlot
                {
                    ContainerType = "Exosuit",
                    ContainerId = "MainCargo",
                    XIndex = col,
                    YIndex = row,
                    Amount = 0,
                    MaxAmount = 0,
                    ItemId = unlocked ? "Empty Slot" : "Locked Slot",
                    SlotType = slotGroupType
                };
            }
        }

        foreach (var item in occupiedSlots)
        {
            var coord = (item.XIndex, item.YIndex);
            if (gridMatrixCanvas.ContainsKey(coord)) gridMatrixCanvas[coord] = item;
        }

        using var db = new NmsDbContext();
        var session = new SaveSession
        {
            OriginalFilePath = originalPath,
            LastBackupTime = DateTime.Now,
            GameVersionToken = playerState.GameVersionToken,
            PlayerState = playerState
        };

        foreach (var slot in gridMatrixCanvas.Values) session.InventorySlots.Add(slot);
        db.SaveSessions.Add(session);
        await db.SaveChangesAsync();
    }
}