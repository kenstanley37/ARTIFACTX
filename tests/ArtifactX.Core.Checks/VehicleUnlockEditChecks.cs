using ArtifactX.Core.Unlocks;
using Newtonsoft.Json.Linq;

internal static class VehicleUnlockEditChecks
{
    public static void Check()
    {
        static void Require(bool ok) { if (!ok) throw new Exception("Vehicle unlock edit check failed."); }
        var ids = new JArray("^OTHER", "^PART", "PART");
        Require(RewardUnlockEdits.Specials(ids, "PART", false).Count == 1 && ids.Count == 3);
        Require(RewardUnlockEdits.Specials(ids, "PART", true).Count == 3);
        var rule = new TitleUnlockRule("TITLE", "Title", "Requirement", "", "COUNTER", 100, 0,
            "", "", "", "None", 0, "", false, "", [], ["PART"]);
        var original = JArray.Parse("[{'b2n':'^COUNTER','>MX':{}},{'b2n':'^OTHER','>MX':{'>vs':500}}]");
        var raised = RewardUnlockEdits.RaiseRequirements(original, [rule, rule with { StatValue = 200 }]);
        Require(raised[0][">MX"]![">vs"]!.Value<int>() == 200 && original[0][">MX"]![">vs"] is null);
        Require(raised[1][">MX"]![">vs"]!.Value<int>() == 500);
        Require(RewardUnlockEdits.RaiseRequirements(raised, [rule])[0][">MX"]![">vs"]!.Value<int>() == 200);
        var later = (JArray)raised.DeepClone();
        later[1][">MX"]![">vs"] = 700;
        var restored = RewardUnlockEdits.RestoreEntries(later, [new("COUNTER", original[0], raised[0])], "b2n");
        Require(restored[0][">MX"]![">vs"] is null && restored[1][">MX"]![">vs"]!.Value<int>() == 700);
        later[0][">MX"]![">vs"] = 300;
        Require(RewardUnlockEdits.RestoreEntries(later, [new("COUNTER", original[0], raised[0])], "b2n")[0][">MX"]![">vs"]!.Value<int>() == 300);
        foreach (var bad in new[] { rule with { MissionId = "UNKNOWN" }, rule with { StatValue = 0.5f } })
        {
            bool rejected = false;
            try { RewardUnlockEdits.RaiseRequirements(original, [bad]); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected);
        }
        bool floatRejected = false;
        try { RewardUnlockEdits.RaiseRequirements(JArray.Parse("[{'b2n':'^COUNTER','>MX':{'eoL':1.5}}]"), [rule]); }
        catch (InvalidOperationException) { floatRejected = true; }
        Require(floatRejected);
        const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Data><Property name=\"AudioVolume\" value=\"72\"/><Property name=\"UnlockedSpecials\"><Property name=\"UnlockedSpecials\" value=\"OTHER\" _index=\"0\"/><Property name=\"UnlockedSpecials\" value=\"PART\" _index=\"1\"/><Property name=\"UnlockedSpecials\" value=\"PART\" _index=\"2\"/></Property><Property name=\"UnlockedTitles\"><Property name=\"UnlockedTitles\" value=\"TITLE\"/></Property></Data>";
        var removedXml = UserSettingsSpecialEdits.Set(xml, "PART", false);
        Require(!UserSettingsSpecialEdits.Read(removedXml).Contains("PART"));
        Require(UserSettingsSpecialEdits.Read(removedXml).SetEquals(["OTHER"]));
        var beforeXml = System.Xml.Linq.XDocument.Parse(xml);
        var afterXml = System.Xml.Linq.XDocument.Parse(removedXml);
        foreach (string name in new[] { "AudioVolume", "UnlockedTitles" })
            Require(System.Xml.Linq.XNode.DeepEquals(beforeXml.Root!.Elements().Single(e => (string?)e.Attribute("name") == name),
                afterXml.Root!.Elements().Single(e => (string?)e.Attribute("name") == name)));
        var enabledXml = UserSettingsSpecialEdits.Set(removedXml, "PART", true);
        Require(UserSettingsSpecialEdits.Read(enabledXml).SetEquals(["OTHER", "PART"]));
        Require(UserSettingsSpecialEdits.Set(enabledXml, "PART", true) == enabledXml);
        Require(System.Xml.Linq.XDocument.Parse(enabledXml).Declaration?.Encoding == "utf-8");
        Require(System.Xml.Linq.XDocument.Parse(enabledXml).Descendants("Property")
            .Where(e => e.Attribute("_index") is not null).Select(e => (int)e.Attribute("_index")!).SequenceEqual([0, 1]));
        bool malformedRejected = false;
        try { UserSettingsSpecialEdits.Set("<Data/>", "PART", false); }
        catch (System.IO.InvalidDataException) { malformedRejected = true; }
        Require(malformedRejected);
        Console.WriteLine("Vehicle unlock edits: detached changes, minimum counters, guarded prerequisites and selective reset passed.");
        Console.WriteLine("Settings mirror: selected reward removal/addition, idempotence, indices, UTF-8 and unrelated settings/title preservation passed.");
    }
}
