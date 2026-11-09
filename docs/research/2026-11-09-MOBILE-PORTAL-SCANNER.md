# Mobile portal scanner — 2026-11-09

Continues `feature/maui-foundation`. The active Systems page now uses
`PortalScannerViewModel`; it does not call the unfinished sample-returning
`PortalGlyphsViewModel` or `PortalSystemReadout` from the pre-existing local work.
Those earlier files remain uncommitted. Their mock names, hardcoded body details,
and sample fallback are not used by this scanner.

## Structure

- `ArtifactX.PortalScanning`: bounded photo decoding, all eight EXIF orientations,
  white-strip localization, shallow-angle/perspective tolerance, and numerical
  shape classification. It has no dependency on Core, WinUI, libMBIN, or MAUI.
- `ArtifactX.GameData/Services/PortalGlyphModelExportService.cs`: reads the local
  catalog in read-only mode and exports a game-build-stamped numerical model.
  Low-order Legendre moments and aspect ratios are stored, not source pixels,
  drawable paths, textures or reference photographs.
- `ArtifactX.Maui/Services/PortalPhotoScanService.cs`: native camera/photo picker
  and local background processing. No upload and no save access.
- `ArtifactX.Maui/ViewModels/PortalScannerViewModel.cs`: retained address entry,
  separate candidate review, manual correction, explicit accept/discard, galaxy
  choice, and Core-based system/region/body-count lookup.
- `ArtifactX.Maui/Controls/PortalGlyphReviewPanel.xaml`: reusable review/keypad UI.
- `tests/ArtifactX.PortalScanning.Checks`: platform-free scanner and ViewModel
  checks, with optional private image regression inputs.

## Build data

Generate the ignored mobile resource from a desktop catalog before building:

```powershell
dotnet run --project src/ArtifactX.Tools/ArtifactX.Tools.DataCataloger -- export-portal-model "$env:APPDATA\ArtifactX\GameData\nms_catalog.sqlite" src/ArtifactX.Maui/Resources/Raw/portal-glyph-model.json
dotnet build src/ArtifactX.Maui -f net10.0-android
```

The generated JSON is a release artifact and stays out of git. Regenerate it
from the matching game build for release packaging. Builds without it retain
manual entry and clearly report unavailable scanner data. The development
reference model was generated from game executable version `184271`, fingerprint
`C3B61BD0FE434A1E`, catalog format 74. No game images are bundled.

## Measured evidence and limits

The owner's desktop screenshot and `PXL_20261109_185624545.jpg` independently
produce `2197F8FF7007` in the offline recognizer. Budullangr is visible in the
images, but galaxy recognition is not implemented: the player selects it.
Both references show the same address and therefore cover only seven distinct
glyph classes: `0, 1, 2, 7, 8, 9, F`. General camera accuracy and the other nine
classes still need real photographs. The classifier is experimental, not a
trained general-purpose vision model or a calibrated confidence percentage.

Three negative crops (monitor texture, desk/background, and screenshot without
the glyph strip) are rejected. Initial monitor-pattern false positives prompted
a conservative whole-strip score threshold; these are regression cases, not an
independent accuracy benchmark. Additional unrelated images are still needed.

Synthetic checks cover shape invariance, model validation and matching, blank
and corrupt input, all EXIF orientations including mirrors, cancellation,
concurrent media-operation suppression, manual corrections, incomplete reviews,
accept/discard, and preservation of the accepted address after failure.

Validation: Android MAUI, Windows MAUI and WinUI desktop builds each completed
with zero warnings and zero errors. All seven `tests/*.Checks` default synthetic
suites passed, as did the explicit portal-travel, food-display and planet-shop
survey checks. The signed Android APK contains the generated numerical model
and the ARM64 Skia native dependency. These are build/offline checks, not device
runtime validation.

Camera capture is a still-photo flow through the platform camera, not a live
viewfinder. Every result requires player review. The existing original vector
art is reused for the keypad; it is not used as the recognition reference.
System names are generated and can differ from player-renamed discoveries.
No app was launched or clicked through. Android device behavior, screen-reader
output and visual layout must be tested by the owner. iOS/Mac Catalyst require
a Mac build and device checks before they can be claimed as supported here.

The next useful reference is a photo-mode strip from another system with glyphs
outside the seven above. Then test different distances, gentle viewing angles,
glare, darker/brighter screens, and rejection of text and unrelated imagery.
