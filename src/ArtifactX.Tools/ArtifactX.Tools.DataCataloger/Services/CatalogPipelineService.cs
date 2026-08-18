using libMBIN;
using ArtifactX.Tools.DataCataloger.Services.Interfaces;

namespace ArtifactX.Tools.DataCataloger.Services;

public interface ICatalogPipelineService
{
    void ProcessPak(string pakPath);
}
public class CatalogPipelineService : ICatalogPipelineService
{
    private readonly IPakReaderService _pakReader;
    private readonly ITestService _testService;
    private readonly ExtractionService _extractionService;

    public CatalogPipelineService(
        IPakReaderService pakReader,
        ITestService testService,
        ExtractionService extractionService)
    {
        LogService.Write(">>> CatalogPipelineService CONSTRUCTOR FIRED <<<");

        _pakReader = pakReader;
        _testService = testService;
        _extractionService = extractionService;
    }

    public void ProcessPak(string pakPath)
    {
        LogService.Write(">>> CatalogPipelineService.ProcessPak() is running <<<");
        LogService.Write($"DEBUG: ProcessPak invoked for {pakPath}");

        string pakName = Path.GetFileName(pakPath).Trim();
        string pakNameLower = pakName.ToLowerInvariant();

        LogService.Write($"Reading entries in {pakName}");
        var entries = _pakReader.Read(pakPath);

        var header = _pakReader.ReadHeader(pakPath);

        // --- TESTS ------------------------------------------------------------
        _testService.Header(header, pakName);
        _testService.Entries(entries, pakName);
        // NOTE: _testService.Manifest(...) removed - it hunted for a filename
        // containing "MANIFEST", which was only ever true for the raw entry-0
        // pseudo-file. That entry is now correctly parsed and stripped inside
        // PakReaderService.Read(), so this check is obsolete and only produced
        // misleading "No manifest entry found" noise in the log.

        // ======================================================================
        // SINGLE ENTRY DEBUG TEST (always fires for globals regardless of casing)
        // ======================================================================
        if (pakNameLower.Contains("globals"))
        {
            LogService.Write("=== DEBUG: SINGLE ENTRY EXTRACTION TEST ===");
            LogService.Write($"  IsCompressed: {header.IsCompressed}");
            LogService.Write($"  ChunkCount:   {header.ChunkCount}");
            LogService.Write($"  FileCount:    {entries.Count}");

            var firstMbin = entries
                .Where(e => !string.IsNullOrEmpty(e.FileName) &&
                            e.FileName.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Offset)
                .FirstOrDefault();

            if (firstMbin != null)
            {
                LogService.Write($"  Testing entry: {firstMbin.FileName}");
                LogService.Write($"    Offset:         {firstMbin.Offset}");
                LogService.Write($"    RelativeOffset: {firstMbin.RelativeOffset}");
                LogService.Write($"    Size:           {firstMbin.Size}");

                byte[]? rawBytes = _extractionService.ExtractEntryBytes(pakPath, firstMbin, header);

                if (rawBytes == null || rawBytes.Length == 0)
                {
                    LogService.Write("  DEBUG: Extraction failed.");
                }
                else
                {
                    string debugOut = Path.Combine(AppContext.BaseDirectory, "Working", "DEBUG");
                    Directory.CreateDirectory(debugOut);

                    string outPath = Path.Combine(debugOut, firstMbin.FileName + ".debug.mbin");
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                    File.WriteAllBytes(outPath, rawBytes);

                    LogService.Write($"  DEBUG: Wrote {rawBytes.Length} bytes → {outPath}");
                }
            }

            LogService.Write("=== END DEBUG TEST ===");
        }

        // ======================================================================
        // MBIN DISCOVERY
        // ======================================================================
        var mbinEntries = entries
            .Where(e => !string.IsNullOrEmpty(e.FileName) &&
                        e.FileName.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (mbinEntries.Count == 0)
        {
            LogService.Write($"No MBIN entries found in {pakName}");
            LogService.Write("--------------------------------------------------------------------------------");
            return;
        }

        LogService.Write($"Found {mbinEntries.Count} MBIN entries in {pakName}");

        // ======================================================================
        // MBIN EXTRACTION LOOP
        // ======================================================================
        foreach (var entry in mbinEntries)
        {
            LogService.Write($"Extracting {entry.FileName}...");

            byte[]? bytes = _extractionService.ExtractEntryBytes(pakPath, entry, header);
            if (bytes == null || bytes.Length == 0)
            {
                LogService.Write($"  Failed to extract {entry.FileName}");
                continue;
            }

            try
            {
                using var ms = new MemoryStream(bytes);
                var mbin = new MBINFile(ms);

                mbin.Load();
                var template = mbin.GetData();

                _testService.Template(template, entry.FileName);

                LogService.Write($"  Decoded {entry.FileName} successfully.");

                string mxmlFolder = Path.Combine(AppContext.BaseDirectory, "Working", "EXML");

                // entry.FileName can contain forward-slash subpaths (e.g.
                // "models/planets/.../foo.mbin") - normalize to the platform
                // separator and ensure every intermediate directory exists
                // before writing, or WriteToMxml throws.
                // NOTE: template.ToString() is NOT a serializer - it's the
                // inherited Object.ToString(), which just prints the fully
                // qualified type name (that's why every "exml" file was ~40
                // bytes of nothing). The real writer is WriteToMxml, since
                // this libMBIN build (post Worlds Part II) uses MXML rather
                // than the older EXML format.
                string relativePath = entry.FileName.Replace('/', Path.DirectorySeparatorChar);
                string mxmlPath = Path.Combine(mxmlFolder, relativePath + ".mxml");

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(mxmlPath)!);

                    template.WriteToMxml(mxmlPath, hideVersionInfo: false, IncludeTypeInfo: true);

                    LogService.Write($"  Saved MXML → {mxmlPath}");
                }
                catch (Exception ex)
                {
                    LogService.Write($"  Failed MXML export: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogService.Write($"  Failed to decode {entry.FileName}: {ex.Message}");
            }
        }

        LogService.Write("--------------------------------------------------------------------------------");
    }
}