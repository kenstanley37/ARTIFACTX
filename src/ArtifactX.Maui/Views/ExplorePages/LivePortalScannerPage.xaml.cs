using ArtifactX.Maui.Services;
using ArtifactX.Maui.ViewModels;

namespace ArtifactX.Maui.Views.ExplorePages;

public partial class LivePortalScannerPage : ContentPage
{
    private readonly LivePortalScannerViewModel _viewModel;
    private readonly CameraPreviewSource _source;
    private CancellationTokenSource? _lifetime;
    private Task _scan = Task.CompletedTask;
    private Window? _observedWindow;
    private bool _visible;

    public LivePortalScannerPage(LivePortalScannerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _source = new CameraPreviewSource(Preview);
        Loaded += OnLoaded;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        if (IsLoaded) AttachAndStart();
    }

    private void OnLoaded(object? sender, EventArgs args)
    {
        if (_visible) AttachAndStart();
    }

    private void AttachAndStart()
    {
        // Shell can raise Appearing before the page has a Window. Loaded has the native camera handler.
        if (_observedWindow is null && Window is { } window)
        {
            _observedWindow = window;
            window.Stopped += OnWindowStopped;
            window.Activated += OnWindowActivated;
        }
        if (_lifetime is null || _lifetime.IsCancellationRequested) StartScan();
    }

    private void StartScan()
    {
        PortalScanDiagnostics.Write("Starting visible scanner session");
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

    private void OnWindowStopped(object? sender, EventArgs args)
    {
        PortalScanDiagnostics.Write("Window stopped; cancelling scanner");
        _lifetime?.Cancel();
    }

    private void OnWindowActivated(object? sender, EventArgs args)
    {
        if (_visible && (_lifetime is null || _lifetime.IsCancellationRequested)) StartScan();
    }

    protected override void OnDisappearing()
    {
        _visible = false;
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
        if (_observedWindow is { } window)
        {
            window.Stopped -= OnWindowStopped;
            window.Activated -= OnWindowActivated;
            _observedWindow = null;
        }
        base.OnDisappearing();
    }
}
