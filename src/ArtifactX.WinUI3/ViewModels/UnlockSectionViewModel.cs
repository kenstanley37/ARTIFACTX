using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// One card on an account-wide-unlock page (Catalog / Expedition Rewards /
/// Twitch Rewards; UnlockSectionCard). Its header checkbox unlocks or locks
/// this section only - the pages' old Lock All / Unlock All changed every item
/// on the page at once (owner feedback 2026-11-03). "Locked only" narrows the
/// list to what isn't unlocked yet; rows unlocked meanwhile stay in view until
/// it's toggled, so nothing jumps out from under the pointer.
/// </summary>
public sealed partial class UnlockSectionViewModel : ObservableObject
{
    private readonly AccountUnlocksViewModel _unlocks;
    private readonly ISet<string> _lockedOnlyKeys;

    /// <param name="key">Stable name that keeps "Locked only" on when the page
    /// rebuilds its cards (search, filters).</param>
    /// <param name="listHeight">The card's list height; each page picks its sizing rule.</param>
    /// <param name="lockedOnlyKeys">The page's set of sections showing locked items only.</param>
    public UnlockSectionViewModel(string key, string title, List<AccountItemRowViewModel> items, double listHeight,
        AccountUnlocksViewModel unlocks, ISet<string> lockedOnlyKeys)
    {
        Key = key;
        Title = title;
        Items = items;
        ListHeight = listHeight;
        _unlocks = unlocks;
        _lockedOnlyKeys = lockedOnlyKeys;
        ShowLockedOnly = lockedOnlyKeys.Contains(key);
        Refresh(reapplyFilter: true);
    }

    public string Key { get; }
    public string Title { get; }
    public List<AccountItemRowViewModel> Items { get; }
    public double ListHeight { get; }

    /// <summary>Header in capitals for the themed card band (TextBlock has no text-transform).</summary>
    public string HeaderUpper => $"{Title} ({Items.Count:N0})".ToUpperInvariant();

    [ObservableProperty] public partial IReadOnlyList<AccountItemRowViewModel> VisibleItems { get; private set; } = [];
    [ObservableProperty] public partial bool ShowLockedOnly { get; set; }

    /// <summary>True when every item is unlocked, false when none is, null when mixed.</summary>
    [ObservableProperty] public partial bool? AllUnlocked { get; private set; }
    [ObservableProperty] public partial string CountText { get; private set; } = "";
    [ObservableProperty] public partial bool IsNothingShown { get; private set; }

    public string SelectAllName => $"Unlock or lock every item in {Title}";
    public string LockedOnlyName => $"Show only locked items in {Title}";

    partial void OnShowLockedOnlyChanged(bool value)
    {
        if (value) _lockedOnlyKeys.Add(Key);
        else _lockedOnlyKeys.Remove(Key);
        Refresh(reapplyFilter: true);
    }

    public void SetRow(AccountItemRowViewModel row, bool unlock)
    {
        _unlocks.Set([row], unlock);
        Refresh(reapplyFilter: false);
    }

    /// <summary>The header checkbox: a mixed or locked section unlocks; only a
    /// fully unlocked one locks (the usual "select all" convention, which
    /// WinUI's own toggle would reverse for the mixed state).</summary>
    public void ToggleAll()
    {
        _unlocks.Set(Items, AllUnlocked != true);
        Refresh(reapplyFilter: false);
        OnPropertyChanged(nameof(AllUnlocked)); // the checkbox toggled itself; restore the real state
    }

    private void Refresh(bool reapplyFilter)
    {
        int unlocked = Items.Count(i => i.IsUnlocked);
        AllUnlocked = unlocked == Items.Count ? true : unlocked == 0 ? false : null;
        CountText = $"{unlocked:N0} of {Items.Count:N0} unlocked";
        if (reapplyFilter) VisibleItems = ShowLockedOnly ? Items.Where(i => !i.IsUnlocked).ToList() : Items;
        IsNothingShown = VisibleItems.Count == 0;
    }
}
