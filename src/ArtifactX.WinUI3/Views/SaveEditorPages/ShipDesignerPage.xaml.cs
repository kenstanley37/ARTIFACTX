using ArtifactX.GameData;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.ViewModels.ShipDesignerViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views.SaveEditorPages;

/// <summary>
/// The Ship Designer page (2026-10-16, GitHub #5). State and logic live in <see cref="ShipDesignerViewModel"/>;
/// the page only hosts what needs a XamlRoot or the preview control: dialogs, the 3D preview and navigation
/// (<see cref="IShipDesignerHost"/>). Split out of an 885-line code-behind 2026-11-10 (no behaviour change).
/// A navigation parameter of type int opens paint mode on that starship index.
/// </summary>
public sealed partial class ShipDesignerPage : Page, IShipDesignerHost
{
    public ShipDesignerPage()
    {
        ViewModel = new ShipDesignerViewModel(this);
        InitializeComponent();
        ViewModel.Attach();
        Unloaded += (_, _) => ViewModel.Detach();
    }

    public ShipDesignerViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync(e.Parameter is int ship ? ship : null);
    }

    // ------------------------------------------------------------------ IShipDesignerHost

    public async Task ShowMessageAsync(string title, string text) =>
        await new ContentDialog
        {
            Title = title, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, CloseButtonText = "OK", XamlRoot = XamlRoot,
        }.ShowAsync();

    public async Task<bool> ConfirmAsync(string title, string text, string action) =>
        await new ContentDialog
        {
            Title = title, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = action, CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot,
        }.ShowAsync() == ContentDialogResult.Primary;

    public async Task<string?> AskDesignNameAsync(string suggested)
    {
        var nameBox = new TextBox { Header = "Design name", Text = suggested, MaxLength = 80 };
        nameBox.SelectAll();
        var dialog = new ContentDialog
        {
            Title = "Save as a new design",
            Content = nameBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = suggested.Trim().Length > 0,
            XamlRoot = XamlRoot,
        };
        nameBox.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = nameBox.Text.Trim().Length > 0;
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? nameBox.Text.Trim() : null;
    }

    public async Task<bool> ShowApplyDesignAsync(ApplyDesignViewModel choice)
    {
        var dialog = new ContentDialog
        {
            Title = "Apply the design to which ship?",
            Content = new ShipApplyDesignPanel(choice),
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = choice.HasSelection,
            XamlRoot = XamlRoot,
        };
        void OnChoiceChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ApplyDesignViewModel.HasSelection)) dialog.IsPrimaryButtonEnabled = choice.HasSelection;
        }
        choice.PropertyChanged += OnChoiceChanged;
        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary && choice.HasSelection;
        }
        finally
        {
            choice.PropertyChanged -= OnChoiceChanged;
        }
    }

    /// <summary>No model: the plain placeholder grey; otherwise the unpainted hull grey under the textures.</summary>
    public void ShowPreview(ShipMesh? mesh, Func<string, ModelTexture?>? textureOf, bool hologram) =>
        PreviewView.Show(mesh, _ => mesh is null ? 0x9EA2AAu : 0x8C8E94u, textureOf, hologram);

    public Task<byte[]?> SnapshotPreviewAsync(int width, int height) => PreviewView.SnapshotAsync(width, height);

    public void OpenNewDesigner() => Frame.Navigate(typeof(ShipDesignerPage));
}
