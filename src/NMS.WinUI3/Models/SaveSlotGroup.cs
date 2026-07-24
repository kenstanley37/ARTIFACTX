using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace NMS.WinUI3.Models;

public sealed partial class SaveSlotGroup : ObservableObject
{
    public required int SlotId { get; init; }
    public required IReadOnlyList<SaveFileEntry> Files { get; init; }
    public required string SourceDisplayName { get; init; }

    [ObservableProperty]
    private bool isActive;

    public string SlotLabel => $"Slot {SlotId}";
    public string ActiveLabel => $"{SourceDisplayName} - {SlotLabel}";
}