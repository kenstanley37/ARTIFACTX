using ArtifactX.Core.Universe;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.Maui.ViewModels;

public sealed partial class SystemBodyItemViewModel : ObservableObject
{
    public SystemBodyItemViewModel(GeneratedSystemBody body)
    {
        Body = body;
        SwatchColor = Color.FromArgb(body.SwatchHex);
    }

    public GeneratedSystemBody Body { get; }
    public Color SwatchColor { get; }
    public string Name => Body.Name;
    public string PrimaryLine => Body.PrimaryLine;
    public string SecondaryLine => Body.SecondaryLine;
    public string BodyPortalCode => Body.BodyPortalCode;
    public bool IsDialedBody => Body.IsDialedBody;
    public bool HasRings => Body.HasRings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyButtonLabel))]
    public partial bool IsCopied { get; set; }

    public string CopyButtonLabel => IsCopied ? "Copied" : "Copy address";
}
