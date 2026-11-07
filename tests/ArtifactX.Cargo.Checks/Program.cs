using ArtifactX.Core;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

JObject Container(string name, int amount) => new()
{
    ["WA4"] = new JObject { ["rri"] = "Chest" }, ["NKm"] = name,
    ["hl?"] = new JArray(new JObject { [">Qh"] = 0, ["XJ>"] = 0 }),
    [":No"] = new JArray(new JObject
    {
        ["b2n"] = "^TEST", ["Vn8"] = new JObject { ["elv"] = "Product" },
        ["1o9"] = amount, ["F9q"] = 100, ["3ZH"] = new JObject { [">Qh"] = 0, ["XJ>"] = 0 }
    })
};

string temporary = Path.Combine(Path.GetTempPath(), $"artifactx-cargo-{Guid.NewGuid():N}.json");
try
{
    JObject Stack(int x, int amount, int max = 100, string category = "Product", string id = "^TEST") => new()
    {
        ["b2n"] = id, ["Vn8"] = new JObject { ["elv"] = category },
        ["1o9"] = amount, ["F9q"] = max, ["3ZH"] = new JObject { [">Qh"] = x, ["XJ>"] = 0 },
        ["b76"] = true, ["eVk"] = 0, ["extra"] = "preserve"
    };
    var stacks = new JArray(Stack(0, 80), Stack(1, 80), Stack(2, 20));
    var original = stacks.DeepClone();
    var merged = ArtifactX.Core.Inventory.InventoryStackMerger.Merge(stacks, out int freed)!;
    Check(freed == 1 && merged.Count == 2, "Partial stacks free only the slots they no longer need");
    Check(merged[0]["1o9"]!.Value<int>() == 100 && merged[1]["1o9"]!.Value<int>() == 80,
        "Overflow remains in an additional stack without losing quantities");
    Check(JToken.DeepEquals(stacks, original) && merged.All(x => x["extra"]!.Value<string>() == "preserve"),
        "Merging preserves source data and unknown item metadata");
    Check(ArtifactX.Core.Inventory.InventoryStackMerger.Merge(merged, out _) is null, "An already packed inventory is unchanged");
    var noFreeSlot = ArtifactX.Core.Inventory.InventoryStackMerger.Merge(new JArray(Stack(0, 60), Stack(1, 60)), out freed);
    Check(noFreeSlot is not null && freed == 0 && noFreeSlot[0]["1o9"]!.Value<int>() == 100,
        "Repacking reports a change even when no whole slot is freed");
    var different = Stack(1, 20); different["extra"] = "different";
    Check(ArtifactX.Core.Inventory.InventoryStackMerger.Merge(new JArray(Stack(0, 20), different), out _) is null,
        "Different saved properties prevent merging");
    Check(ArtifactX.Core.Inventory.InventoryStackMerger.Merge(new JArray(Stack(0, 1, 1), Stack(1, 1, 1)), out _) is null,
        "Non-stackable items are not deleted as duplicates");
    Check(ArtifactX.Core.Inventory.InventoryStackMerger.Merge(new JArray(Stack(0, 20, 100, "Technology"), Stack(1, 20, 100, "Technology")), out _) is null,
        "Technology is excluded from cargo merging");
    Check(ArtifactX.Core.Inventory.InventoryStackMerger.Merge(new JArray(Stack(0, 20), Stack(1, 20, 200)), out _) is null,
        "Different maximum stack sizes stay separate");
    var player = new JObject();
    for (int i = 0; i < 13; i++) player[$"container{i}"] = Container($"Container {i:D2}", i + 1);
    var root = new JObject { ["vLc"] = new JObject { ["6f="] = player } };
    await File.WriteAllTextAsync(temporary, root.ToString());
    SaveSessionManager.Session = await SaveEditSession.LoadAsync(temporary);
    var vm = new BaseStorageViewModel();
    vm.RefreshContainers();
    Check(vm.Containers.Count == 10, "Only ten reachable containers are shown");
    await vm.LoadSelectedAsync();
    var first = vm.SelectedContainer!;
    Check(first.IsSelected && vm.Containers.Count(c => c.IsSelected) == 1, "Container buttons mark exactly the selected container");
    var firstEditor = vm.Editor!;
    var firstCell = firstEditor.Inventory.Cells[0];
    Check(firstCell.Amount == 1, "Production inventory model reads the first container");
    Check(!await firstEditor.RefreshAsync(), "Unchanged refresh is suppressed");
    Check(ReferenceEquals(firstCell, firstEditor.Inventory.Cells[0]), "Unchanged refresh keeps cell identity");

    SaveSessionManager.Session!.Root["vLc"]!["6f="]![first.Key]!["NKm"] = "Game renamed container";
    vm.RefreshContainers();
    await vm.LoadSelectedAsync();
    Check(vm.Name == "Game renamed container", "A changed saved name updates an untouched name field");
    SaveSessionManager.Session.Root["vLc"]!["6f="]![first.Key]!["NKm"] = first.Name;
    vm.RefreshContainers();
    await vm.LoadSelectedAsync();

    vm.Name = "Unfinished draft";
    SaveSessionManager.StageEdit("unrelated", "other");
    vm.RefreshContainers();
    Check(!await vm.LoadSelectedAsync() && vm.Name == "Unfinished draft", "Unrelated edits retain draft name and grid");

    firstEditor.Inventory.StageAmount(0, 0, 42);
    Check(vm.HasChanges, "Inventory edits are staged");
    var previouslySelected = vm.SelectedContainer!;
    vm.SelectedContainer = vm.Containers[1];
    Check(vm.SelectedContainer.IsSelected && !previouslySelected.IsSelected, "Container button selection moves without rebuilding the selector");
    await vm.LoadSelectedAsync();
    vm.SelectedContainer = vm.Containers.First(c => c.Key == first.Key);
    await vm.LoadSelectedAsync();
    Check(ReferenceEquals(firstEditor, vm.Editor), "Returning to a container keeps its editor");
    Check(vm.Editor!.Inventory.Cells[0].Amount == 42, "Pending quantity survives container switches");
    vm.Name = "Renamed container";
    vm.Rename();
    Check(vm.SelectedContainer!.Name == "Renamed container", "Staged rename updates selector by stable key");
    Check(SaveSessionManager.Session.Root["vLc"]!["6f="]![first.Key]!["NKm"]!.Value<string>() == first.Name, "Renames do not mutate loaded save JSON");
    vm.Revert();
    await vm.LoadSelectedAsync();
    Check(vm.Name == first.Name && vm.Editor!.Inventory.Cells[0].Amount == 1 && !vm.HasChanges, "Base reset includes names and inventory");
    Check(SaveSessionManager.GetString("other") == "unrelated", "Reset preserves unrelated staged edits");

    var mergePath = NmsInventoryContainer.BaseStorageContainerPath(first.Key).Append(":No").ToArray();
    SaveSessionManager.StageEdit(stacks, mergePath);
    await firstEditor.RefreshAsync();
    Check(firstEditor.Inventory.TryMergeDuplicates(out freed) && freed == 1, "Inventory action stages the merged whole array");
    Check(((JArray)SaveSessionManager.GetValue(mergePath)!).Sum(x => x["1o9"]!.Value<int>()) == 180,
        "Staged merge preserves the combined quantity");
    firstEditor.Revert();
    await firstEditor.RefreshAsync();
    Check(firstEditor.Inventory.Cells[0].Amount == 1, "Reset restores the pre-merge inventory");

    CatalogService.WarmGate = new();
    SaveSessionManager.Session.Root["vLc"]!["6f="]![first.Key]![":No"]![0]!["1o9"] = 77;
    bool inventoryReady = false;
    Task<bool> pending = firstEditor.RefreshAsync(() => inventoryReady = firstEditor.Inventory.Cells[0].Amount == 77);
    Check(inventoryReady && !pending.IsCompleted, "Inventory is ready to draw while catalog loading is still blocked");
    SaveSessionManager.Session = await SaveEditSession.LoadAsync(temporary);
    CatalogService.WarmGate.SetResult();
    Check(!await pending, "Old async refresh is rejected after changing save");
    CatalogService.WarmGate = null;
    vm.RefreshContainers();
    await vm.LoadSelectedAsync();
    Check(!ReferenceEquals(firstEditor, vm.Editor), "Changing save discards old container editors");
    Check(vm.Editor!.Inventory.Cells[0].Amount == 1, "New save loads its own values");
    var owner = new Microsoft.UI.Xaml.FrameworkElement();
    int refreshes = 0;
    var path = NmsInventoryContainer.BaseStorageContainerPath(first.Key);
    _ = new CargoStorageFeed(owner, () => [path], () => refreshes++);
    Check(refreshes == 0, "Unopened storage feed performs no work");
    owner.RaiseLoaded();
    owner.RaiseLoaded();
    Check(refreshes == 1, "Repeated Loaded does not duplicate subscriptions");
    SaveSessionManager.NotifyEdits();
    owner.DispatcherQueue.Drain();
    Check(refreshes == 2, "Pending edits trigger one refresh");
    var disk = (JObject)SaveSessionManager.Session!.Root.DeepClone();
    disk["vLc"]!["6f="]![first.Key]![":No"]![0]!["1o9"] = 91;
    SaveSessionManager.NotifyGame(disk);
    owner.DispatcherQueue.Drain();
    Check(SaveSessionManager.GetValue(path)![":No"]![0]!["1o9"]!.Value<int>() == 91, "App-wide feed adopts current storage snapshot");
    SaveSessionManager.StageEdit("keep my edit", path.Append("NKm").ToArray());
    disk["vLc"]!["6f="]![first.Key]![":No"]![0]!["1o9"] = 92;
    SaveSessionManager.NotifyGame(disk);
    owner.DispatcherQueue.Drain();
    Check(SaveSessionManager.GetValue(path)![":No"]![0]!["1o9"]!.Value<int>() == 91, "Game feed preserves containers with pending edits");
    SaveSessionManager.RevertEditsUnder(path);
    int before = refreshes;
    SaveSessionManager.NotifyGame(disk);
    SaveSessionManager.Session = await SaveEditSession.LoadAsync(temporary);
    owner.DispatcherQueue.Drain();
    Check(refreshes == before, "Queued game snapshot is ignored after switching save");
    disk["expedition"] = true;
    SaveSessionManager.NotifyGame(disk);
    owner.DispatcherQueue.Drain();
    Check(refreshes == before, "Feed rejects a mismatching expedition context");
    owner.RaiseUnloaded();
    SaveSessionManager.NotifySession();
    SaveSessionManager.NotifyEdits();
    owner.DispatcherQueue.Drain();
    Check(refreshes == before, "Unloaded controls detach from static events");
    owner.RaiseLoaded();
    before = refreshes;
    SaveSessionManager.NotifyEdits();
    owner.DispatcherQueue.Drain();
    Check(refreshes == before + 1, "Revisited controls reattach exactly once");
    owner.RaiseUnloaded();
    SaveSessionManager.Session = null;
    vm.RefreshContainers();
    await vm.LoadSelectedAsync();
    Check(vm.Editor is null && vm.Containers.Count == 0, "Unloading a save clears base storage");
    Console.WriteLine($"PASS: {checks} Cargo checks (production storage view models and inventory, synthetic saves only).");
}
finally { File.Delete(temporary); }
