using System.Diagnostics;
using ArtifactX.Core.NmsModels;
using ArtifactX.Maui.Services;
using ArtifactX.PortalScanning;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.Maui.ViewModels;

/// <summary>One cancellable scan per visible scanner page; never accepts a lone or ambiguous frame.</summary>
public sealed partial class LivePortalScannerViewModel(IPortalFrameAnalyzer analyzer, ISystemNavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsRunning { get; set; }
    [ObservableProperty]
    public partial string Status { get; set; } = "Point at the glyphs and galaxy name together.";
    [ObservableProperty]
    public partial string AddressPreview { get; set; } = "Searching for 12 glyphs";
    [ObservableProperty]
    public partial string GalaxyPreview { get; set; } = "Searching for galaxy name";

    public async Task RunAsync(IPortalPreviewSource source, CancellationToken cancellationToken)
    {
        if (IsRunning) return;
        IsRunning = true;
        AddressPreview = "Searching for 12 glyphs";
        GalaxyPreview = "Searching for galaxy name";
        Status = "Point at the glyphs and galaxy name together.";
        var stable = new LivePortalScanState();
        var clock = Stopwatch.StartNew();
        try
        {
            await analyzer.InitializeAsync(cancellationToken);
            await source.StartAsync(cancellationToken);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var frame = await source.ReadFrameAsync(cancellationToken);
                var capturedAt = clock.Elapsed;
                if (frame is null) { stable.Reset(); await Task.Delay(400, cancellationToken); continue; }
                var observation = await analyzer.AnalyzeAsync(frame, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                AddressPreview = observation.Glyphs.HasCandidate ? observation.Glyphs.Candidate! : "Searching for 12 glyphs";
                GalaxyPreview = observation.GalaxyNumber is int galaxy ? NmsGalaxyNames.ForNumber(galaxy) ?? "Galaxy not recognized" : "Searching for galaxy name";
                var accepted = stable.Observe(observation, capturedAt);
                Status = stable.MatchingFrames > 0 ? $"Hold steady: {stable.MatchingFrames} of 3 matching readings" :
                    "Keep all 12 glyphs and the galaxy name beneath them visible. Move closer if the text is too small.";
                if (accepted is not null)
                {
                    await source.StopAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    await navigation.OpenScanResultAsync(accepted.PortalCode, accepted.GalaxyNumber);
                    return;
                }
                // Backpressure: process one frame fully before sampling another; never queue stale frames.
                await Task.Delay(350, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (FileNotFoundException) { Status = "Scanner data is not installed in this build. Go back to enter the glyphs by hand."; }
        catch (CameraScanException ex) { Status = ex.Message; }
        catch (Exception ex) { Debug.WriteLine(ex); Status = "The camera scanner could not continue. Go back and try again, or enter the glyphs by hand."; }
        finally
        {
            try { await source.StopAsync(); }
            catch (Exception) { /* The camera may already be disconnected after navigating or backgrounding. */ }
            IsRunning = false;
        }
    }
}
