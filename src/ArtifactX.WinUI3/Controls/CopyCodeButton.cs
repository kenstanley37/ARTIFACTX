using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace ArtifactX.WinUI3.Controls;

/// <summary>"Copy" beside a portal address: puts the 12-digit code on the clipboard.
/// The code shows as its tooltip.</summary>
public sealed class CopyCodeButton : Button
{
    public static readonly DependencyProperty CodeProperty =
        DependencyProperty.Register(nameof(Code), typeof(string), typeof(CopyCodeButton), new PropertyMetadata("", OnCodeChanged));

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public CopyCodeButton()
    {
        Content = "Copy";
        Padding = new Thickness(10, 3, 10, 3);
        VerticalAlignment = VerticalAlignment.Center;
        IsEnabled = false;
        Click += (_, _) =>
        {
            var package = new DataPackage();
            package.SetText(Code);
            Clipboard.SetContent(package);
        };
    }

    private static void OnCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (CopyCodeButton)d;
        string code = e.NewValue as string ?? "";
        button.IsEnabled = code.Length > 0;
        ToolTipService.SetToolTip(button, code.Length > 0 ? code : null);
        AutomationProperties.SetName(button, code.Length > 0 ? $"Copy portal code {code}" : "Copy portal code");
    }
}
