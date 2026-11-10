using ArtifactX.Core.NmsModels;
using ArtifactX.Maui.Services;
using ArtifactX.Maui.ViewModels;
using ArtifactX.PortalScanning;

internal static class LiveScanChecks
{
    public static async Task RunAsync()
    {
        var names = new GalaxyNameMatcher(NmsGalaxyInfo.AllGalaxies.ToDictionary(g => g.Number, g => g.Name));
        Require(names.Match(["Nudquathsenfe Galaxy"]) == 110, "Owner's galaxy name did not match galaxy 110.");
        Require(names.Match(["nudquathsenfe  galaxy"]) == 110, "OCR whitespace/case normalization failed.");
        Require(names.Match(["Hilbert Dimension Galaxy"]) == 2, "Multiword galaxy did not match.");
        Require(names.Match(["Euclid Galaxy", "Calypso Galaxy"]) is null, "Conflicting galaxies were accepted.");
        Require(names.Match(["Euclidish Galaxy"]) is null && names.Match(["Unrecognized Galaxy"]) is null, "A partial or unknown name was guessed.");

        var target = PortalScanTarget.FromRegion(new(20, 10, 80, 30), 200, 100);
        Require(target is { X: 0.1, Y: 0.1, Width: 0.3, Height: 0.2 }, "Preview bounds did not normalize correctly.");
        Require(PortalScanTarget.FromRegion(new(-20, -10, 220, 110), 200, 100) is { X: 0, Y: 0, Width: 1, Height: 1 },
            "Preview bounds escaped the camera surface.");
        Require(PortalScanTarget.FromRegion(new(80, 30, 20, 10), 200, 100) is null &&
            PortalScanTarget.FromRegion(new(20, 10, 80, 30), 0, 100) is null, "Invalid preview bounds were accepted.");

        var a = Observation("11FA016452DB", 110);
        var b = Observation("21FA016452DB", 110);
        var state = new LivePortalScanState();
        Require(state.Observe(a, TimeSpan.FromSeconds(1)) is null, "One frame completed a scan.");
        Require(state.Observe(a, TimeSpan.FromSeconds(2)) is null, "Two frames completed a scan.");
        Require(state.Observe(a, TimeSpan.FromSeconds(3))?.GalaxyNumber == 110, "Three matching pairs were not accepted.");
        state.Reset();
        state.Observe(a, TimeSpan.FromSeconds(1));
        state.Observe(b, TimeSpan.FromSeconds(2));
        Require(state.MatchingFrames == 1, "Changed address did not reset agreement.");
        state.Observe(b with { GalaxyNumber = 1 }, TimeSpan.FromSeconds(3));
        Require(state.MatchingFrames == 1, "Changed galaxy did not reset agreement.");
        state.Observe(a with { GalaxyNumber = null }, TimeSpan.FromSeconds(4));
        Require(state.MatchingFrames == 0, "Missing galaxy retained stale agreement.");
        var uncertain = a with { Glyphs = a.Glyphs with { Glyphs = a.Glyphs.Glyphs.Select((g, i) => i == 0 ? g with { Margin = 0.01 } : g).ToArray() } };
        var twoUncertain = a with { Glyphs = a.Glyphs with { Glyphs = a.Glyphs.Glyphs.Select((g, i) => i < 2 ? g with { Margin = 0.01 } : g).ToArray() } };
        state.Reset();
        state.Observe(twoUncertain, TimeSpan.FromSeconds(1));
        Require(state.MatchingFrames == 0, "A strip with two uncertain symbols advanced the automatic scan.");
        state.Observe(a, TimeSpan.FromSeconds(2));
        state.Observe(a, TimeSpan.FromSeconds(3));
        Require(state.Observe(uncertain, TimeSpan.FromSeconds(4)) is not null,
            "Three agreeing frames with every position confident twice were not accepted.");
        state.Reset();
        state.Observe(uncertain, TimeSpan.FromSeconds(1));
        state.Observe(uncertain, TimeSpan.FromSeconds(2));
        Require(state.Observe(a, TimeSpan.FromSeconds(3)) is null && state.MatchingFrames == 3,
            "A position read confidently only once was accepted.");
        Require(state.Observe(a, TimeSpan.FromSeconds(4)) is not null,
            "A fourth agreeing frame did not complete the uncertain position.");
        state.Reset();
        state.Observe(uncertain, TimeSpan.FromSeconds(1));
        state.Observe(uncertain, TimeSpan.FromSeconds(2));
        state.Observe(uncertain, TimeSpan.FromSeconds(3));
        Require(state.Observe(uncertain, TimeSpan.FromSeconds(4)) is null, "A never-confident position was accepted.");
        state.Reset();
        state.Observe(a, TimeSpan.FromSeconds(6));
        state.Observe(a, TimeSpan.FromSeconds(6));
        Require(state.MatchingFrames == 1, "The same frame timestamp was counted twice.");
        state.Observe(a, TimeSpan.FromSeconds(20));
        Require(state.MatchingFrames == 1, "A long frame gap retained agreement.");

        state.Reset();
        state.Observe(a, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(16));
        state.Observe(a, TimeSpan.FromSeconds(17), TimeSpan.FromSeconds(32));
        Require(state.Observe(a, TimeSpan.FromSeconds(33), TimeSpan.FromSeconds(48)) is not null,
            "Slow frame analysis incorrectly reset consecutive agreement.");
        Require(state.Observe(a, TimeSpan.FromSeconds(65), TimeSpan.FromSeconds(80)) is null && state.MatchingFrames == 1,
            "An actual capture idle gap did not reset agreement.");

        var preview = new FakePreview();
        var navigation = new FakeNavigation(preview);
        var analyzer = new FakeAnalyzer(a);
        var vm = new LivePortalScannerViewModel(analyzer, navigation);
        await vm.RunAsync(preview, CancellationToken.None);
        Require(navigation.Address == "11FA016452DB" && navigation.Galaxy == 110 && navigation.Calls == 1,
            "Live scan lost the address/galaxy or navigated repeatedly.");
        Require(analyzer.Calls == 3 && preview.DisposedFrames == 3 && !vm.IsRunning && vm.IsLocked, "Frames leaked or did not stop at agreement.");

        using var cancel = new CancellationTokenSource();
        preview = new FakePreview();
        navigation = new FakeNavigation(preview);
        analyzer = new FakeAnalyzer(a) { BeforeReturn = count => { if (count == 3) cancel.Cancel(); } };
        vm = new LivePortalScannerViewModel(analyzer, navigation);
        await vm.RunAsync(preview, cancel.Token);
        Require(navigation.Calls == 0 && preview.Stopped && !vm.IsRunning && preview.DisposedFrames == 3,
            "Late analysis navigated after cancellation or leaked camera frames.");

        preview = new FakePreview();
        navigation = new FakeNavigation(preview);
        analyzer = new FakeAnalyzer(a) { FailInitialize = true };
        vm = new LivePortalScannerViewModel(analyzer, navigation);
        await vm.RunAsync(preview, CancellationToken.None);
        Require(!preview.Started && !vm.IsRunning && navigation.Calls == 0 && vm.Status.Contains("not installed"),
            "Missing scanner data started the camera or left it busy.");
        Console.WriteLine("PASS: galaxy text matching, three-frame agreement, uncertainty/conflict/gap resets, navigation, cancellation and preview disposal.");
    }

    private static LivePortalObservation Observation(string code, int galaxy) => new(
        new(code, code.Select(c => new GlyphMatch(c, 0.05, 0.8)).ToArray(), ""), galaxy);
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class FakeAnalyzer(LivePortalObservation observation) : IPortalFrameAnalyzer
    {
        public int Calls { get; private set; }
        public bool FailInitialize { get; init; }
        public Action<int>? BeforeReturn { get; init; }
        public Task InitializeAsync(CancellationToken token) => FailInitialize ? Task.FromException(new FileNotFoundException()) : Task.CompletedTask;
        public Task<LivePortalObservation> AnalyzeAsync(Stream frame, CancellationToken token)
        {
            Calls++;
            BeforeReturn?.Invoke(Calls);
            return Task.FromResult(observation);
        }
    }
    private sealed class FakePreview : IPortalPreviewSource
    {
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }
        public int DisposedFrames { get; private set; }
        public Task StartAsync(CancellationToken token) { Started = true; return Task.CompletedTask; }
        public Task StopAsync() { Stopped = true; return Task.CompletedTask; }
        public Task<Stream?> ReadFrameAsync(CancellationToken token) => Task.FromResult<Stream?>(new OwnedFrame(() => DisposedFrames++));
    }
    private sealed class OwnedFrame(Action disposed) : MemoryStream
    {
        protected override void Dispose(bool disposing) { if (disposing) disposed(); base.Dispose(disposing); }
    }
    private sealed class FakeNavigation(FakePreview preview) : ISystemNavigationService
    {
        public string? Address { get; private set; }
        public int Galaxy { get; private set; }
        public int Calls { get; private set; }
        public Task StartScanAsync() => Task.CompletedTask;
        public Task OpenAsync(string code, int galaxy) => OpenScanResultAsync(code, galaxy);
        public Task OpenScanResultAsync(string code, int galaxy)
        {
            Require(preview.Stopped, "Navigation occurred before releasing the camera.");
            Address = code;
            Galaxy = galaxy;
            Calls++;
            return Task.CompletedTask;
        }
    }
}
