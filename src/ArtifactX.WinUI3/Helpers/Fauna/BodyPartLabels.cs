using ArtifactX.WinUI3.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArtifactX.WinUI3.Helpers.Fauna;

/// <summary>
/// Plain names for a companion's body-part slots (its osl entries). The game has no display names for these - its
/// descriptor groups are internal codes such as _ACC_, _ACB_ or _TBACC_ - so a slot is named after the main part it
/// hangs off ("Head", "Tail", the species' "Body type"), plus the word its options share when they share one
/// ("Head: Ears"), else a number ("Head extra 1"). The raw code stays on the option tooltips.
/// </summary>
public static partial class BodyPartLabels
{
    // Top-level groups with an obvious word; any other top-level group is a species body type (_TREX_, _BONECAT_...).
    private static readonly Dictionary<string, string> Areas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HEAD"] = "Head", ["SHHEAD"] = "Head", ["FACE"] = "Face", ["TAIL"] = "Tail", ["SHTAIL"] = "Tail",
        ["BODY"] = "Body", ["SHBODY"] = "Body", ["MESH"] = "Skin", ["BACK"] = "Back", ["SHOULDERACC"] = "Shoulders",
        ["WINGS"] = "Wings", ["BWINGS"] = "Wings", ["FWINGS"] = "Front wings", ["MWINGS"] = "Wings", ["LEGS"] = "Legs",
        ["LEGSBACK"] = "Back legs", ["LEGSFRONT"] = "Front legs", ["SHELL"] = "Shell", ["CRABSHELL"] = "Shell",
        ["PLATING"] = "Plating", ["ACC"] = "Extras", ["SHAPE"] = "Shape",
    };

    // Words a group or its options can be named by when they say what the part is.
    private static readonly HashSet<string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        "EARS", "EAR", "HORNS", "HORN", "WING", "WINGS", "CLAWS", "SPIKES", "FEATHERS", "TEETH", "EYES", "FINS", "SHELL",
        "TAIL", "LEGS", "ANTENNA", "ANTENNAE", "TUSKS", "JAW", "CREST", "FRILL", "MANE", "BEAK", "SPINES", "PLATES",
    };

    /// <summary>The label for each slot in save order: the slot's tree node, or null for an entry with no catalog
    /// option (the per-instance detail seed).</summary>
    public static List<string> For(IReadOnlyList<CreatureDescriptorNode?> slots, IReadOnlyList<CreatureDescriptorNode> tree)
    {
        var byOption = tree.GroupBy(n => n.OptionId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var extras = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var labels = new List<string>();
        foreach (var node in slots)
        {
            if (node is null) { labels.Add("Detail seed"); continue; }
            var root = node;
            for (int guard = 0; root.ParentOptionId is { } parent && byOption.TryGetValue(parent, out var up) && guard < 32; guard++) root = up;
            string area = Areas.GetValueOrDefault(root.Category.Trim('_'), "Body type");
            if (ReferenceEquals(root, node)) { labels.Add(area); continue; }
            string? word = SharedWord(node, tree);
            if (word is not null) { labels.Add($"{(area == "Body type" ? "Body" : area)}: {word}"); continue; }
            int n = extras[area] = extras.GetValueOrDefault(area) + 1;
            labels.Add($"{(area == "Body type" ? "Body" : area)} extra {n}");
        }
        return labels;
    }

    /// <summary>The group's own word (_WING_ -> "Wing") or the word all its real options share (_ACB_EARS, _ACB_EARS2
    /// -> "Ears"), else null.</summary>
    private static string? SharedWord(CreatureDescriptorNode node, IReadOnlyList<CreatureDescriptorNode> tree)
    {
        string category = node.Category.Trim('_');
        if (Words.Contains(category)) return Title(category);
        var stems = tree.Where(n => n.Category == node.Category && n.ParentOptionId == node.ParentOptionId)
            .Select(n => Stem(n.OptionId, node.Category))
            .Where(s => s.Length > 0 && !s.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return stems.Count == 1 && Words.Contains(stems[0]) ? Title(stems[0]) : null;
    }

    /// <summary>An option's letters after its group prefix, without numbers or the rare tag (_ACB_EARS3 -> EARS).</summary>
    private static string Stem(string optionId, string category)
    {
        string rest = optionId.StartsWith(category, StringComparison.OrdinalIgnoreCase) ? optionId[category.Length..] : optionId;
        return Digits().Replace(rest.Replace("XRARE", "", StringComparison.OrdinalIgnoreCase), "").Trim('_');
    }

    private static string Title(string word) => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

    [GeneratedRegex("[0-9]")]
    private static partial Regex Digits();
}
