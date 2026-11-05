using ArtifactX.GameData.Models;
using System.Numerics;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData.Services;

/// <summary>
/// Research inspection of Shop L-system rules and their referenced weapon-terminal assets.
/// Does not run L-system selection, resolve restrictions, place buildings, compose world
/// transforms or establish cabinet stock. Decoding stays in the GameData layer.
/// </summary>
public static class PlanetShopWeaponSurveyService
{
    /// <summary>Read-only install inspection. Does not create or modify the installed catalog.</summary>
    public static PlanetShopWeaponSurvey Read(GameInstall game)
    {
        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(game.PcbanksPath))
        {
            if (pak.FullPath is not { } path) continue;
            index.Add(path, reader.ReadHeader(path), reader.Read(path));
        }
        var extraction = new ExtractionService();
        NMSTemplate? Decode(string path)
        {
            if (!index.TryFind(path, out var pak, out var entry, out var header)) return null;
            var raw = extraction.ExtractEntryBytes(pak, entry, header);
            if (raw is null) return null;
            var file = new MBINFile(new MemoryStream(raw));
            file.Load();
            return file.GetData();
        }
        return Read(Decode) with { SourceFingerprint = game.Fingerprint };
    }

    public static PlanetShopWeaponSurvey Read(Func<string, NMSTemplate?> decode)
    {
        const string tablePath = "METADATA/SIMULATION/ENVIRONMENT/PLANETBUILDINGTABLE.MBIN";
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        var cache = new Dictionary<string, NMSTemplate?>(StringComparer.OrdinalIgnoreCase);
        NMSTemplate? Read(string path)
        {
            path = Normalize(path);
            if (cache.TryGetValue(path, out var found)) return found;
            var result = decode(path);
            cache[path] = result;
            if (result is null) unresolved.Add(path);
            return result;
        }

        if (Read(tablePath) is not GcBuildingDefinitionTable table)
            return new([], [tablePath]);
        var references = new List<ShopRuleWeaponReference>();
        int shop = (int)GcBuildingClassification.BuildingClassEnum.Shop;
        for (int raceIndex = 0; raceIndex < (table.BuildingFiles?.Length ?? 0); raceIndex++)
        {
            var files = table.BuildingFiles![raceIndex]?.BuildingFiles;
            if (files is null || files.Length <= shop) continue;
            var roots = files[shop]?.LSystem;
            if (roots is null) continue;
            for (int systemIndex = 0; systemIndex < roots.Length; systemIndex++)
            {
                string root = Normalize(roots[systemIndex]?.Value ?? "");
                if (root.Length == 0) continue;
                if (Read(root) is not TkLSystemRulesData rules) { unresolved.Add(root); continue; }
                string race = Enum.GetName((GcAlienRace.AlienRaceEnum)raceIndex) ?? raceIndex.ToString();
                string system = Enum.GetName((GcBuildingSystemTypeEnum.BuildingSystemTypeEnum)systemIndex)
                    ?? systemIndex.ToString();
                foreach (var rule in rules.Rules ?? new())
                foreach (var inner in rule.Rules ?? new())
                foreach (var entry in inner.Entries ?? new())
                {
                    string model = Normalize(entry.Model?.Value ?? "");
                    if (model.Length == 0) continue;
                    foreach (var terminal in Terminals(model, "", Matrix4x4.Identity, new(StringComparer.OrdinalIgnoreCase), 0))
                        references.Add(new(race, system, root, rule.Name?.Value ?? "",
                            Normalize(rule.Model?.Value ?? ""), inner.LocatorType?.Value ?? "", model,
                            entry.Probability, entry.Restrictions?.Count ?? 0, terminal));
                }
            }
        }
        return new(references.ToArray(), unresolved.ToArray());

        List<SceneWeaponTerminal> Terminals(string scene, string chain, Matrix4x4 parentTransform,
            HashSet<string> ancestors, int depth)
        {
            var result = new List<SceneWeaponTerminal>();
            if (depth > 32 || !ancestors.Add(scene)) { unresolved.Add(scene); return result; }
            try
            {
                if (Read(scene) is not TkSceneNodeData root) { unresolved.Add(scene); return result; }
                void Walk(TkSceneNodeData node, string parent, Matrix4x4 transform)
                {
                    var local = SceneTransformMath.Local(node.Transform) * transform;
                    string path = parent + "/" + (node.Name?.Value ?? "");
                    string attachmentPath = Normalize(Attribute(node, "ATTACHMENT"));
                    if (attachmentPath.Length > 0)
                    {
                        if (Read(attachmentPath) is TkAttachmentData attachment)
                        {
                            foreach (var interaction in (attachment.Components ?? new())
                                .Select(c => c.Template).OfType<GcInteractionComponentData>())
                            {
                                if (interaction.InteractionType?.InteractionType != GcInteractionType.InteractionTypeEnum.WeaponTerminal) continue;
                                var weapon = interaction.InteractionSpecificData as GcWeaponTerminalInteractionData;
                                var position = Vector3.Transform(Vector3.Zero, local);
                                result.Add(new(scene, path, attachmentPath, [position.X, position.Y, position.Z], interaction.ReseedAfterRewardSuccess,
                                    interaction.InteractionSpecificData?.GetType().Name,
                                    weapon?.RespawnPeriodInSeconds, weapon?.UseSentinelWeapon));
                            }
                        }
                        else unresolved.Add(attachmentPath);
                    }
                    string reference = Normalize(Attribute(node, "SCENEGRAPH"));
                    if (reference.Length > 0) result.AddRange(Terminals(reference, path, local, ancestors, depth + 1));
                    foreach (var child in node.Children ?? new()) Walk(child, path, local);
                }
                Walk(root, chain, parentTransform);
            }
            finally { ancestors.Remove(scene); }
            return result;
        }
    }

    private static string Attribute(TkSceneNodeData node, string name) => node.Attributes?
        .FirstOrDefault(a => a.Name?.Value.Equals(name, StringComparison.OrdinalIgnoreCase) == true)?.Value?.Value ?? "";

    private static string Normalize(string path) => path.Replace('\\', '/')
        .Replace(".MXML", ".MBIN", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
}
