using ArtifactX.Core.Unlocks;
using Newtonsoft.Json.Linq;

internal static class VehicleUnlockEditChecks
{
    public static void Check()
    {
        static void Require(bool ok) { if (!ok) throw new Exception("Vehicle unlock edit check failed."); }
        var ids = new JArray("^OTHER", "^PART", "PART");
        Require(VehicleUnlockEdits.Specials(ids, "PART", false).Count == 1 && ids.Count == 3);
        Require(VehicleUnlockEdits.Specials(ids, "PART", true).Count == 3);
        var rule = new TitleUnlockRule("TITLE", "Title", "Requirement", "", "COUNTER", 100, 0,
            "", "", "", "None", 0, "", false, "", [], ["PART"]);
        var original = JArray.Parse("[{'b2n':'^COUNTER','>MX':{}},{'b2n':'^OTHER','>MX':{'>vs':500}}]");
        var raised = VehicleUnlockEdits.RaiseRequirements(original, [rule, rule with { StatValue = 200 }]);
        Require(raised[0][">MX"]![">vs"]!.Value<int>() == 200 && original[0][">MX"]![">vs"] is null);
        Require(raised[1][">MX"]![">vs"]!.Value<int>() == 500);
        Require(VehicleUnlockEdits.RaiseRequirements(raised, [rule])[0][">MX"]![">vs"]!.Value<int>() == 200);
        var later = (JArray)raised.DeepClone();
        later[1][">MX"]![">vs"] = 700;
        var restored = VehicleUnlockEdits.RestoreEntries(later, [new("COUNTER", original[0], raised[0])], "b2n");
        Require(restored[0][">MX"]![">vs"] is null && restored[1][">MX"]![">vs"]!.Value<int>() == 700);
        later[0][">MX"]![">vs"] = 300;
        Require(VehicleUnlockEdits.RestoreEntries(later, [new("COUNTER", original[0], raised[0])], "b2n")[0][">MX"]![">vs"]!.Value<int>() == 300);
        foreach (var bad in new[] { rule with { MissionId = "UNKNOWN" }, rule with { StatValue = 0.5f } })
        {
            bool rejected = false;
            try { VehicleUnlockEdits.RaiseRequirements(original, [bad]); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected);
        }
        bool floatRejected = false;
        try { VehicleUnlockEdits.RaiseRequirements(JArray.Parse("[{'b2n':'^COUNTER','>MX':{'eoL':1.5}}]"), [rule]); }
        catch (InvalidOperationException) { floatRejected = true; }
        Require(floatRejected);
        Console.WriteLine("Vehicle unlock edits: detached changes, minimum counters, guarded prerequisites and selective reset passed.");
    }
}
