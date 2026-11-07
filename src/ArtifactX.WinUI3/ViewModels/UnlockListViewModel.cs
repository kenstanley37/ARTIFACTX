using ArtifactX.WinUI3.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// One unlock list - the account's (Expedition Rewards, Twitch Rewards) or one of the
/// save's (the Catalog page's known technology and blueprints) - with one copy of the
/// raw-id bookkeeping. Lists store ids with a leading "^"; matching goes through
/// CatalogService.NormalizeId. Every change stages the whole array once (IUnlockListStore).
/// </summary>
public sealed class UnlockListViewModel
{
    private readonly IUnlockListStore _store;
    private List<string> _ids = new();
    private HashSet<string> _set = new(StringComparer.Ordinal);

    public UnlockListViewModel(IUnlockListStore store) => _store = store;

    /// <summary>Reads the staged/loaded list; call once its session has loaded.</summary>
    public void Load()
    {
        var array = _store.Read();
        _ids = array?.Select(t => t.Value<string>() ?? "").Where(s => s.Length > 0).ToList() ?? new();
        _set = new HashSet<string>(_ids.Select(CatalogService.NormalizeId), StringComparer.Ordinal);
    }

    /// <summary>Read-only, so it's safe to call from a background row build.</summary>
    public bool IsUnlocked(string gameId) => _set.Contains(gameId);

    /// <summary>True when any of the ids is in the list (a Recipes tile is known when
    /// any recipe making it is).</summary>
    public bool AnyUnlocked(IEnumerable<string> gameIds) => gameIds.Any(_set.Contains);

    /// <summary>Unlocks or locks the rows and stages the list - only when one
    /// actually changed, so a no-op never shows as an unsaved change.</summary>
    public void Set(IEnumerable<IUnlockRow> rows, bool unlock)
    {
        bool changed = false;
        foreach (var row in rows)
        {
            row.IsUnlocked = unlock;
            foreach (string id in row.ListIds)
            {
                if (unlock && _set.Add(id))
                {
                    _ids.Add("^" + id);
                    changed = true;
                }
                else if (!unlock && _set.Remove(id))
                {
                    _ids.RemoveAll(raw => CatalogService.NormalizeId(raw) == id);
                    changed = true;
                }
            }
        }
        if (changed) _store.Stage(new JArray(_ids));
    }
}
