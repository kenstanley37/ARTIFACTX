using ArtifactX.Maui.ViewModels;
using System.Diagnostics;

namespace ArtifactX.Maui.Views.AppPages;

public partial class LoadingPage : ContentPage
{
    private const double AnimationDurationMs = 1950.0;
    private const double ProgressBarMaxWidth = 148.0;

    private IDispatcherTimer? _timer;
    private readonly Stopwatch _stopwatch = new();
    private bool _transitioned;

    public LoadingViewModel ViewModel { get; } = new();

    public LoadingPage()
    {
        InitializeComponent();
        BindingContext = ViewModel;
        ViewModel.Completed += OnCompleted;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartLogoAnimation();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTimer();
    }

    private void StartLogoAnimation()
    {
        _transitioned = false;
        ViewModel.LogoProgress = 0.0;
        ViewModel.TitleOpacity = 0.0;
        ViewModel.BarProgress = 0.0;

        _stopwatch.Restart();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnAnimationTick;
        _timer.Start();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        double elapsed = _stopwatch.Elapsed.TotalMilliseconds;
        double t = Math.Clamp(elapsed / AnimationDurationMs, 0.0, 1.0);

        // Smooth cubic ease-out for the logo stroke trace
        double easedLogo = 1.0 - Math.Pow(1.0 - Math.Clamp(t / 0.82, 0.0, 1.0), 2.4);
        ViewModel.LogoProgress = easedLogo;

        // Fade in "A R T I F A C T X" as the X-arms finish locking in
        ViewModel.TitleOpacity = Math.Clamp((t - 0.42) / 0.35, 0.0, 1.0);

        // Fill the slim progress bar smoothly across the full duration
        ViewModel.BarProgress = ProgressBarMaxWidth * t;

        if (t >= 1.0)
        {
            StopTimer();
            TransitionToHome();
        }
    }

    private void StopTimer()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnAnimationTick;
            _timer = null;
        }
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        StopTimer();
        TransitionToHome();
    }

    private void TransitionToHome()
    {
        if (_transitioned) return;
        _transitioned = true;

        if (Window is not null)
        {
            Window.Page = new AppShell();
        }
    }
}
