using ArtifactX.Maui.Services;
using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ExplorePages;

public partial class LivePortalScannerPage : ContentPage
{
    private readonly LivePortalScannerViewModel _viewModel;
    private readonly CameraPreviewSource _source;
    private CancellationTokenSource? _lifetime;
    private Task _scan = Task.CompletedTask;

    public LivePortalScannerPage(LivePortalScannerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _source = new CameraPreviewSource(Preview);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (Window is { } window)
        {
            window.Stopped += OnWindowStopped;
            window.Resumed += OnWindowResumed;
        }
        StartScan();
    }

    private void StartScan()
    {
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = new CancellationTokenSource();
        _scan = RunAfterPreviousAsync(_scan, _lifetime.Token);
    }

    private async Task RunAfterPreviousAsync(Task previous, CancellationToken token)
    {
        await previous;
        if (!token.IsCancellationRequested) await _viewModel.RunAsync(_source, token);
    }

    private void OnWindowStopped(object? sender, EventArgs args) => _lifetime?.Cancel();
    private void OnWindowResumed(object? sender, EventArgs args) => StartScan();

    protected override void OnDisappearing()
    {
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
        if (Window is { } window)
        {
            window.Stopped -= OnWindowStopped;
            window.Resumed -= OnWindowResumed;
        }
        base.OnDisappearing();
    }
}
