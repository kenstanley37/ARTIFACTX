#if IOS || MACCATALYST
using Foundation;
using Vision;
using ImageIO;

namespace ArtifactX.Maui.Services;

public sealed class AppleGalaxyTextReader : IGalaxyTextReader
{
    public Task<IReadOnlyList<string>> ReadLinesAsync(byte[] image, CancellationToken cancellationToken) => Task.Run<IReadOnlyList<string>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var data = NSData.FromArray(image);
        using var handler = new VNImageRequestHandler(data, CGImagePropertyOrientation.Up, new VNImageOptions());
        using var request = new VNRecognizeTextRequest(null) { RecognitionLevel = VNRequestTextRecognitionLevel.Accurate, UsesLanguageCorrection = false };
        request.RecognitionLanguages = ["en-US"];
        if (!handler.Perform([request], out var error))
            throw new InvalidOperationException(error?.LocalizedDescription ?? "Galaxy text could not be read.");
        cancellationToken.ThrowIfCancellationRequested();
        return request.GetResults<VNRecognizedTextObservation>()?.Select(o => o.TopCandidates(1).FirstOrDefault()?.String ?? "").ToArray() ?? [];
    }, cancellationToken);
}
#endif
