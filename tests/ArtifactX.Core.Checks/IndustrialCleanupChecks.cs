using ArtifactX.Core.IndustrialCleanup;
using Newtonsoft.Json.Linq;

internal static class IndustrialCleanupChecks
{
    public static void Check()
    {
        var groups = JArray.Parse("""
        [{":rc":"^OTHER","gUR":[{"b2n":"^CARGO_VALUE",">MX":{">vs":999}}]},
         {":rc":"^GLOBAL_STATS","gUR":[
          {"b2n":"^CARGO_VALUE",">MX":{">vs":15580}},
          {"b2n":"^CARGO_TOX",">MX":{}},
          {"b2n":"^CARGO_RAD",">MX":{"eoL":119.5}},
          {"b2n":"^BAD",">MX":{">vs":"invalid"}}]}]
        """);
        string before = groups.ToString();
        var values = IndustrialCleanupProgress.Read(groups);
        if (values.Count != 3 || values["CARGO_VALUE"] != 15580 || values["CARGO_TOX"] != 0 || values["CARGO_RAD"] != 119.5)
            throw new InvalidOperationException("Cleanup progress did not read the global numeric counters correctly.");
        if (IndustrialCleanupProgress.Read(null).Count != 0 || IndustrialCleanupProgress.Read(new JArray()).Count != 0)
            throw new InvalidOperationException("Missing cleanup progress must remain unavailable.");
        if (before != groups.ToString()) throw new InvalidOperationException("Cleanup progress mutated the save.");
        Console.WriteLine("PASS Industrial Cleanup read-only counters");
    }
}
