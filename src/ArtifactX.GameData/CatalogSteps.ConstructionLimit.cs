using System.Text.Json;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// The game's cap on building parts ("Cannot Build / Construction Limit Reached", #21), read
    /// from the player's own NMS.exe (format 73) - it is in no data file. Found 2026-11-05 in build
    /// 184271: the only instruction in the executable comparing with 21,000 is at 0x1403C3BC8,
    /// in the routine that adds up the loaded bases' part counts ([base+0x310], skipping bases
    /// whose type bit is in 0x1D042) and, at or over the cap, sets the build state
    /// ([+0x368]) to 15:
    ///   41 81 F9 &lt;cap&gt;      cmp  r9d, cap
    ///   7C xx               jl   ...
    ///   83 BF xxxxxxxx 0F   cmp  dword [rdi+state], 15
    ///   7E xx               jle  ...
    ///   C7 87 xxxxxxxx 0F000000  mov dword [rdi+state], 15
    /// The 0x1D042 type mask is not GcPersistentBaseTypes: read that way it would skip the freighter,
    /// ship and claimed-station bases, but the owner's limit tests show all three count (2026-10-21:
    /// 738 station built-ins, 8 parts of room; Slot 6 backup 2026-10-18_14-03-09-after-limit-test:
    /// refused at 21,010 with 349 freighter and 118 ship-base parts). NmsBuildCounts counts them.
    /// The whole sequence must occur exactly once, or nothing is written and the app keeps its
    /// measured 21,000 (NmsBuildCounts.DefaultGlobalLimit, owner's in-game tests 2026-10-18/21).
    /// </summary>
    public static void AddConstructionLimit(string catalogPath, string pcbanksPath)
    {
        // PCBANKS is GAMEDATA\PCBANKS; the exe is in Binaries next to GAMEDATA.
        string? root = Directory.GetParent(pcbanksPath)?.Parent?.FullName;
        string exe = root is null ? "" : Path.Combine(root, "Binaries", "NMS.exe");
        var rows = new List<CatalogItem>();
        if (File.Exists(exe) && FindConstructionLimit(File.ReadAllBytes(exe)) is int cap)
        {
            rows.Add(new CatalogItem
            {
                GameId = "CONSTRUCTIONLIMIT",
                NameEnglish = "Construction limit",
                DescriptionEnglish = JsonSerializer.Serialize(new { GlobalPartLimit = cap }),
            });
            ConsoleStyle.Success($"Construction limit: {cap:N0} parts (from NMS.exe).");
        }
        else ConsoleStyle.Warning("Construction limit not found in NMS.exe - the app keeps its measured value.");
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "GameConstants", "NmsExeConstant", "binaries/nms.exe", rows);
    }

    /// <summary>The cap from the one matching instruction sequence, or null (none, several, or out of range).</summary>
    public static int? FindConstructionLimit(byte[] exe)
    {
        int? found = null;
        for (int i = 0; i + 28 < exe.Length; i++)
        {
            if (exe[i] != 0x41 || exe[i + 1] != 0x81 || exe[i + 2] != 0xF9) continue;
            if (exe[i + 7] != 0x7C || exe[i + 9] != 0x83 || exe[i + 10] != 0xBF || exe[i + 15] != 0x0F || exe[i + 16] != 0x7E) continue;
            if (exe[i + 18] != 0xC7 || exe[i + 19] != 0x87 || BitConverter.ToInt32(exe, i + 24) != 15) continue;
            // The same state field in both: cmp [rdi+X], 15 ... mov [rdi+X], 15.
            if (BitConverter.ToInt32(exe, i + 11) != BitConverter.ToInt32(exe, i + 20)) continue;
            if (found is not null) return null;
            found = BitConverter.ToInt32(exe, i + 3);
        }
        return found is >= 1000 and <= 1_000_000 ? found : null;
    }
}
