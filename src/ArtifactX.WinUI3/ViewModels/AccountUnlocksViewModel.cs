using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The account-wide unlock list (NmsAccountData.UnlockedItemsPath) as Catalog,
/// Expedition Rewards and Twitch Rewards edit it - one copy of the raw-id
/// bookkeeping those three pages each used to carry. The list stores ids with
/// a leading "^"; matching goes through CatalogService.NormalizeId. Every
/// change stages the whole array once (AccountSessionManager.StageEdit).
/// </summary>
public sealed class AccountUnlocksViewModel
{
    private List<string> _ids = new();
    private HashSet<string> _set = new(StringComparer.Ordinal);

    /// <summary>Reads the staged/loaded list; call after AccountSessionManager.LoadAsync.</summary>
    public void Load()
    {
        var array = AccountSessionManager.GetValue(NmsAccountData.UnlockedItemsPath) as JArray;
        _ids = array?.Select(t => t.Value<string>() ?? "").Where(s => s.Length > 0).ToList() ?? new();
        _set = new HashSet<string>(_ids.Select(CatalogService.NormalizeId), StringComparer.Ordinal);
    }

    /// <summary>Read-only, so it's safe to call from a background row build.</summary>
    public bool IsUnlocked(string gameId) => _set.Contains(gameId);

    /// <summary>Unlocks or locks the rows and stages the list - only when one
    /// actually changed, so a no-op never shows as an unsaved change.</summary>
    public void Set(IEnumerable<AccountItemRowViewModel> rows, bool unlock)
    {
        bool changed = false;
        foreach (var row in rows)
        {
            row.IsUnlocked = unlock;
            if (unlock && _set.Add(row.GameId))
            {
                _ids.Add("^" + row.GameId);
                changed = true;
            }
            else if (!unlock && _set.Remove(row.GameId))
            {
                _ids.RemoveAll(id => CatalogService.NormalizeId(id) == row.GameId);
                changed = true;
            }
        }
        if (changed) AccountSessionManager.StageEdit(new JArray(_ids), NmsAccountData.UnlockedItemsPath);
    }
}
