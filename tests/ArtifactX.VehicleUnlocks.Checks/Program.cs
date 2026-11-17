using ArtifactX.Core.Unlocks;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Newtonsoft.Json.Linq;

// Runs the real editor coordinator with in-memory service adapters. No game,
// account, settings or app files are opened; file commit behavior is outside scope.
static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
static TitleUnlockRule Rule(string title, float target) => new(title, title, "", "", "COUNTER", target, 0,
    "", "", "", "None", 0, "", false, "", [], []);
SaveSessionManager.Root = JObject.Parse("{'vLc':{'6f=':{'24<':['^SAVED'],'gUR':[{':rc':'^GLOBAL_STATS','gUR':[{'b2n':'^COUNTER','>MX':{'>vs':10}},{'b2n':'^OTHER','>MX':{'>vs':2}}]}]}}}");
var editor = RewardUnlockEditor.Current;
await editor.LoadAsync();
long Counter() => SaveSessionManager.GetValue(NmsPlayerStatsPaths.GroupStatsArrayPath(0))![0]![">MX"]![">vs"]!.Value<long>();
editor.Set("NEW1", [Rule("TITLE1", 100)], true);
editor.Set("NEW2", [Rule("TITLE2", 200)], true);
Require(Counter() == 200 && editor.IsPendingEnable("NEW1"), "Shared prerequisites must use highest pending minimum.");
var other = (JArray)SaveSessionManager.GetValue(NmsPlayerStatsPaths.GroupStatsArrayPath(0))!.DeepClone();
other[1][">MX"]![">vs"] = 9;
SaveSessionManager.StageEdit(other, NmsPlayerStatsPaths.GroupStatsArrayPath(0));
editor.Set("NEW2", [], false);
Require(Counter() == 100 && editor.Account!.Rewards.Contains("NEW1") && !editor.Account.Rewards.Contains("NEW2"), "Undo must retain only remaining pending reward.");
Require(editor.Account!.Titles.SetEquals(["TITLE1"]), "Undo must remove only pending title changes.");
Require(SaveSessionManager.GetValue(NmsPlayerStatsPaths.GroupStatsArrayPath(0))![1]![">MX"]![">vs"]!.Value<int>() == 9, "Undo must preserve unrelated edits.");
editor.Set("NEW1", [], false);
Require(Counter() == 10 && editor.Account!.Titles.Count == 0 && !editor.IsPendingEnable("NEW1"), "Undo last choice must restore original prerequisites.");
bool blocked = false;
try { editor.Set("SAVED", [], false); } catch (InvalidOperationException) { blocked = true; }
Require(blocked, "Previously saved unlock must not be removable.");
editor.Set("NEW3", [Rule("TITLE3", 50)], true);
SaveSessionManager.Commit();
Require(!editor.IsPendingEnable("NEW3"), "Committed unlock must cease being undoable.");
blocked = false;
try { editor.Set("NEW3", [], false); } catch (InvalidOperationException) { blocked = true; }
Require(blocked, "A newly committed unlock must become read-only.");
editor.Set("NEW4", [], true);
Require(!editor.IsPendingEnable("NEW3"), "Later edits must not make committed choices undoable again.");
Console.WriteLine("PASS: saved unlock guard, pending undo, shared prerequisites, titles, unrelated edits and post-commit locking (in-memory adapters).");
