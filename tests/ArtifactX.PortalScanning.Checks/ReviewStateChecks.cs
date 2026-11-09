using ArtifactX.Maui.Services;
using ArtifactX.Maui.ViewModels;
using ArtifactX.PortalScanning;

internal static class ReviewStateChecks
{
    public static async Task RunAsync()
    {
        var fake = new FakeScanner();
        var vm = new PortalScannerViewModel(fake) { AddressInput = "123456789ABC" };
        const string candidate = "FEDCBA987654";
        fake.Result = new(candidate, candidate.Select(c => new GlyphMatch(c, 0.1, 0.5)).ToArray(), "Review");
        await vm.TakePhotoCommand.ExecuteAsync(null);
        Require(vm.AddressInput == "123456789ABC" && vm.HasReview && vm.ReviewInput == candidate,
            "A scan must stay separate until the player accepts it.");
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
            await vm.TakePhotoCommand.ExecuteAsync(null);
            Require(vm.AddressInput == candidate && !vm.HasReview && !vm.IsBusy, "Cancellation/failure changed the address.");
        }
        fake.Throw = true;
        await vm.TakePhotoCommand.ExecuteAsync(null);
        Require(vm.AddressInput == candidate && !vm.IsBusy, "An exception lost input or left the scanner busy.");
        fake.Throw = false;
        fake.WaitForCancellation = true;
        var pending = vm.TakePhotoCommand.ExecuteAsync(null);
        await vm.ChoosePhotoCommand.ExecuteAsync(null);
        Require(fake.ActiveCalls == 1, "Two media operations overlapped.");
        vm.CancelScanCommand.Execute(null);
        await pending;
        Require(!vm.IsBusy && vm.AddressInput == candidate, "Cancellation did not release the scanner.");

        vm.EnterManuallyCommand.Execute(null);
        vm.SelectSlotCommand.Execute(vm.Slots[4]);
        vm.TapGlyphCommand.Execute(vm.Keys[0]);
        Require(vm.ReviewInput == "FEDC0A987654" && vm.SelectedPosition == 6, "Manual correction changed the wrong position.");
        Console.WriteLine("PASS: review/accept/discard, incomplete input, failure/exception preservation, concurrency, cancellation, manual correction.");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class FakeScanner : IPortalPhotoScanService
    {
        public PortalScanResult? Result { get; set; }
        public bool Throw { get; set; }
        public bool WaitForCancellation { get; set; }
        public int ActiveCalls { get; private set; }
        public async Task<PortalScanResult?> ScanAsync(bool useCamera, CancellationToken cancellationToken)
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
