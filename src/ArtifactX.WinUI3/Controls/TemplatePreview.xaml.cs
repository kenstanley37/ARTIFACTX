using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class TemplatePreview : UserControl
{
    public TemplatePreview(TemplatePreviewViewModel preview)
    {
        Preview = preview;
        InitializeComponent();
    }

    public TemplatePreviewViewModel Preview { get; }

    /// <summary>Shows a template's contents in a dialog. True when the player chose the
    /// dialog's action (useLabel, e.g. "Apply"); null useLabel shows Close only.</summary>
    public static async Task<bool> ShowAsync(XamlRoot root, NmsLoadoutTemplate template, string? useLabel = null)
    {
        await CatalogService.WarmCacheAsync(template.TechItems.Select(t => t.ItemId).OfType<string>().Distinct());
        var dialog = new ContentDialog
        {
            Title = template.Name,
            Content = new ScrollViewer { Content = new TemplatePreview(new TemplatePreviewViewModel(template)) },
            CloseButtonText = useLabel is null ? "Close" : "Cancel",
            XamlRoot = root,
        };
        if (useLabel is not null)
        {
            dialog.PrimaryButtonText = useLabel;
            dialog.DefaultButton = ContentDialogButton.Close;
        }
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
