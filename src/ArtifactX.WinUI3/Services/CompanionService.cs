using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>A tamed pet in the save's companion array (Mcl): its slot and the label the
/// picker shows (its name, or "Species #N" for one never renamed).</summary>
public sealed record CompanionEntry(int Index, string Label);

/// <summary>
/// Animal Companions ("Pets") in the save - a flat 30-slot array (Mcl), unlike every
/// other page's :No/hl? inventory-grid shape. See NmsCompanionPaths for the confirmed
/// fields, and CompanionViewModel for how each is shown and edited.
/// </summary>
public static class CompanionService
{
    /// <summary>
    /// The occupied slots. Occupancy is XID (species), NOT fH8 (custom name) - a freshly
    /// tamed pet the player never manually renamed has fH8 == "" while still occupying a
    /// real slot. The fancy auto-generated name shown in-game (e.g. "Riverpito") is
    /// computed client-side for display and is never written to the save at all -
    /// confirmed 2026-07-28 by decrypting a real save with 7 such pets and full-text-
    /// searching the raw JSON for their names, which found nothing anywhere in the file.
    /// Filtering on fH8 (the original assumption) silently dropped every one of them.
    /// </summary>
    public static List<CompanionEntry> ReadCompanions()
    {
        var companions = new List<CompanionEntry>();
        if (SaveSessionManager.GetValue(NmsCompanionPaths.CompanionArrayPath) is not JArray array) return companions;

        for (int i = 0; i < array.Count; i++)
        {
            // XID is "^" (a bare, contentless prefix) on a truly empty slot, not "" -
            // IsNullOrEmpty alone doesn't catch it.
            string xid = array[i]?["XID"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(xid.TrimStart('^'))) continue;

            string customName = array[i]?["fH8"]?.Value<string>() ?? "";
            companions.Add(new CompanionEntry(i, string.IsNullOrEmpty(customName) ? $"{FormatArchetype(xid)} #{i + 1}" : customName));
        }
        return companions;
    }

    /// <summary>"^PLANTCAT" -> "Plantcat" - just enough formatting to be readable; not
    /// the fancy in-game Latin species name (unconfirmed/likely not stored - see
    /// NmsCompanionPaths).</summary>
    public static string FormatArchetype(string raw)
    {
        string trimmed = raw.TrimStart('^');
        if (trimmed.Length == 0) return "";
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    /// <summary>Rebuilds a WHOLE new osl array for a different top-level
    /// archetype choice (e.g. TREX's _TREX_4 -> _TREX_3XRARE) - needed
    /// because the top-level entry determines which child slots even exist
    /// (HEAD/BODY/TAIL for one archetype, TAILB/TOPB for another), so a
    /// same-slot swap doesn't apply here the way it does for every other
    /// row. Walks the tree depth-first from newRootOptionId, and at each
    /// branch, visits sibling CATEGORIES in the game's own original order
    /// (via SortOrder - see CreatureDescriptorNode's doc comment) picking
    /// ONE default option per category (preferring a non-"(Rare)" option
    /// when one exists, then lowest SortOrder) - matching the exact
    /// depth-first shape confirmed against real save data (e.g. TREX's
    /// osl visits HEAD's full subtree, then BODY's, then TAIL's, in that
    /// order). Any entries from the CURRENT array that don't match any
    /// catalog node at all (the trailing per-instance detail seed seen on
    /// every sampled pet) are preserved as-is, appended after the rebuilt
    /// tree portion - there's no way to regenerate those and no evidence
    /// they need to change when the archetype does.
    /// UNTESTED IN-GAME as of 2026-07-29 - unlike the confirmed same-slot
    /// swap, nobody has yet confirmed the game accepts a full archetype
    /// swap built this way.</summary>
    public static JArray BuildDefaultDescriptorArray(List<CreatureDescriptorNode> tree, string newRootOptionId, JArray currentOsl)
    {
        var childrenByParent = tree
            .Where(n => n.ParentOptionId != null)
            .GroupBy(n => n.ParentOptionId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var result = new List<string>();

        void Visit(string optionId)
        {
            result.Add("^" + optionId);

            if (!childrenByParent.TryGetValue(optionId, out var children)) return;

            var categoriesInOrder = children
                .GroupBy(n => n.Category)
                .OrderBy(g => g.Min(n => n.SortOrder));

            foreach (var categoryGroup in categoriesInOrder)
            {
                var defaultChoice = categoryGroup
                    .OrderBy(n => n.OptionId.EndsWith("XRARE", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenBy(n => n.SortOrder)
                    .First();
                Visit(defaultChoice.OptionId);
            }
        }

        Visit(newRootOptionId);

        var byOptionId = new HashSet<string>(tree.Select(n => n.OptionId), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in currentOsl)
        {
            string raw = entry?.Value<string>() ?? "";
            if (!byOptionId.Contains(raw.TrimStart('^')))
                result.Add(raw);
        }

        return new JArray(result.Cast<object>());
    }

    /// <summary>Turns a raw descriptor slot code like "_HTAZEARS_" into
    /// "Htazears" - title-cased for readability. Rig slot names are the
    /// game's own internal shorthand (not always a recognizable English
    /// word), so this is cosmetic cleanup only, not a translation - see
    /// HumanizeDescriptorOption below for the same caveat on option
    /// values.</summary>
    public static string HumanizeDescriptorCategory(string category)
    {
        string trimmed = category.Trim('_');
        if (trimmed.Length == 0) return category;
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    /// <summary>Turns a raw descriptor option code like "_HEAD_TURTLE" into
    /// "Turtle" for the dropdown label - strips the category's own prefix
    /// (already shown as the row label via HumanizeDescriptorCategory),
    /// splits what's left on underscores, and capitalizes each piece. The
    /// game ships no separate display-name data for these at all -
    /// TkResourceDescriptorData's own "Name" field turned out to just be a
    /// differently-cased copy of the Id, not a real label, confirmed while
    /// decoding the tree - so this is the best available cleanup, not a
    /// translation. The exact raw code stays visible via each item's
    /// tooltip for anyone who wants to report back the literal value.</summary>
    public static string HumanizeDescriptorOption(string category, string rawCode)
    {
        string remainder = rawCode;
        if (remainder.StartsWith(category, StringComparison.OrdinalIgnoreCase))
            remainder = remainder[category.Length..];
        remainder = remainder.Trim('_');
        if (remainder.Length == 0) return rawCode.Trim('_');

        // "XRARE" is a real, recognizable rarity-tier suffix seen across
        // many categories in the game's own data (e.g. "_TREX_3XRARE",
        // "_TAIL_ALIENXRARE", "_HTAZEARS_0XRARE") - preserve it as a clean
        // "(Rare)" suffix instead of letting the generic path below mash it
        // into unreadable noise like "3xrare".
        bool isRare = remainder.EndsWith("XRARE", StringComparison.OrdinalIgnoreCase);
        if (isRare) remainder = remainder[..^5].Trim('_');

        string label;
        if (remainder.Length == 0)
        {
            label = "";
        }
        else if (remainder.All(char.IsDigit))
        {
            // Confirmed against the real catalog data (e.g. rodent's
            // _HTAZEARS_/_HTAZACC_/_TAZBACK_ slots) that some categories
            // are ENTIRELY bare-numbered variants with no descriptive text
            // anywhere in the game's own data - not a gap in this
            // formatting, there's genuinely nothing more to extract. Labeled
            // "Variant N" rather than a bare number so it reads as an
            // intentional unnamed option, not a broken label.
            label = $"Variant {remainder}";
        }
        else
        {
            var parts = remainder.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant());
            label = string.Join(' ', parts);
        }

        if (!isRare) return label;
        return label.Length == 0 ? "(Rare)" : $"{label} (Rare)";
    }
}
