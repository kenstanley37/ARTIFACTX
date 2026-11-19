using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>What the expedition / main game notice should do: reload the save first (nothing was edited) and say so, or
/// ask the player to reload (their edits were made to the character the game left).</summary>
public sealed record ContextChange(string Title, string Message, bool ReloadFirst, bool OfferReload);

/// <summary>
/// The main window's state: which nav items show (save pages need a loaded save; Save Editor and Account Data only a
/// chosen platform), the nav badges, the "No Man's Sky is running" banner, the game-data bar (<see cref="GameData"/>),
/// and the two notices that follow the game - it saving a different slot, or moving the loaded save between an
/// expedition and the main game. The window shows the dialogs, reloads pages and animates.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    public GameDataBarViewModel GameData { get; } = new();

    // ---------------------------------------------------------------- nav

    [ObservableProperty] public partial bool IsSaveLoaded { get; private set; }

    /// <summary>Save Editor and Account Data: Account Data edits accountdata.hg, once per platform-account folder rather
    /// than per slot, so a chosen platform is enough (a loaded slot implies one).</summary>
    [ObservableProperty] public partial bool HasPlatform { get; private set; }

    /// <summary>A reminder on Cargo: how many different items are waiting in the cache (easy to forget, user 2026-10-15) -
    /// kinds of item, not quantities.</summary>
    [ObservableProperty] public partial int CacheItems { get; private set; }
    public bool HasCacheItems => CacheItems > 0;
    public string? CacheTip => CacheItems > 0 ? $"{CacheItems} item{(CacheItems == 1 ? "" : "s")} waiting from a dismantled base" : null;

    /// <summary>"New" counts on Multi-Tool and Starships for what the game added while it played (LiveItemsService),
    /// until each one is clicked.</summary>
    [ObservableProperty] public partial int NewMultiTools { get; private set; }
    [ObservableProperty] public partial int NewShips { get; private set; }
    public bool HasNewMultiTools => NewMultiTools > 0;
    public bool HasNewShips => NewShips > 0;
    public string? NewMultiToolsTip => NewTip(NewMultiTools, "multi-tool");
    public string? NewShipsTip => NewTip(NewShips, "starship");

    private static string? NewTip(int count, string noun) => count > 0 ? $"{count} new {noun}{(count == 1 ? "" : "s")} in your game" : null;

    [ObservableProperty] public partial bool IsGameRunning { get; set; }

    public void RefreshNav()
    {
        IsSaveLoaded = SaveSessionManager.IsSaveLoaded;
        HasPlatform = SaveSessionManager.IsSaveLoaded || SaveSessionManager.HasActivePlatform;
        CacheItems = SaveSessionManager.GetValue(NmsInventoryContainer.BaseItemCachePath)?[":No"] is JArray cached ? cached.Count : 0;
        OnPropertyChanged(nameof(HasCacheItems));
        OnPropertyChanged(nameof(CacheTip));
    }

    public void RefreshLiveBadges()
    {
        NewMultiTools = LiveItemsService.NewCount(LiveCollection.MultiTools);
        NewShips = LiveItemsService.NewCount(LiveCollection.Ships);
        OnPropertyChanged(nameof(HasNewMultiTools));
        OnPropertyChanged(nameof(HasNewShips));
        OnPropertyChanged(nameof(NewMultiToolsTip));
        OnPropertyChanged(nameof(NewShipsTip));
    }

    // ---------------------------------------------------------------- the game saved another slot

    // The slot the game moved to, and one the player said no to (not offered again on every autosave - only once the
    // game moves somewhere else).
    private SaveSlotGroup? _offeredSlot;
    private (string? Folder, int SlotId)? _declinedSlot;

    [ObservableProperty] public partial bool SlotSwitchOpen { get; set; }
    [ObservableProperty] public partial string SlotSwitchMessage { get; private set; } = "";
    [ObservableProperty] public partial string SlotSwitchButtonText { get; private set; } = "";

    public SaveSlotGroup? OfferedSlot => _offeredSlot;

    /// <summary>The game saved a different slot of this account than the one loaded: offer to follow it, rather than
    /// switching on our own - the loaded save may have edits waiting (user request 2026-10-14).</summary>
    public void OfferSlotSwitch(SaveSlotGroup slot)
    {
        string? folder = Path.GetDirectoryName(slot.PrimaryFile.FullPath);
        if (_declinedSlot is { } no && no.SlotId == slot.SlotId && string.Equals(no.Folder, folder, StringComparison.OrdinalIgnoreCase)) return;
        _declinedSlot = null;
        _offeredSlot = slot;
        string loaded = SaveSessionManager.ActiveSlot is { } a ? $"{a.SlotLabel} ({a.PrimaryFile.SaveName})" : "another save";
        SlotSwitchMessage = $"The game is now saving to {slot.SlotLabel} ({slot.PrimaryFile.SaveName}). ArtifactX is showing {loaded}.";
        SlotSwitchButtonText = $"Switch to {slot.SlotLabel}";
        SlotSwitchOpen = true;
    }

    /// <summary>The player closed the offer: not offered again until the game moves somewhere else.</summary>
    public void SlotSwitchDeclined()
    {
        if (_offeredSlot is { } slot) _declinedSlot = (Path.GetDirectoryName(slot.PrimaryFile.FullPath), slot.SlotId);
        _offeredSlot = null;
    }

    /// <summary>Switching: the offer is used up.</summary>
    public void TakeSlotSwitch()
    {
        _offeredSlot = null;
        SlotSwitchOpen = false;
    }

    // ---------------------------------------------------------------- expedition / main game

    // The context the player chose to keep editing despite the game moving away from it - not offered again on every
    // autosave.
    private bool? _declinedContextIsExpedition;

    [ObservableProperty] public partial bool ContextOpen { get; set; }
    [ObservableProperty] public partial string ContextTitle { get; private set; } = "";
    [ObservableProperty] public partial string ContextMessage { get; private set; } = "";
    [ObservableProperty] public partial bool ContextOffersReload { get; private set; }

    /// <summary>The game saved the loaded slot. Ordinary autosaves change nothing on screen (user rule 2026-10-16); only a
    /// move between the expedition and the main game does, since every page would otherwise show a character the game is
    /// no longer playing. Null: nothing to do.</summary>
    public ContextChange? OnGameSavedLoadedSlot(JObject disk)
    {
        bool nowExpedition = SaveSessionManager.IsMidExpedition(disk);
        if (nowExpedition == SaveSessionManager.IsExpeditionActive)
        {
            _declinedContextIsExpedition = null;
            return null;
        }
        if (_declinedContextIsExpedition == SaveSessionManager.IsExpeditionActive) return null;

        string now = nowExpedition ? "You started an expedition" : "You left the expedition";
        if (!SaveSessionManager.HasUnsavedChanges)
            return new ContextChange(now, nowExpedition
                ? "ArtifactX is now showing your expedition character."
                : "ArtifactX is now showing your main game.", ReloadFirst: true, OfferReload: false);

        int count = SaveSessionManager.PendingEditCount;
        string was = SaveSessionManager.IsExpeditionActive ? "the expedition" : "your main game";
        return new ContextChange($"{now} in No Man's Sky",
            $"Your {count} unsaved {(count == 1 ? "change was" : "changes were")} made to {was}, which the game is no longer playing. " +
            "Reload to see what the game is playing now.", ReloadFirst: false, OfferReload: true);
    }

    public void ShowContext(ContextChange change)
    {
        ContextTitle = change.Title;
        ContextMessage = change.Message;
        ContextOffersReload = change.OfferReload;
        ContextOpen = true;
    }

    /// <summary>The player closed a reload offer: keep editing what's loaded.</summary>
    public void ContextClosed()
    {
        if (ContextOffersReload) _declinedContextIsExpedition = SaveSessionManager.IsExpeditionActive;
    }

    /// <summary>The save is being read fresh: any earlier "keep editing" no longer applies.</summary>
    public void ContextReloaded() => _declinedContextIsExpedition = null;

    /// <summary>A save was opened or closed - nothing to offer.</summary>
    public void SessionChanged()
    {
        RefreshNav();
        SlotSwitchOpen = false;
        ContextOpen = false;
    }
}
