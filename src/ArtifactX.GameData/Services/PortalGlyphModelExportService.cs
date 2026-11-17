using System.Text.Json;
using ArtifactX.PortalScanning;
using Microsoft.Data.Sqlite;
using SkiaSharp;

namespace ArtifactX.GameData.Services;

/// <summary>
/// Exports non-drawable shape moments for the mobile scanner from the player's local catalog.
/// Source icons are used only here, never copied into the mobile bundle. Like the other mobile
/// rule tables, the generated numerical model is build-stamped and stays out of source control.
/// </summary>
public static class PortalGlyphModelExportService
{
    public static void Export(string catalogPath, string outputPath)
    {
        var info = GameDataInfo.Read(catalogPath) ?? throw new InvalidDataException("The catalog has no game build stamp.");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Items.GameId, IconBlobs.PngData FROM Items
            JOIN Icons ON Icons.ItemId = Items.Id JOIN IconBlobs ON IconBlobs.Id = Icons.IconBlobId
            WHERE Items.GameId LIKE 'PORTALSYMBOL_%' ORDER BY Items.GameId
            """;
        using var reader = command.ExecuteReader();
        var examples = new List<GlyphShapeExample>();
        while (reader.Read())
        {
            string id = reader.GetString(0);
            if (id.Length != "PORTALSYMBOL_0".Length || !"0123456789ABCDEF".Contains(id[^1])) continue;
            using var source = SKBitmap.Decode((byte[])reader[1]) ?? throw new InvalidDataException($"Unreadable glyph {id}.");
            foreach (int size in new[] { 16, 24, 40, 80, 128 })
            {
                using var scaled = new SKBitmap(size, size);
                using (var canvas = new SKCanvas(scaled))
                using (var image = SKImage.FromBitmap(source))
                    canvas.DrawImage(image, new SKRect(0, 0, size, size), new SKSamplingOptions(SKFilterMode.Linear));
                var pixels = scaled.Pixels;
                foreach (int threshold in new[] { 80, 140, 200, 235 })
                {
                    var points = new List<(int, int)>();
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                            if (pixels[y * size + x].Alpha > threshold) points.Add((x, y));
                    if (points.Count >= 4) examples.Add(new(id[^1], GlyphShape.Describe(points)));
                }
            }
        }
        var model = new PortalGlyphModel(1, $"{info.ExeVersion}/{info.Fingerprint}", examples.ToArray());
        model.Validate();
        string destination = Path.GetFullPath(outputPath);
        if (StringComparer.OrdinalIgnoreCase.Equals(destination, Path.GetFullPath(catalogPath)))
            throw new ArgumentException("The output must differ from the catalog.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, JsonSerializer.Serialize(model, PortalGlyphModelJsonContext.Default.PortalGlyphModel));
    }
}
