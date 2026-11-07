using System.Numerics;
using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.Recipes;

/// <summary>Read-only resource-network reconstruction. Evidence 2026-11-01:
/// real saved pipeline endpoints are Position and Position + At; extracted
/// resource-network masks/subgroups and summed connection radii reproduce the
/// owner's Paraffinium (10000) and Oxygen (10500) depot capacities. Those two
/// cases support this adapter, not arbitrary modded parts or network types.
/// UseMinDistance/socket-based rules are rejected until independently mapped.
/// No production, offline growth, substance identity or item rounding is inferred.</summary>
public static class BaseExtractionNetworks
{
    private sealed record Node(int Index, ExtractionNetworkRule Rule, Vector3[] Endpoints, ulong Stored);

    public static IReadOnlyList<ExtractionNetwork> Read(JArray objects, IReadOnlyDictionary<string, ExtractionNetworkRule> rules)
    {
        var nodes = new List<Node>();
        for (int index = 0; index < objects.Count; index++)
        {
            if (objects[index] is not JObject obj) continue;
            string partId = obj["r<7"]?.Value<string>()?.TrimStart('^') ?? "";
            if (!rules.TryGetValue(partId, out var rule)) continue;
            // Ordinary resource producers aren't necessarily industrial extractors.
            if (rule.HotspotKind == "None" && rule.Rate != 0) continue;
            if (rule.UseMinDistance || rule.Group is < 0 or >= 31 || rule.Mask < 0
                || !double.IsFinite(rule.Distance) || rule.Distance < 0 || rule.Storage < 0)
                throw new InvalidOperationException("Unsupported extraction network connection rule.");
            var position = Vector(obj["wMC"]);
            bool connector = rule.Storage == 0 && rule.Rate == 0;
            Vector3[] endpoints = connector ? [position, position + Vector(obj["aNu"])] : [position];
            ulong packed = obj["CVX"]?.Value<ulong>() ?? 0;
            nodes.Add(new(index, rule, endpoints, packed >> 32));
        }
        int[] parent = Enumerable.Range(0, nodes.Count).ToArray();
        int Find(int index)
        {
            while (parent[index] != index)
            { parent[index] = parent[parent[index]]; index = parent[index]; }
            return index;
        }
        for (int i = 0; i < nodes.Count; i++)
            for (int j = 0; j < i; j++)
            {
                var a = nodes[i]; var b = nodes[j];
                if ((a.Rule.Mask & (1 << b.Rule.Group)) == 0 || (b.Rule.Mask & (1 << a.Rule.Group)) == 0) continue;
                double distance = a.Rule.Distance + b.Rule.Distance;
                if (a.Endpoints.Any(p => b.Endpoints.Any(q => Vector3.DistanceSquared(p, q) < distance * distance)))
                    parent[Find(i)] = Find(j);
            }
        return nodes.Select((node, index) => (Node: node, Root: Find(index))).GroupBy(p => p.Root)
            .Select(group => new ExtractionNetwork(
                group.Where(p => p.Node.Rule.Storage > 0).Select(p => new ExtractionNetworkMember(p.Node.Index,
                    p.Node.Rule.PartId, p.Node.Stored, p.Node.Rule.Storage)).ToArray(),
                group.Where(p => p.Node.Rule.Storage == 0).Select(p => p.Node.Index).ToArray()))
            .Where(network => network.Members.Count > 0).ToArray();
    }

    private static Vector3 Vector(JToken? value)
    {
        if (value is not JArray { Count: 3 } array) throw new InvalidOperationException("Missing extraction network position.");
        var vector = new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
        if (!float.IsFinite(vector.X) || !float.IsFinite(vector.Y) || !float.IsFinite(vector.Z))
            throw new InvalidOperationException("Invalid extraction network position.");
        return vector;
    }
}
