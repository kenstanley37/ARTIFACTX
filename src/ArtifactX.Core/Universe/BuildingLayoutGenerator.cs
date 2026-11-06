namespace ArtifactX.Core.Universe;

/// <summary>One placed model of a building layout: its upper-case scene path and its transform in the layout's root
/// frame (the root model at the origin, before the re-centring on the bounds).</summary>
public sealed record BuildingLayoutNode(string Model, LayoutTransform World, int Parent);

/// <summary>A generated layout, its bounds (mesh boxes through each node, metres) and what the game derives from them:
/// the record radius (half the XZ diagonal, at least MinRadius), the root offset that re-centres the layout on its
/// XZ bounds, and (for shop layouts) the weapon crate's point in the root room (<see cref="WeaponCrateRoot"/>) and in
/// the re-centred building frame (<see cref="WeaponCrate"/>: side, up, forward).</summary>
public sealed record BuildingLayout(
    BuildingLayoutNode[] Nodes,
    (double X, double Y, double Z) BoundsMin,
    (double X, double Y, double Z) BoundsMax,
    double Radius,
    (double X, double Z) RootOffset,
    (double X, double Y, double Z)? WeaponCrateRoot = null,
    (double X, double Y, double Z)? WeaponCrate = null);

/// <summary>
/// The game's L-system expander (cTkLSystemManager, build 184271) for planet building layouts, ported from
/// scripts/research/region-generator/lsysport.py (docs/research/PLANETARY_MULTITOOL_LOCATIONS.md, "L-system expander
/// ported"):
/// - rules: templates merged first, the file on top; rules match by model; Balance appends new models to an inner
///   rule, Prioritize puts them first, Replace removes the inner rule and appends the new one; the first BaseRule is
///   the root. An empty global restriction list keeps the template's;
/// - chain restrictions (0x142D768B0): each AtLeast global adds AtLeast 1 to its single reachable producer, up the tree;
/// - GenerateStep 0x142D79260: breadth-first queue; per inner rule, each active locator (a direct child of the node,
///   or a child of its Interior_ locator; upper-cased name containing the locator type) gets one pick (0x142D851D0: a
///   forced minimum, else one MWC draw on the manager's running state) and is switched off; the child is aligned to
///   its own matching locator (opposite _IN_/_OUT_ type, any type for plain ones), which is switched off too;
/// - bounds: each node model's mesh box (<see cref="LayoutScene.BoxMin"/>) through the node.
/// A building record's stored sample n (n = record index % 3) uses the L-system's seed with lo + 100 n (<see cref="SampleSeed"/>).
/// Checked 2026-11-06 in Python against the 8 Vy'keen L-systems x 3 samples of Kaim 30/19 read live (read-only): 24/24
/// layouts, 1,056/1,056 nodes in order, positions 0.0000 m; radii 22/24 exact, the shelter's other two within 5 mm.
/// This port is checked against the same samples by the GameData checks (--building-layout-check).
/// </summary>
public static class BuildingLayoutGenerator
{
    private const ulong Multiplier = 0x5A76F899;
    private const int Unlimited = 1_000_000;

    /// <summary>Record sample n's seed: the L-system seed with its low word + 100 (n % 3).</summary>
    public static ulong SampleSeed(ulong seed, int n) =>
        (seed & 0xFFFFFFFF00000000UL) | (uint)((uint)seed + 100 * (uint)(n % 3));

    /// <summary>The layout for this seed, or null when the L-system or its root scene isn't in the tables.</summary>
    public static BuildingLayout? Generate(BuildingLayoutTables tables, string lsystem, ulong seed)
    {
        var rules = RuleSet.Build(tables, Path(lsystem));
        if (rules?.Root is not { } rootRule) return null;
        uint lo = (uint)seed, hi = (uint)(seed >> 32);
        uint s0 = lo == 0 ? 1 : lo, s1 = ((lo >> 16) | (lo << 16)) ^ hi ^ lo;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var nodes = new List<Node> { new(rootRule.Model, LayoutTransform.Identity, -1) };
        var queue = new Queue<int>([0]);
        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            var node = nodes[index];
            if (rules.RuleFor(node.Model) is not { } rule) continue;
            var locators = Locators(tables, node.Model);
            foreach (var inner in rule.Inner)
            {
                for (int li = 0; li < locators.Length; li++)
                {
                    string name = locators[li].Name.ToUpperInvariant();
                    if (node.Off.Contains(li) || !name.Contains(inner.Locator, StringComparison.Ordinal)) continue;
                    node.Off.Add(li);
                    int pick = Pick(rules, inner, nodes, index, counts, ref s0, ref s1);
                    if (pick < 0) continue;
                    var entry = inner.Entries[pick];
                    var at = node.World.Then(new LayoutTransform(locators[li].Matrix));
                    var child = new Node(entry.Model, at, index);
                    int want = LocatorKind(name) switch { 0 => 1, 1 => 0, _ => 2 };
                    var own = Locators(tables, entry.Model);
                    for (int ci = 0; ci < own.Length; ci++)
                    {
                        string childName = own[ci].Name.ToUpperInvariant();
                        if (!childName.Contains(inner.Locator, StringComparison.Ordinal) || (want != 2 && LocatorKind(childName) != want)) continue;
                        child.World = at.Then(new LayoutTransform(own[ci].Matrix).Inverse());
                        child.Off.Add(ci);
                        break;
                    }
                    node.Children.Add(nodes.Count);
                    counts[entry.Model] = counts.GetValueOrDefault(entry.Model) + 1;
                    nodes.Add(child);
                    if (rules.RuleFor(entry.Model) is not null) queue.Enqueue(nodes.Count - 1);
                }
            }
        }

        var min = (X: double.MaxValue, Y: double.MaxValue, Z: double.MaxValue);
        var max = (X: double.MinValue, Y: double.MinValue, Z: double.MinValue);
        foreach (var n in nodes)
        {
            if (!tables.Scenes.TryGetValue(n.Model, out var scene) || scene.BoxMin is not { } a || scene.BoxMax is not { } b) continue;
            for (int c = 0; c < 8; c++)
            {
                var p = n.World.Apply((c & 4) != 0 ? b[0] : a[0], (c & 2) != 0 ? b[1] : a[1], (c & 1) != 0 ? b[2] : a[2]);
                min = (Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = (Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }
        }
        if (min.X > max.X) return null;
        double radius = Math.Max(0.5 * Math.Sqrt(Math.Pow(max.X - min.X, 2) + Math.Pow(max.Z - min.Z, 2)), tables.MinRadius);
        var rootOffset = (X: -(min.X + max.X) / 2, Z: -(min.Z + max.Z) / 2);
        (double X, double Y, double Z)? crateRoot = null, crate = null;
        var crateNode = nodes.FirstOrDefault(n => n.Model.EndsWith("/FLOORLAYOUT_NPC.SCENE.MBIN", StringComparison.Ordinal)
                && tables.Scenes.TryGetValue(n.Model, out var sc) && sc.WeaponCrate is { Length: 12 })
            ?? nodes.FirstOrDefault(n => tables.Scenes.TryGetValue(n.Model, out var sc) && sc.WeaponCrate is { Length: 12 });
        if (crateNode is not null && tables.Scenes[crateNode.Model].WeaponCrate is { } crateMatrix)
        {
            var cr = crateNode.World.Then(new LayoutTransform(crateMatrix)).Position;
            crateRoot = cr;
            crate = (cr.X + rootOffset.X, cr.Y, cr.Z + rootOffset.Z);
        }
        else if (nodes.FirstOrDefault(n => n.Model.Contains("CRATE_WEAPON", StringComparison.Ordinal)) is { } directCrate)
        {
            var cr = directCrate.World.Position;
            crateRoot = cr;
            crate = (cr.X + rootOffset.X, cr.Y, cr.Z + rootOffset.Z);
        }
        return new BuildingLayout(
            nodes.Select(n => new BuildingLayoutNode(n.Model, n.World, n.Parent)).ToArray(),
            min, max, radius, rootOffset, crateRoot, crate);
    }

    /// <summary>The L-system path for a race (GcAlienRace), building class (GcBuildingClassification) and system type
    /// (0 Normal, 1 AbandonedSystem) from the catalog's building table, or null when not present.</summary>
    public static string? LSystemPath(BuildingLayoutTables tables, int race, int buildingClass, int systemType = 0) =>
        tables.BuildingLSystems is { } r && race >= 0 && race < r.Length
        && r[race] is { } b && buildingClass >= 0 && buildingClass < b.Length
        && b[buildingClass] is { } s && systemType >= 0 && systemType < s.Length && s[systemType].Length > 0
            ? s[systemType] : null;

    /// <summary>0x142D851D0: the first entry below a local or global minimum, else one draw r = u * 100 over the
    /// candidates (entries under both maxima), each weighted p + pooled / n, pooled = the excluded entries' p; -1 = nothing.</summary>
    private static int Pick(RuleSet rules, Inner inner, List<Node> nodes, int parent, Dictionary<string, int> counts, ref uint s0, ref uint s1)
    {
        var candidates = new List<int>();
        float pooled = 0;
        for (int i = 0; i < inner.Entries.Count; i++)
        {
            var e = inner.Entries[i];
            var global = rules.Globals.GetValueOrDefault(e.Model) ?? [];
            int localMax = MaxOf(e.Restrictions), globalMax = MaxOf(global);
            int localMin = e.Restrictions.Where(r => r.Type != 0).Sum(r => r.Amount);
            int globalMin = global.Where(r => r.Type != 0).Sum(r => r.Amount);
            int localCount = nodes[parent].Children.Count(c => nodes[c].Model == e.Model);
            int globalCount = counts.GetValueOrDefault(e.Model);
            if (localCount < localMin || globalCount < globalMin) return i;
            if (localCount < localMax && globalCount < globalMax) candidates.Add(i);
            else pooled += e.Probability;
        }
        if (candidates.Count == 0) return -1;
        ulong step = s0 * Multiplier + s1;
        s0 = (uint)step; s1 = (uint)(step >> 32);
        float r = (float)(s0 * Math.Pow(2, -32)) * 100f;
        float cumulative = 0;
        foreach (int i in candidates)
        {
            cumulative += pooled / candidates.Count + inner.Entries[i].Probability;
            if (r < cumulative) return i;
        }
        return -1;
    }

    private static int MaxOf(IReadOnlyList<LSystemRestriction> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i].Type == 0) return list[i].Amount;
        return Unlimited;
    }

    private static int LocatorKind(string upperName) =>
        upperName.Contains("_IN_", StringComparison.Ordinal) ? 0 : upperName.Contains("_OUT_", StringComparison.Ordinal) ? 1 : 2;

    private static LayoutLocator[] Locators(BuildingLayoutTables tables, string model) =>
        tables.Scenes.TryGetValue(model, out var scene) ? scene.Locators : [];

    internal static string Path(string path) => path.Replace('\\', '/').ToUpperInvariant();

    private sealed class Node(string model, LayoutTransform world, int parent)
    {
        public string Model { get; } = model;
        public LayoutTransform World { get; set; } = world;
        public int Parent { get; } = parent;
        public List<int> Children { get; } = [];
        public HashSet<int> Off { get; } = [];
    }

    private sealed record Entry(string Model, float Probability, LSystemRestriction[] Restrictions);

    private sealed class Inner(string locator, List<Entry> entries)
    {
        public string Locator { get; } = locator;
        public List<Entry> Entries { get; } = entries;
    }

    private sealed class Rule(string model)
    {
        public string Model { get; } = model;
        public List<Inner> Inner { get; } = [];
    }

    /// <summary>An L-system file with its templates merged, its root and its chained global restrictions.</summary>
    private sealed class RuleSet
    {
        public List<Rule> Rules { get; } = [];
        public Rule? Root { get; private set; }
        // Insertion-ordered: the chain pass walks the globals in the order they were first set.
        public Dictionary<string, List<LSystemRestriction>> Globals { get; } = new(StringComparer.Ordinal);
        private readonly List<string> globalOrder = [];

        public Rule? RuleFor(string model) => Rules.FirstOrDefault(r => r.Model == model);

        public static RuleSet? Build(BuildingLayoutTables tables, string path)
        {
            var set = new RuleSet();
            if (!set.Merge(tables, path, 0) || set.Rules.Count == 0) return null;
            set.Root ??= set.Rules[0];
            set.Chain();
            return set;
        }

        private bool Merge(BuildingLayoutTables tables, string path, int depth)
        {
            if (depth > 16 || !tables.LSystems.TryGetValue(path, out var file)) return false;
            foreach (var template in file.Templates) Merge(tables, Path(template), depth + 1);
            foreach (var g in file.Globals)
            {
                string model = Path(g.Model);
                if (g.Restrictions.Length == 0 && Globals.ContainsKey(model)) continue;   // an empty list keeps the template's
                if (!Globals.ContainsKey(model)) globalOrder.Add(model);
                Globals[model] = [.. g.Restrictions];
            }
            foreach (var r in file.Rules)
            {
                string model = Path(r.Model);
                var rule = RuleFor(model);
                if (rule is null) Rules.Add(rule = new Rule(model));
                if (r.IsBase) Root ??= rule;
                foreach (var ir in r.Rules)
                {
                    var entries = ir.Entries.Select(e => new Entry(Path(e.Model), e.Probability, e.Restrictions)).ToList();
                    string locator = ir.LocatorType.ToUpperInvariant();
                    var old = rule.Inner.FirstOrDefault(x => x.Locator == locator);
                    if (ir.Merge == 2 || old is null)
                    {
                        if (old is not null) rule.Inner.Remove(old);
                        rule.Inner.Add(new Inner(locator, entries));
                        continue;
                    }
                    var added = entries.Where(e => !old.Entries.Any(x => x.Model == e.Model)).ToList();
                    if (ir.Merge == 1) old.Entries.InsertRange(0, added);
                    else old.Entries.AddRange(added);
                }
            }
            return true;
        }

        private void Chain()
        {
            var reach = new HashSet<string>(StringComparer.Ordinal) { Root!.Model };
            var stack = new Stack<string>([Root.Model]);
            while (stack.Count > 0)
            {
                if (RuleFor(stack.Pop()) is not { } r) continue;
                foreach (var e in r.Inner.SelectMany(i => i.Entries))
                    if (reach.Add(e.Model)) stack.Push(e.Model);
            }
            var todo = new Queue<string>(globalOrder.Where(m => Globals[m].Any(r => r.Type == 1 && r.Amount > 0)));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (todo.Count > 0)
            {
                string model = todo.Dequeue();
                if (!seen.Add(model)) continue;
                var producers = Rules.Where(r => reach.Contains(r.Model) && r.Inner.Any(i => i.Entries.Any(e => e.Model == model))).ToList();
                if (producers.Count != 1 || producers[0] == Root) continue;
                string producer = producers[0].Model;
                if (!Globals.TryGetValue(producer, out var list))
                {
                    Globals[producer] = list = [];
                    globalOrder.Add(producer);
                }
                list.Add(new LSystemRestriction(1, 1));
                todo.Enqueue(producer);
            }
        }
    }
}
