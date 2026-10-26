using System.Numerics;
using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    private const string SolarGenerationGlobalsFile = "GCSOLARGENERATIONGLOBALS.GLOBAL.MBIN";
    private const string SimulationGlobalsFile = "GCSIMULATIONGLOBALS.GLOBAL.MBIN";
    private const string VerifiedStandardTypeBRoot = "MODELS/SPACE/SPACESTATION/SPACESTATIONTYPEB.SCENE.MBIN";
    private const string VerifiedTypeBLaunchReference = "_AttachPoint_LaunchTubeA";
    private const string StationScenePrefix = "MODELS/SPACE/SPACESTATION/";

    /// <summary>
    /// Extracts station-placement globals and the current standard TypeB station's actual
    /// WeaponTerminal interaction origins per launch branch, plus the complete nested root
    /// descriptor. Pirate, abandoned and legacy roots are recorded
    /// but deliberately not assigned TypeB cabinet geometry.
    /// </summary>
    private static StationEquipmentTables? StationEquipment(Func<string, NMSTemplate?> decode)
    {
        if (decode(SolarGenerationGlobalsFile) is not GcSolarGenerationGlobals solar
            || decode(SimulationGlobalsFile) is not GcSimulationGlobals simulation)
            return null;

        static string PathOf(GcFilename? file) => NormalizeScenePath(file?.Value ?? "");
        var roots = new StationSceneRoots(
            PathOf(simulation.SpaceStationFile),
            PathOf(simulation.LegacySpaceStationFile),
            PathOf(simulation.PirateSystemSpaceStationFile),
            PathOf(simulation.AbandonedSpaceStationFile));
        var debug = decode("GCDEBUGOPTIONS.GLOBAL.MBIN") as GcDebugOptions;

        var layout = new SolarLayoutTables(
            new[] { solar.SolarSystemSize.X, solar.SolarSystemSize.Y, solar.SolarSystemSize.Z },
            new[] { solar.PerPlanetGenerationAngleChangeDegreesRange.X, solar.PerPlanetGenerationAngleChangeDegreesRange.Y },
            new[] { solar.PerPlanetGenerationElevationChangeDegreesRange.X, solar.PerPlanetGenerationElevationChangeDegreesRange.Y },
            solar.SolarSystemMaximumRadius,
            solar.SolarSystemMaximumRadiusMassive,
            solar.GenerateForcedNumberPlanets,
            solar.GenerateMaximumSolarSystem,
            solar.MassiveSolarSystems,
            solar.StationSpawnAvoidRadius,
            new[] { solar.SpawnPointStationToPlanetInterpRange.X, solar.SpawnPointStationToPlanetInterpRange.Y },
            debug is { BootMode: GcDebugOptions.BootModeEnum.SolarSystem }
                ? true : null);

        if (!roots.Standard.Equals(VerifiedStandardTypeBRoot, StringComparison.OrdinalIgnoreCase))
            return new StationEquipmentTables(layout, roots, null, ForceLegacySpaceStations: debug?.UseLegacySpaceStations);
        var variants = new[] { "_AttachPoint_LaunchTubeA", "_AttachPoint_LaunchTubeB", "_AttachPoint_LaunchTubeC" }
            .Select(reference => ExtractVerifiedTypeB(roots.Standard, reference, decode))
            .OfType<StationEquipmentScene>().ToArray();
        string descriptorPath = roots.Standard.Replace(".SCENE.MBIN", ".DESCRIPTOR.MBIN", StringComparison.Ordinal);
        string? descriptor = decode(descriptorPath) is TkModelDescriptorList list
            ? System.Text.Json.JsonSerializer.Serialize(StationDescriptorGroups(list)) : null;
        return new StationEquipmentTables(layout, roots,
            variants.FirstOrDefault(v => v.LaunchReference == VerifiedTypeBLaunchReference), descriptor, variants,
            debug?.UseLegacySpaceStations);
    }

    // Children are List<NMSTemplate>; serializing the base type silently loses the nested
    // groups. Keep the catalog's existing ShipParts JSON shape with explicit recursion.
    private static object[] StationDescriptorGroups(TkModelDescriptorList list) =>
        (list.List ?? new()).Select(g => (object)new
        {
            t = g.TypeId?.Value ?? "",
            o = (g.Descriptors ?? new()).Select(d => new
            {
                id = d.Id?.Value ?? "", n = d.Name?.Value ?? "",
                s = (d.ReferencePaths ?? new()).Select(r => r.Value).ToArray(),
                c = (d.Children ?? new()).OfType<TkModelDescriptorList>().Select(StationDescriptorGroups).ToArray(),
            }).ToArray(),
        }).ToArray();

    private static StationEquipmentScene? ExtractVerifiedTypeB(string rootScene, string launchReference, Func<string, NMSTemplate?> decode)
    {
        var cabinets = new List<StationCabinetDefinition>();
        var activeScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var decoded = new Dictionary<string, NMSTemplate?>(StringComparer.OrdinalIgnoreCase);
        NMSTemplate? DecodeCached(string path)
        {
            path = NormalizeScenePath(path);
            if (!decoded.TryGetValue(path, out var value)) decoded[path] = value = decode(path);
            return value;
        }

        void WalkScene(string scenePath, Matrix4x4 parent, IReadOnlyList<StationSceneReference> chain, int depth)
        {
            scenePath = NormalizeScenePath(scenePath);
            if (depth > 16 || !scenePath.StartsWith(StationScenePrefix, StringComparison.OrdinalIgnoreCase)
                || !activeScenes.Add(scenePath) || DecodeCached(scenePath) is not TkSceneNodeData root)
                return;

            WalkNode(root, scenePath, "", parent, chain, depth);
            activeScenes.Remove(scenePath);
        }

        void WalkNode(TkSceneNodeData node, string scenePath, string parentNodePath, Matrix4x4 parent,
            IReadOnlyList<StationSceneReference> chain, int depth)
        {
            string name = node.Name?.Value ?? "";
            string nodePath = parentNodePath.Length == 0 ? name : parentNodePath + "/" + name;
            Matrix4x4 localToStation = Local(node.Transform) * parent;

            string attachmentPath = Attribute(node, "ATTACHMENT");
            if (attachmentPath.Length > 0
                && WeaponTerminal(attachmentPath, DecodeCached) is { } terminal)
            {
                Vector3 position = Vector3.Transform(Vector3.Zero, localToStation);
                string id = chain.Count > 0 ? LeafName(chain[^1].NodePath) : name;
                cabinets.Add(new StationCabinetDefinition(
                    id,
                    scenePath,
                    nodePath,
                    NormalizeScenePath(attachmentPath),
                    terminal.InteractionType.InteractionType.ToString(),
                    terminal.ReseedAfterRewardSuccess,
                    new[] { position.X, position.Y, position.Z },
                    MatrixValues(localToStation),
                    chain.ToArray(),
                    StationWeaponPool(terminal),
                    (terminal.InteractionSpecificData as GcWeaponTerminalInteractionData)?.RespawnPeriodInSeconds,
                    (terminal.InteractionSpecificData as GcWeaponTerminalInteractionData)?.UseSentinelWeapon));
            }

            string reference = NormalizeScenePath(Attribute(node, "SCENEGRAPH"));
            // Extract each launch branch independently. Its own scene graph supplies the
            // transforms; descriptor selection later chooses exactly one branch.
            bool verifiedRootHop = chain.Count > 0
                || name.Equals(launchReference, StringComparison.OrdinalIgnoreCase);
            if (verifiedRootHop && reference.Length > 0
                && reference.StartsWith(StationScenePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var hop = new StationSceneReference(scenePath, nodePath, reference,
                    TransformOf(node.Transform), MatrixValues(localToStation));
                WalkScene(reference, localToStation, chain.Append(hop).ToArray(), depth + 1);
            }

            foreach (var child in node.Children ?? new())
                WalkNode(child, scenePath, nodePath, localToStation, chain, depth);
        }

        WalkScene(rootScene, Matrix4x4.Identity, Array.Empty<StationSceneReference>(), 0);
        if (cabinets.Count == 0) return null;

        // A scene can reference the same interaction through more than one presentation node.
        // Keep distinct physical origins, with a stable order for catalog JSON and later generation.
        var exact = cabinets
            .GroupBy(c => $"{c.AttachmentPath}|{c.LocalPosition[0]:R}|{c.LocalPosition[1]:R}|{c.LocalPosition[2]:R}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ThenBy(c => c.LocalPosition[2])
            .ToArray();
        return new StationEquipmentScene("StandardTypeB" + launchReference.Replace("_AttachPoint_", ""), rootScene, exact, launchReference);
    }

    private static GcInteractionComponentData? WeaponTerminal(string attachmentPath, Func<string, NMSTemplate?> decode)
    {
        if (decode(NormalizeScenePath(attachmentPath)) is not TkAttachmentData attachment) return null;
        return (attachment.Components ?? new())
            .Select(c => c?.Template)
            .OfType<GcInteractionComponentData>()
            .FirstOrDefault(c => c.InteractionType?.InteractionType == GcInteractionType.InteractionTypeEnum.WeaponTerminal);
    }

    /// <summary>
    /// NMS build 180836, 0x141003610: WeaponTerminal with no specific data is Standard;
    /// UseSentinelWeapon selects Sentinel, otherwise a positive respawn period selects
    /// SettlementRotational. Unknown specific-data types are deliberately unsupported.
    /// </summary>
    private static string? StationWeaponPool(GcInteractionComponentData terminal)
    {
        if (terminal.InteractionSpecificData is null)
            return GcMultitoolPoolType.MultiToolPoolTypeEnum.Standard.ToString();
        if (terminal.InteractionSpecificData is not GcWeaponTerminalInteractionData weapon) return null;
        return (weapon.UseSentinelWeapon ? GcMultitoolPoolType.MultiToolPoolTypeEnum.Sentinel
            : weapon.RespawnPeriodInSeconds > 0 ? GcMultitoolPoolType.MultiToolPoolTypeEnum.SettlementRotational
            : GcMultitoolPoolType.MultiToolPoolTypeEnum.Standard).ToString();
    }

    private static string Attribute(TkSceneNodeData node, string key) =>
        node.Attributes?.FirstOrDefault(a => string.Equals(a.Name?.Value, key, StringComparison.OrdinalIgnoreCase))
            ?.Value?.Value ?? "";

    private static string NormalizeScenePath(string path) =>
        path.Replace('\\', '/').Replace(".MXML", ".MBIN", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();

    private static string LeafName(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path[(slash + 1)..] : path;
    }

    private static StationSceneTransform TransformOf(TkTransformData transform) => new(
        transform.TransX, transform.TransY, transform.TransZ,
        transform.RotX, transform.RotY, transform.RotZ,
        transform.ScaleX, transform.ScaleY, transform.ScaleZ);

    /// <summary>The game's scene transform: scale, rotate X/Y/Z in degrees, then translate.</summary>
    private static Matrix4x4 Local(TkTransformData transform)
    {
        const float radians = MathF.PI / 180f;
        var matrix = Matrix4x4.CreateScale(transform.ScaleX, transform.ScaleY, transform.ScaleZ)
                   * Matrix4x4.CreateRotationX(transform.RotX * radians)
                   * Matrix4x4.CreateRotationY(transform.RotY * radians)
                   * Matrix4x4.CreateRotationZ(transform.RotZ * radians);
        matrix.Translation = new Vector3(transform.TransX, transform.TransY, transform.TransZ);
        return matrix;
    }

    private static float[] MatrixValues(Matrix4x4 matrix) =>
    [
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44,
    ];
}
