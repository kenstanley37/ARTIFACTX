using System.Xml;
using System.Xml.Linq;
using System.Text;

namespace ArtifactX.Core.Unlocks;

/// <summary>Detached edits to the settings mirror of UnlockedSpecials.
/// Confirmed in real GCUSERSETTINGSDATA.MXML and GcUserSettingsData on
/// 2026-11-03; native account initialization imports this +0xB0 list.
/// Changes only the selected reward and list indices, not other settings.</summary>
public static class UserSettingsSpecialEdits
{
    private static XElement List(XDocument document)
    {
        var lists = document.Root?.Elements("Property").Where(p =>
            (string?)p.Attribute("name") == "UnlockedSpecials" && p.Attribute("value") is null).ToArray();
        return lists?.Length == 1 ? lists[0] : throw new InvalidDataException("The game settings unlock list has an unexpected format.");
    }

    public static HashSet<string> Read(string xml) => List(XDocument.Parse(xml, LoadOptions.PreserveWhitespace))
        .Elements("Property").Select(p => ((string?)p.Attribute("value") ?? "").TrimStart('^'))
        .Where(s => s.Length > 0).ToHashSet(StringComparer.Ordinal);

    public static string Set(string xml, string id, bool enabled)
    {
        id = id.TrimStart('^');
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An unlock id is required.", nameof(id));
        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var list = List(document);
        var matching = list.Elements("Property").Where(p => ((string?)p.Attribute("value"))?.TrimStart('^') == id).ToArray();
        if (enabled && matching.Length > 0 || !enabled && matching.Length == 0) return xml;
        if (enabled) list.Add(new XElement("Property", new XAttribute("name", "UnlockedSpecials"), new XAttribute("value", id)));
        else foreach (var entry in matching) entry.Remove();
        int index = 0;
        foreach (var entry in list.Elements("Property")) entry.SetAttributeValue("_index", index++);
        using var output = new Utf8Writer();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings
        { Indent = false, OmitXmlDeclaration = document.Declaration is null, NewLineHandling = NewLineHandling.None }))
            document.Save(writer);
        return output.ToString();
    }

    private sealed class Utf8Writer : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
