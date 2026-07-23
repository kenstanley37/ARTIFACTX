using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NMS.Core; // Directly hooks your core decryption/decompression engine[cite: 2, 8]
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace NMS.WinUI3.Views.InspectionPages;

public sealed partial class HGDecryptionPage : Page
{
    private string _cachedFullJsonText = string.Empty;

    public HGDecryptionPage()
    {
        InitializeComponent();
        InitializeWebView();
    }

    private async void InitializeWebView()
    {
        // Ensure the underlying Chromium architecture initializes safely
        await JsonWebView.EnsureCoreWebView2Async();
    }

    private async void SelectFileBtn_Click(object sender, RoutedEventArgs e)
    {
        FileOpenPicker filePicker = new FileOpenPicker();
        filePicker.FileTypeFilter.Add(".hg");
        filePicker.FileTypeFilter.Add(".json");
        filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var window = NMS.WinUI3.App.MainWindowInstance;
        if (window != null)
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);
        }

        StorageFile file = await filePicker.PickSingleFileAsync();
        if (file != null)
        {
            string selectedFilename = file.Name.ToLower().Trim();

            if (selectedFilename.StartsWith("mf_"))
            {
                ContentDialog manifestWarningDialog = new ContentDialog
                {
                    Title = "Invalid Save File Target",
                    Content = $"The file '{file.Name}' is a cloud manifest hash ledger.\n\nPlease select a primary slot save file (e.g., save.hg, save2.hg).",
                    CloseButtonText = "Understood",
                    XamlRoot = this.XamlRoot
                };
                await manifestWarningDialog.ShowAsync();
                return;
            }

            TargetFileTxt.Text = file.Name;
            SearchPanel.Visibility = Visibility.Collapsed;
            SearchQueryTxt.Text = string.Empty;
            LoadingRing.IsActive = true;

            try
            {
                // 1. Decompress and format entirely in a background memory worker thread[cite: 2, 8]
                string targetPath = file.Path;
                _cachedFullJsonText = await Task.Run(async () => await DecompressAndPrettifyAsync(targetPath));

                // 2. Wrap the text in our high-performance dark-themed HTML container
                string htmlDocument = BuildHtmlContainer(_cachedFullJsonText);

                // 3. Save the string to a temporary file to bypass WinRT parameter size limits
                string tempFolder = Path.GetTempPath();
                string tempFilePath = Path.Combine(tempFolder, "nms_inspector_preview.html");
                await File.WriteAllTextAsync(tempFilePath, htmlDocument, Encoding.UTF8);

                // 4. Ensure WebView is ready, then point it directly to the local cache URI
                await JsonWebView.EnsureCoreWebView2Async();
                JsonWebView.CoreWebView2.Navigate(new Uri(tempFilePath).AbsoluteUri);

                SearchPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                TargetFileTxt.Text = "Error Loading File";
                JsonWebView.NavigateToString($"<html><body style='background:#1e1e1e;color:#ff6b6b;font-family:monospace;'><h3>DECOMPRESSION BREAKDOWN</h3><pre>{ex.Message}\n\n{ex.StackTrace}</pre></body></html>");
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }
    }

    private async Task<string> DecompressAndPrettifyAsync(string filePath)
    {
        if (!File.Exists(filePath)) return "Error: File no longer exists on disk.";

        // Use core library decompression stream directly[cite: 2, 8]
        using Stream rawJsonStream = await SaveStreamProcessor.DecompressSaveToStreamAsync(filePath);

        using var ms = new MemoryStream();
        await rawJsonStream.CopyToAsync(ms);
        byte[] rawJsonBytes = ms.ToArray();

        if (rawJsonBytes.Length == 0) return "Empty decompressed stream payload buffer.";

        // Trim trailing padding identically to your system specification[cite: 1]
        int validLength = rawJsonBytes.Length;
        while (validLength > 0 && (rawJsonBytes[validLength - 1] == 0x00 || rawJsonBytes[validLength - 1] <= 0x20))
        {
            validLength--;
        }

        // Scrub leading BOM markers out of the parsing array text frame window
        int jsonStartOffset = 0;
        if (validLength >= 3 && rawJsonBytes[0] == 0xEF && rawJsonBytes[1] == 0xBB && rawJsonBytes[2] == 0xBF)
        {
            jsonStartOffset = 3;
        }

        string cleanJsonString = Encoding.UTF8.GetString(rawJsonBytes.AsSpan(jsonStartOffset, validLength - jsonStartOffset));

        try
        {
            using var doc = JsonDocument.Parse(cleanJsonString, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            using var outputStream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = true }))
            {
                doc.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(outputStream.ToArray());
        }
        catch (JsonException)
        {
            return cleanJsonString;
        }
    }

    private string BuildHtmlContainer(string rawJsonText)
    {
        // Safe string escape for HTML injection container
        string escapedJson = System.Web.HttpUtility.HtmlEncode(rawJsonText);

        // Optimized dark-themed text wrapper requiring zero custom JavaScript tracking loops
        return $@"
        <html>
        <head>
            <style>
                body {{ 
                    background-color: #1e1e1e; 
                    color: #d4d4d4; 
                    font-family: 'Consolas', 'Courier New', monospace; 
                    font-size: 14px; 
                    line-height: 1.5;
                    margin: 16px; 
                    padding: 0;
                }}
                pre {{ margin: 0; white-space: pre-wrap; word-wrap: break-word; }}
            </style>
        </head>
        <body>
            <pre id='code-container'>{escapedJson}</pre>
        </body>
        </html>";
    }

    private async void SearchQueryTxt_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (JsonWebView.CoreWebView2 == null) return;

        string query = SearchQueryTxt.Text;

        if (string.IsNullOrWhiteSpace(query))
        {
            // Clear highlights if query is empty
            await JsonWebView.ExecuteScriptAsync("window.find('', false, false, true);");
            return;
        }

        string safeQuery = query.Replace("\\", "\\\\").Replace("'", "\\'");

        // Find first instance initially (backwards = false, wrapAround = true)
        await JsonWebView.ExecuteScriptAsync($"window.find('{safeQuery}', false, false, true);");
    }

    private void SearchQueryTxt_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            TriggerSearchNavigation(backwards: false);
        }
    }

    // 📍 ADDED: Next button handler
    private void NextSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        TriggerSearchNavigation(backwards: false);
    }

    // 📍 ADDED: Previous button handler
    private void PrevSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        TriggerSearchNavigation(backwards: true);
    }

    // 📍 Core Navigation Engine helper function
    private async void TriggerSearchNavigation(bool backwards)
    {
        if (JsonWebView.CoreWebView2 == null) return;

        string query = SearchQueryTxt.Text;
        if (string.IsNullOrWhiteSpace(query)) return;

        string safeQuery = query.Replace("\\", "\\\\").Replace("'", "\\'");
        string isBackwardsFlag = backwards ? "true" : "false";

        // Calling window.find again with the same word, but without resetting wrapAround, 
        // forces Chromium's native engine to advance directly to the next or previous matching position.
        await JsonWebView.ExecuteScriptAsync($"window.find('{safeQuery}', false, {isBackwardsFlag}, true);");
    }

    private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        SearchQueryTxt.Text = string.Empty;
    }
}