using ArtifactX.Core.Guides;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Tools > Guides: the player's own list of creator videos (GuidesService,
/// user.db), tagged to catalog items so other pages can show "your guides
/// about this". Links are checked by VideoLink (YouTube, Twitch, TikTok only)
/// and open in the browser - nothing plays inside the app.
/// </summary>
public sealed partial class GuidesPage : Page
{
    private Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);

    // Form state
    private string? _editingId;
    private string _editingCreatedAt = "";
    private readonly List<string> _formItems = new();
    private VideoLink.Result? _link;
    private string _thumbnail = "";
    private string _autoTitle = "";
    private string _autoCreator = "";
    private int _fetchVersion;
    private bool _formOpen;
    private bool _ready;

    public GuidesPage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _itemNames = await CatalogService.GetItemNamesAsync();
        await CatalogService.WarmCacheAsync(GuidesService.All.SelectMany(g => g.ItemIds));

        FormTopicBox.Items.Add(new ComboBoxItem { Content = "-", Tag = "" });
        foreach (var topic in GuidesService.Topics) FormTopicBox.Items.Add(new ComboBoxItem { Content = topic, Tag = topic });
        FormTopicBox.SelectedIndex = 0;

        foreach (var (label, tag) in new[] { ("Any platform", ""), (VideoLink.YouTube, VideoLink.YouTube), (VideoLink.Twitch, VideoLink.Twitch), (VideoLink.TikTok, VideoLink.TikTok) })
            PlatformFilterBox.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        PlatformFilterBox.SelectedIndex = 0;
        foreach (var label in new[] { "Newest first", "Title (A to Z)", "Creator (A to Z)" }) SortBox.Items.Add(label);
        SortBox.SelectedIndex = 0;

        _ready = true;
        RefreshTopicFilter();
        Apply();
    }

    // ---------------------------------------------------------------- list

    private void RefreshTopicFilter()
    {
        string previous = (TopicFilterBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var guides = GuidesService.All;
        TopicFilterBox.Items.Clear();
        TopicFilterBox.Items.Add(new ComboBoxItem { Content = $"All topics ({guides.Count})", Tag = "" });
        foreach (var topic in GuidesService.Topics)
        {
            int count = guides.Count(g => g.Topic == topic);
            if (count > 0) TopicFilterBox.Items.Add(new ComboBoxItem { Content = $"{topic} ({count})", Tag = topic });
        }
        TopicFilterBox.SelectedItem = TopicFilterBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == previous) ?? TopicFilterBox.Items[0];
    }

    private void Apply()
    {
        if (!_ready) return;
        var guides = GuidesService.All;
        string text = SearchBox.Text.Trim();
        string topic = (TopicFilterBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        string platform = (PlatformFilterBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

        bool Matches(Guide g) => text.Length == 0
            || g.Title.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || g.Creator.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || g.Notes.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || g.ItemIds.Any(i => NameOf(i).Contains(text, StringComparison.CurrentCultureIgnoreCase));

        var filtered = guides.Where(g => topic.Length == 0 || g.Topic == topic)
            .Where(g => platform.Length == 0 || g.Platform == platform)
            .Where(Matches);
        var ordered = SortBox.SelectedIndex switch
        {
            1 => filtered.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase),
            2 => filtered.OrderBy(g => g.Creator, StringComparer.CurrentCultureIgnoreCase),
            _ => filtered.OrderByDescending(g => g.CreatedAt, StringComparer.Ordinal),
        };

        var cards = ordered.Select(ToCard).ToList();
        GuidesList.ItemsSource = cards;
        CountTxt.Text = $"{cards.Count} of {guides.Count} videos";
        EmptyTxt.Visibility = guides.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string NameOf(string id) => _itemNames.TryGetValue(id, out var n) ? n : CatalogService.TryGet(id)?.DisplayName ?? id;

    private GuideCardViewModel ToCard(Guide g)
    {
        ImageSource? thumb = null;
        if (Uri.TryCreate(g.ThumbnailUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            thumb = new BitmapImage(uri) { DecodePixelWidth = 256 };
        return new GuideCardViewModel
        {
            Id = g.Id,
            Url = g.Url,
            Title = g.Title.Length > 0 ? g.Title : g.Url,
            Creator = g.Creator,
            Platform = g.Platform.ToUpperInvariant(),
            PlatformBrush = new SolidColorBrush(PlatformColour(g.Platform)),
            Thumbnail = thumb,
            ThumbnailVisibility = thumb is null ? Visibility.Collapsed : Visibility.Visible,
            Topic = g.Topic.ToUpperInvariant(),
            TopicVisibility = g.Topic.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
            Items = g.ItemIds.Select(id => new FindItemChip { Label = NameOf(id), Icon = CatalogService.TryGet(id)?.Icon }).ToList(),
            ItemsVisibility = g.ItemIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
            Notes = g.Notes,
            NotesVisibility = g.Notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
    }

    // Platform brand colours for the badge - fixed, not themed.
    private static Windows.UI.Color PlatformColour(string platform) => platform switch
    {
        VideoLink.YouTube => Windows.UI.Color.FromArgb(255, 204, 0, 0),
        VideoLink.Twitch => Windows.UI.Color.FromArgb(255, 145, 70, 255),
        _ => Windows.UI.Color.FromArgb(255, 40, 40, 48),
    };

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => Apply();
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => Apply();

    private async void GuidesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is GuideCardViewModel card) await OpenAsync(card.Url);
    }

    /// <summary>Opens a saved link in the default browser - re-checked first, so
    /// only a YouTube/Twitch/TikTok https link is ever launched.</summary>
    public static async Task OpenAsync(string url)
    {
        var link = VideoLink.Parse(url);
        if (link.Ok) await Launcher.LaunchUriAsync(new Uri(link.Url));
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var guide = GuidesService.All.FirstOrDefault(g => g.Id == id);
        var confirm = new ContentDialog
        {
            Title = "Remove this video?",
            Content = guide?.Title is { Length: > 0 } t ? $"\"{t}\" will be removed from your list." : "This video will be removed from your list.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        GuidesService.Delete(id);
        if (_editingId == id) ResetForm();
        RefreshTopicFilter();
        Apply();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var g = GuidesService.All.FirstOrDefault(x => x.Id == id);
        if (g is null) return;

        _editingId = g.Id;
        _editingCreatedAt = g.CreatedAt;
        _thumbnail = g.ThumbnailUrl;
        _autoTitle = g.Title;
        _autoCreator = g.Creator;
        FormTitleBox.Text = g.Title;
        FormCreatorBox.Text = g.Creator;
        FormLinkBox.Text = g.Url;
        SelectTopic(g.Topic);
        FormNotesBox.Text = g.Notes;
        _formItems.Clear();
        _formItems.AddRange(g.ItemIds);
        RenderFormItems();
        FormTitleTxt.Text = "EDIT VIDEO";
        FormSaveBtn.Content = "Save changes";
        SetFormOpen(true);
    }

    // ---------------------------------------------------------------- form

    private void FormToggle_Click(object sender, RoutedEventArgs e) => SetFormOpen(!_formOpen);

    private void SetFormOpen(bool open)
    {
        _formOpen = open;
        FormPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        FormToggleIcon.Glyph = open ? "" : "";
    }

    private async void FormLink_TextChanged(object sender, TextChangedEventArgs e)
    {
        int version = ++_fetchVersion;
        string input = FormLinkBox.Text.Trim();
        if (input.Length == 0)
        {
            _link = null;
            FormLinkStatusTxt.Text = "";
            return;
        }
        _link = VideoLink.Parse(input);
        if (!_link.Ok)
        {
            FormLinkStatusTxt.Text = _link.Error;
            return;
        }

        var duplicate = GuidesService.All.FirstOrDefault(g => g.Url == _link.Url && g.Id != _editingId);
        if (duplicate is not null)
        {
            FormLinkStatusTxt.Text = $"Already in your list as \"{duplicate.Title}\".";
            return;
        }

        if (_link.Platform == VideoLink.Twitch)
        {
            FormLinkStatusTxt.Text = "Twitch video - type its title and creator below (Twitch doesn't share them without an account).";
            return;
        }

        FormLinkStatusTxt.Text = $"{_link.Platform} video - getting its details...";
        await Task.Delay(400); // wait for the paste/typing to settle
        if (version != _fetchVersion) return;
        var details = await GuidesService.FetchDetailsAsync(_link.Platform, _link.Url);
        if (version != _fetchVersion) return;
        if (details is null)
        {
            FormLinkStatusTxt.Text = $"{_link.Platform} video - couldn't get its details (offline?). You can type them below.";
            return;
        }
        // Fill in only what the player hasn't typed themselves.
        if (FormTitleBox.Text.Trim().Length == 0 || FormTitleBox.Text == _autoTitle) FormTitleBox.Text = _autoTitle = details.Title;
        if (FormCreatorBox.Text.Trim().Length == 0 || FormCreatorBox.Text == _autoCreator) FormCreatorBox.Text = _autoCreator = details.Creator;
        _thumbnail = details.ThumbnailUrl;
        FormLinkStatusTxt.Text = $"{_link.Platform} video found.";
    }

    private void FormItemSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        string text = sender.Text.Trim();
        sender.ItemsSource = text.Length < 2 ? null : _itemNames.Values
            .Where(n => n.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(n => n.StartsWith(text, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1).ThenBy(n => n)
            .Take(12).ToList();
    }

    private async void FormItemSearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        string name = args.SelectedItem as string ?? "";
        string? id = _itemNames.FirstOrDefault(kv => string.Equals(kv.Value, name, StringComparison.CurrentCultureIgnoreCase)).Key;
        sender.Text = "";
        if (id is null || _formItems.Contains(id, StringComparer.OrdinalIgnoreCase)) return;
        _formItems.Add(id);
        RenderFormItems();
        if (CatalogService.TryGet(id)?.Icon is null)
        {
            await CatalogService.WarmCacheAsync(new[] { id });
            RenderFormItems();
        }
    }

    private void RenderFormItems()
    {
        FormItemChips.Children.Clear();
        foreach (var id in _formItems.ToList())
        {
            var chip = new Button { Padding = new Thickness(6, 2, 6, 2) };
            ToolTipService.SetToolTip(chip, "Remove");
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new Image { Source = CatalogService.TryGet(id)?.Icon, Width = 18, Height = 18 });
            row.Children.Add(new TextBlock { Text = NameOf(id), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new FontIcon { Glyph = "", FontSize = 10 });
            chip.Content = row;
            chip.Click += (_, _) =>
            {
                _formItems.Remove(id);
                RenderFormItems();
            };
            FormItemChips.Children.Add(chip);
        }
    }

    private void FormSave_Click(object sender, RoutedEventArgs e)
    {
        var link = VideoLink.Parse(FormLinkBox.Text);
        if (!link.Ok)
        {
            FormErrorTxt.Text = link.Error;
            return;
        }
        if (GuidesService.All.FirstOrDefault(g => g.Url == link.Url && g.Id != _editingId) is { } duplicate)
        {
            FormErrorTxt.Text = $"Already in your list as \"{duplicate.Title}\".";
            return;
        }
        if (FormTitleBox.Text.Trim().Length == 0)
        {
            FormErrorTxt.Text = "Give it a title.";
            return;
        }

        GuidesService.Save(new Guide
        {
            Id = _editingId ?? Guid.NewGuid().ToString("N"),
            CreatedAt = _editingCreatedAt,
            Url = link.Url,
            Platform = link.Platform,
            Title = FormTitleBox.Text.Trim(),
            Creator = FormCreatorBox.Text.Trim(),
            ThumbnailUrl = _thumbnail,
            Topic = (FormTopicBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
            ItemIds = _formItems.ToList(),
            Notes = FormNotesBox.Text.Trim(),
        });

        ResetForm();
        RefreshTopicFilter();
        Apply();
    }

    private void FormCancel_Click(object sender, RoutedEventArgs e)
    {
        ResetForm();
        SetFormOpen(false);
    }

    private void ResetForm()
    {
        _fetchVersion++;
        _editingId = null;
        _editingCreatedAt = "";
        _thumbnail = "";
        _autoTitle = "";
        _autoCreator = "";
        _link = null;
        FormLinkBox.Text = "";
        FormTitleBox.Text = "";
        FormCreatorBox.Text = "";
        FormNotesBox.Text = "";
        FormErrorTxt.Text = "";
        FormLinkStatusTxt.Text = "";
        FormTopicBox.SelectedIndex = 0;
        _formItems.Clear();
        RenderFormItems();
        FormTitleTxt.Text = "ADD A VIDEO";
        FormSaveBtn.Content = "Save video";
    }

    private void SelectTopic(string topic) =>
        FormTopicBox.SelectedItem = FormTopicBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == topic) ?? FormTopicBox.Items.FirstOrDefault();
}
