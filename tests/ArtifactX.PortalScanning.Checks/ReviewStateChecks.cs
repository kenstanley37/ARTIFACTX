using ArtifactX.Maui.Services;
using ArtifactX.Maui.ViewModels;
using ArtifactX.PortalScanning;

internal static class ReviewStateChecks
{
    public static async Task RunAsync()
    {
        var fake = new FakeScanner();
        var navigation = new FakeNavigation();
        var vm = new PortalScannerViewModel(fake, navigation) { AddressInput = "123456789ABC" };
        const string candidate = "FEDCBA987654";
        fake.Result = new(candidate, candidate.Select(c => new GlyphMatch(c, 0.1, 0.5)).ToArray(), "Review");
        await vm.ChoosePhotoCommand.ExecuteAsync(null);
        Require(vm.AddressInput == "123456789ABC" && vm.HasReview && vm.ReviewInput == candidate,
            "A scan must stay separate until the player accepts it.");
        Require(!vm.IsKeypadOpen, "A scan review should show the address before opening correction keys.");
        vm.DiscardReviewCommand.Execute(null);
        Require(vm.AddressInput == "123456789ABC" && !vm.HasReview, "Discard changed the accepted address.");

        await vm.ChoosePhotoCommand.ExecuteAsync(null);
        vm.ReviewInput = "FEDCBA98765_";
        vm.AcceptReviewCommand.Execute(null);
        Require(vm.HasReview && vm.AddressInput == "123456789ABC", "An incomplete review was accepted.");
        vm.ReviewInput = candidate;
        vm.AcceptReviewCommand.Execute(null);
        Require(!vm.HasReview && vm.AddressInput == candidate, "A reviewed address was not accepted.");

        foreach (var result in new PortalScanResult?[] { null, new(null, [], "Unreadable") })
        {
            fake.Result = result;
            await vm.ChoosePhotoCommand.ExecuteAsync(null);
            Require(vm.AddressInput == candidate && !vm.HasReview && !vm.IsBusy, "Cancellation/failure changed the address.");
        }
        fake.Throw = true;
        await vm.ChoosePhotoCommand.ExecuteAsync(null);
        Require(vm.AddressInput == candidate && !vm.IsBusy, "An exception lost input or left the scanner busy.");
        fake.Throw = false;
        fake.WaitForCancellation = true;
        var pending = vm.ChoosePhotoCommand.ExecuteAsync(null);
        await vm.ChoosePhotoCommand.ExecuteAsync(null);
        Require(fake.ActiveCalls == 1, "Two media operations overlapped.");
        vm.CancelScanCommand.Execute(null);
        await pending;
        Require(!vm.IsBusy && vm.AddressInput == candidate, "Cancellation did not release the scanner.");

        vm.EnterManuallyCommand.Execute(null);
        vm.SelectSlotCommand.Execute(vm.Slots[4]);
        Require(vm.IsKeypadOpen && vm.Slots[4].IsSelected, "Selecting a position did not open correction keys.");
        vm.TapGlyphCommand.Execute(vm.Keys[0]);
        Require(vm.ReviewInput == "FEDC0A987654" && vm.SelectedPosition == 6, "Manual correction changed the wrong position.");
        vm.CloseKeypadCommand.Execute(null);
        Require(!vm.IsKeypadOpen && vm.HasReview && vm.ReviewInput == "FEDC0A987654", "Closing correction keys discarded the review.");
        vm.AddressInput = "invalid";
        await vm.FindSystemCommand.ExecuteAsync(null);
        Require(navigation.Address is null && !vm.IsBusy, "Invalid input navigated.");
        vm.AddressInput = "11FA016452DB";
        vm.SelectedGalaxy = vm.Galaxies[1];
        await vm.FindSystemCommand.ExecuteAsync(null);
        Require(navigation.Address == "11FA016452DB" && navigation.Galaxy == 2 && !vm.IsBusy,
            "Navigation lost the accepted address or galaxy.");
        var readout = ArtifactX.Core.Universe.PortalSystemReadout.Generate("11FA016452DB", 1);
        RecentSystemsService.Record(readout);
        RecentSystemsService.Record(ArtifactX.Core.Universe.PortalSystemReadout.Generate("21FA016452DB", 1));
        Require(RecentSystemsService.Items.Count == 1 && RecentSystemsService.Items[0].PortalCode == "21FA016452DB",
            "Different bodies duplicated the same recent system.");
        RecentSystemsService.Record(ArtifactX.Core.Universe.PortalSystemReadout.Generate("11FA016452DB", 2));
        Require(RecentSystemsService.Items.Count == 2 && RecentSystemsService.Items[0].GalaxyNumber == 2,
            "Recent systems merged different galaxies or lost ordering.");
        for (int i = 0; i < 12; i++)
            RecentSystemsService.Record(ArtifactX.Core.Universe.PortalSystemReadout.Generate($"11FA01645{i:X3}", 1));
        Require(RecentSystemsService.Items.Count == 10, "Recent systems did not enforce the history limit.");
        Console.WriteLine("PASS: navigation validation, galaxy handoff, recent system identity and history limit.");
        Console.WriteLine("PASS: review/accept/discard, incomplete input, failure/exception preservation, concurrency, cancellation, manual correction.");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class FakeNavigation : ISystemNavigationService
    {
        public string? Address { get; private set; }
        public int Galaxy { get; private set; }
        public Task StartScanAsync() => Task.CompletedTask;
        public Task OpenScanResultAsync(string portalCode, int galaxyNumber) => OpenAsync(portalCode, galaxyNumber);
        public Task OpenAsync(string portalCode, int galaxyNumber)
        {
            Address = portalCode;
            Galaxy = galaxyNumber;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeScanner : IPortalPhotoScanService
    {
        public PortalScanResult? Result { get; set; }
        public bool Throw { get; set; }
        public bool WaitForCancellation { get; set; }
        public int ActiveCalls { get; private set; }
        public async Task<PortalScanResult?> ScanAsync(CancellationToken cancellationToken)
        {
            ActiveCalls++;
            try
            {
                if (Throw) throw new IOException("Synthetic read failure.");
                if (WaitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
                return Result;
            }
            finally { ActiveCalls--; }
        }
    }
}
