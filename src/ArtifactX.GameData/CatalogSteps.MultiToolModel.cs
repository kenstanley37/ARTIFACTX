using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;
using System.Text.Json;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>Scene hierarchy and embedded inventory-component metadata; never scans arbitrary entity bytes for tags.</summary>
    private static MultiToolModelTables? MultiToolModel(Func<string, NMSTemplate?> decode)
    {
        const string model = SystemMultiToolGenerator.MainModel;
        string descriptorFile = model.Replace(".SCENE.MBIN", ".DESCRIPTOR.MBIN", StringComparison.Ordinal);
        if (decode(model) is not TkSceneNodeData root || decode(descriptorFile) is not TkModelDescriptorList descriptors) return null;
        var selectable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool ReadIds(TkModelDescriptorList list, int depth)
        {
            if (depth > 32) return false;
            foreach (var group in list.List ?? new())
                foreach (var option in group.Descriptors ?? new())
                {
                    selectable.Add(option.Id.Value);
                    foreach (var child in option.Children ?? new())
                        if (child is not TkModelDescriptorList nested || !ReadIds(nested, depth + 1)) return false;
                }
            return true;
        }
        if (!ReadIds(descriptors, 0)) return null;
        MultiToolModelNode ReadNode(TkSceneNodeData node, int depth)
        {
            string name = node.Name?.Value ?? "";
            bool unsupported = depth > 32 || string.IsNullOrWhiteSpace(name) || node.PlatformExclusion != 0
                || node.Type?.Value == "REFERENCE";
            var components = new List<MultiToolModelInventoryComponent>();
            var attachments = (node.Attributes ?? new()).Where(a => a.Name?.Value == "ATTACHMENT")
                .Select(a => (a.Value?.Value ?? "").Replace('\\', '/').ToUpperInvariant()).Where(p => p.Length > 0).ToArray();
            if (attachments.Length > 1) unsupported = true;
            foreach (string attachment in attachments)
            {
                if (decode(attachment) is not TkAttachmentData entity) { unsupported = true; continue; }
                if ((entity.AdditionalData?.Count ?? 0) > 0) unsupported = true;
                foreach (var link in entity.Components ?? new())
                {
                    if (link.Template is null) { unsupported = true; continue; }
                    if (link.Template is GcCustomInventoryComponentData inventory)
                    {
                        if (inventory.DesiredTechs is null) unsupported = true;
                        components.Add(new MultiToolModelInventoryComponent(inventory.Size?.Value ?? "",
                            (inventory.DesiredTechs?.Count ?? 0) > 0, inventory.Cool));
                    }
                }
            }
            return new MultiToolModelNode(name, selectable.Contains(name), unsupported, components.ToArray(),
                depth > 32 ? [] : (node.Children ?? new()).Select(child => ReadNode(child, depth + 1)).ToArray());
        }
        return new MultiToolModelTables(model, JsonSerializer.Serialize(StationDescriptorGroups(descriptors)), ReadNode(root, 0));
    }
}
