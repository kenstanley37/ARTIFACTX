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
        Loaded += TemplatePreview_Loaded;
    }

    public TemplatePreviewViewModel Preview { get; }

    private async void TemplatePreview_Loaded(object sender, RoutedEventArgs e)
    {
        if (Preview.HasModelPreview)
        {
            ShipModelPreview.Subject = Preview.ModelSubject;
            if (Preview.Template.SourceKind == "Freighter")
            {
                ShipModelPreview.StartYawOffset = Math.PI;
            }
            await ShipModelPreview.ShowAsync(
                Preview.Template.TypeScenePath,
                Preview.Template.Seed,
                built: Preview.BuiltLook);
        }
    }

    /// <summary>Shows a template's contents in a dialog. True when the player chose the
    /// dialog's action (useLabel, e.g. "Apply"); null useLabel shows Close only.</summary>
    public static async Task<bool> ShowAsync(XamlRoot root, NmsLoadoutTemplate template, string? useLabel = null)
    {
        await CatalogService.WarmCacheAsync(template.TechItems.Select(t => t.ItemId).OfType<string>().Distinct());
        var previewVm = new TemplatePreviewViewModel(template);
        var previewControl = new TemplatePreview(previewVm);
        var dialog = new ContentDialog
        {
            Title = template.Name,
            Content = new ScrollViewer { Content = previewControl, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            CloseButtonText = useLabel is null ? "Close" : "Cancel",
            XamlRoot = root,
        };
        if (previewVm.HasModelPreview)
        {
            dialog.Resources["ContentDialogMaxWidth"] = 1000.0;
        }
        if (useLabel is not null)
        {
            dialog.PrimaryButtonText = useLabel;
            dialog.DefaultButton = ContentDialogButton.Close;
        }
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
