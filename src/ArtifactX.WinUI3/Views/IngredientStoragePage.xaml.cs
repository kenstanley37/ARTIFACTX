using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.WinUI3.Services;
using System;

namespace ArtifactX.WinUI3.Views;

/// <summary>The cooking Ingredient Storage (Portable Grill / Nutrient Processor), 50 slots (2026-10-15) - see FoodStoreControl.</summary>
public sealed partial class IngredientStoragePage : Page
{
    public IngredientStoragePage()
    {
        InitializeComponent();
        Store.Init(coldStorage: false);
        Store.Changed += (_, _) => PageResetBtn.Visibility = Visibility.Visible;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
            SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
        };
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() =>
    {
        Store.Reload();
        PageResetBtn.Visibility = Store.HasLocalChanges ? Visibility.Visible : Visibility.Collapsed;
    });

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        Store.Revert();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
