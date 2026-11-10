using System.Diagnostics;
using System.Text.Json;
using ArtifactX.PortalScanning;
using SkiaSharp;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

await ReviewStateChecks.RunAsync();

byte[] speckles = new byte[30];
foreach (int i in new[] { 7, 8, 9, 10, 11, 12 }) speckles[i] = 1;
ScreenPatternSuppression.RemoveSpeckles(speckles, 10, 3);
Require(speckles.All(p => p == 0), "Disconnected edge specks wrapped across image rows.");
byte[] diagonal = new byte[36];
for (int i = 0; i < 6; i++) diagonal[i * 7] = 1;
ScreenPatternSuppression.RemoveSpeckles(diagonal, 6, 6);
Require(diagonal.Count(p => p == 1) == 6, "Thin diagonal glyph strokes were removed.");
try { ScreenPatternSuppression.RemoveSpeckles([], 0, 0); throw new Exception("Accepted invalid mask dimensions."); }
catch (ArgumentException) { }
Console.WriteLine("PASS: isolated screen specks, row boundaries, diagonal stroke preservation and invalid dimensions.");

var cleanMatches = Enumerable.Range(0, 12).Select(_ => new GlyphMatch('0', 0.05, 0.7)).ToArray();
Require(PortalScanQuality.CanReview(cleanMatches), "A clean strip was rejected.");
var obscuredMatches = cleanMatches.ToArray();
obscuredMatches[4] = new('C', 0.18, 0.3);
Require(!PortalScanQuality.CanReview(obscuredMatches), "An isolated damaged glyph was accepted despite much cleaner neighbours.");
var ambiguousMatches = cleanMatches.ToArray();
ambiguousMatches[4] = new('F', 0.1, 0.15); ambiguousMatches[11] = new('A', 0.1, 0.15);
Require(!PortalScanQuality.CanReview(ambiguousMatches), "A strip with multiple ambiguous symbols was accepted.");
ambiguousMatches[11] = cleanMatches[11];
Require(PortalScanQuality.CanReview(ambiguousMatches), "A single marked correction should remain reviewable.");
ambiguousMatches[0] = new('0', -0.1, 0.7);
Require(!PortalScanQuality.CanReview(ambiguousMatches), "Negative match distances were accepted.");
Console.WriteLine("PASS: whole-strip quality, isolated poor fits, multiple ambiguities and one reviewable correction.");

var shape = new List<(int X, int Y)> { (0, 0), (0, 1), (0, 2), (1, 2), (2, 2) };
var translated = shape.Select(p => (p.X * 3 + 15, p.Y * 3 + 42)).ToArray();
Require(GlyphShape.Describe(shape).Zip(GlyphShape.Describe(translated)).All(p => Math.Abs(p.First - p.Second) < 1e-10),
    "Shape description must preserve translation and scale.");
try { new PortalGlyphModel(99, "test", []).Validate(); throw new Exception("Accepted corrupt model."); }
catch (InvalidDataException) { }

var examples = "0123456789ABCDEF".Select((c, i) => new GlyphShapeExample(c,
    Enumerable.Range(0, GlyphShape.FeatureCount).Select(v => (double)(v + i) / 50).ToArray())).ToArray();
var syntheticModel = new PortalGlyphModel(1, "synthetic", examples);
syntheticModel.Validate();
Require(syntheticModel.Match(examples[8].Features).Digit == '8', "Nearest-class matching failed.");
using (var blank = new SKBitmap(500, 200))
{
    blank.Erase(SKColors.Black);
    Require(!new PortalGlyphRecognizer(syntheticModel).Recognize(blank).HasCandidate, "Blank input produced a portal address.");
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    try { new PortalGlyphRecognizer(syntheticModel).Recognize(blank, cancelled.Token); throw new Exception("Ignored cancellation."); }
    catch (OperationCanceledException) { }
}
using (var invalid = new MemoryStream([0, 1, 2, 3]))
{
    try { using var decoded = PortalPhotoDecoder.Decode(invalid); throw new Exception("Accepted corrupt image."); }
    catch (InvalidDataException) { }
}
Console.WriteLine("PASS: shape invariance, model validation/matching, blank image rejection, cancellation, corrupt-image rejection.");

using (var original = new SKBitmap(80, 60))
{
    using (var canvas = new SKCanvas(original))
    using (var paint = new SKPaint())
    {
        foreach (var (x, y, color) in new[] { (0, 0, SKColors.Red), (40, 0, SKColors.Green),
            (0, 30, SKColors.Blue), (40, 30, SKColors.Yellow) })
        { paint.Color = color; canvas.DrawRect(x, y, 40, 30, paint); }
    }
    using var encoded = original.Encode(SKEncodedImageFormat.Jpeg, 100);
    byte[] jpeg = encoded.ToArray();
    SKColor[] expectedTopLeft = [SKColors.Red, SKColors.Green, SKColors.Yellow, SKColors.Blue,
        SKColors.Red, SKColors.Blue, SKColors.Yellow, SKColors.Green];
    for (byte orientation = 1; orientation <= 8; orientation++)
    {
        byte[] exif = [255, 225, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0,
            1, 0, 18, 1, 3, 0, 1, 0, 0, 0, orientation, 0, 0, 0, 0, 0, 0, 0];
        using var stream = new MemoryStream(jpeg[..2].Concat(exif).Concat(jpeg[2..]).ToArray());
        using var photo = PortalPhotoDecoder.Decode(stream);
        Require(photo.Width == (orientation >= 5 ? 60 : 80) && photo.Height == (orientation >= 5 ? 80 : 60), "Incorrect EXIF dimensions.");
        var actual = photo.GetPixel(10, 10); var expected = expectedTopLeft[orientation - 1];
        Require(Math.Abs(actual.Red - expected.Red) + Math.Abs(actual.Green - expected.Green) +
            Math.Abs(actual.Blue - expected.Blue) < 20, $"EXIF orientation {orientation} is incorrect.");
    }
}
Console.WriteLine("PASS: all eight EXIF orientations, including mirrored camera images.");

// Optional private reference checks. Photos and generated game-derived model stay outside git.
if (args.Length > 0)
{
    if (args.Length % 2 != 1) throw new ArgumentException("Expected <model> followed by pairs of <photo> <address or ->. Prefix an address with ? to permit rejection.");
    var model = JsonSerializer.Deserialize(File.ReadAllText(args[0]), PortalGlyphModelJsonContext.Default.PortalGlyphModel)
        ?? throw new InvalidDataException("Missing model.");
    model.Validate();
    var failures = new List<string>();
    for (int i = 1; i < args.Length; i += 2)
    {
        using var stream = File.OpenRead(args[i]);
        using var image = PortalPhotoDecoder.Decode(stream);
        if (Environment.GetEnvironmentVariable("ARTIFACTX_SCAN_DEBUG") is string debugFolder)
        {
            Directory.CreateDirectory(debugFolder);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var output = File.Create(Path.Combine(debugFolder, "decoded.png"));
            data.SaveTo(output);
        }
        var watch = Stopwatch.StartNew();
        Action<string>? trace = Environment.GetEnvironmentVariable("ARTIFACTX_SCAN_TRACE") == "1" ? Console.WriteLine : null;
        var result = new PortalGlyphRecognizer(model, trace).Recognize(image);
        Console.WriteLine($"{Path.GetFileName(args[i])}: {result.Candidate ?? "unreadable"}, correction={result.NeedsCorrection}, {watch.ElapsedMilliseconds}ms");
        Console.WriteLine(string.Join(" ", result.Glyphs.Select(g => $"{g.Digit}:{g.Distance:F3}/{g.Margin:F2}")));
        bool mayReject = args[i + 1].StartsWith('?');
        string? expected = args[i + 1] == "-" ? null : args[i + 1].TrimStart('?');
        if (result.Candidate != expected && !(mayReject && !result.HasCandidate))
            failures.Add($"{Path.GetFileName(args[i])}: expected {expected ?? "no candidate"}, got {result.Candidate ?? "no candidate"}.");
    }
    Require(failures.Count == 0, string.Join(Environment.NewLine, failures));
}
