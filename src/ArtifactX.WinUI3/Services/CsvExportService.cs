using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// "Export CSV" for the Tools pages: asks where to save, writes what the page
/// is showing, and confirms on the button itself (it reads "Saved" for a few
/// seconds). UTF-8 with a byte-order mark and CRLF line ends so Excel opens
/// names like Vy'keen or "Nüsk" correctly; every field is quoted.
/// </summary>
public static class CsvExportService
{
    public static async Task ExportAsync(Button button, string suggestedName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedFileName = $"{suggestedName} {DateTime.Now:yyyy-MM-dd}",
        };
        picker.FileTypeChoices.Add("CSV (spreadsheet)", new List<string> { ".csv" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance));

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        object original = button.Content;
        try
        {
            var lines = new List<string> { Line(headers) };
            lines.AddRange(rows.Select(Line));
            string path = file.Path;
            await Task.Run(() => File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)));
            button.Content = $"Saved {lines.Count - 1:N0} rows";
            ToolTipService.SetToolTip(button, path);
        }
        catch (Exception ex)
        {
            button.Content = "Export failed";
            ToolTipService.SetToolTip(button, ex.Message);
        }

        await Task.Delay(3000);
        button.Content = original;
    }

    private static string Line(IReadOnlyList<string> fields) =>
        string.Join(",", fields.Select(f => "\"" + (f ?? "").Replace("\"", "\"\"") + "\""));
}
