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
dotnet build src/ArtifactX.Maui -f net10.0-android -p:EmbedAssembliesIntoApk=true
```

The generated JSON is a release artifact and stays out of git. Regenerate it
from the matching game build for release packaging. For manual phone installation,
keep `EmbedAssembliesIntoApk=true`; the default Debug APK uses Visual Studio fast
deployment and does not contain the managed app assemblies.

Builds without the model retain manual entry and clearly report unavailable
scanner data. The development
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

## Follow-up: second system, 2026-11-09

The owner's two new photos, `PXL_20261109_193426718.jpg` and
`PXL_20261109_193430115.MP.jpg`, both show `21B4F8FF7007`. Visual inspection
identified that address before the recognizer was changed. The original scanner
rejected both. Strong LCD subpixel patterns and blue-tinted, low-contrast strokes
made the minimum-RGB white filter discard pieces of glyphs.

The original neutral pass is retained. On failure, supplemental passes use
blue/red opposition and then blue-channel contrast, with a 3x3 average and
isolated-speck removal. Small internal gaps are bridged more generously in
these passes. They all use the original whole-strip acceptance threshold;
no address-specific templates, training examples or sample fallback were added.

Both new photographs now produce the visually inspected address. The original
screenshot and photograph still produce `2197F8FF7007`. All five negative crops
(the earlier three plus glyph-free upper sections of each new photograph) are
rejected. Synthetic checks additionally exercise small-component removal,
diagonal stroke retention and prevention of pixel connections across row edges.

Real-image class coverage is now nine of sixteen: `0, 1, 2, 4, 7, 8, 9, B, F`.
The other seven classes and broader camera conditions remain unverified. These
photos were used to develop the preprocessing, so they are regression evidence,
not an independent accuracy benchmark. On this PC, the release-mode offline
checks took roughly four seconds per new photo; phone timing is not measured.

## Follow-up: additional angle/background references

Eight further images exposed two wrong glyph candidates and acceptance of an
obscured strip. The whole-strip score limit remains 0.20, but the quality gate
now allows at most one ambiguous symbol and rejects an isolated poor match in
an otherwise clean row. The per-symbol margin requirement is stricter. Quality
is checked before choosing the best band, so an invalid low-score band cannot
hide a better valid band. No address-specific correction is applied.

| Photograph | Current offline result |
| --- | --- |
| `PXL_20261109_205353051.MP.jpg` | `1147F8FF7007` |
| `PXL_20261109_205529220.jpg` | `1182F8FF7007`; the earlier incorrect C reading is eliminated |
| `PXL_20261109_205543438.jpg` | Rejected; hologram/grid behind the strip |
| `PXL_20261109_205558669.jpg` | Rejected; the earlier incorrect final A reading is eliminated |
| `PXL_20261109_205605967.jpg` | `1182F8FF7007`, despite the smaller strip |
| `PXL_20261109_225149023.jpg` | Rejected; bright scenery obscures positions |
| `PXL_20261109_225202528.MP.jpg` | Rejected; heavily out of focus |
| `PXL_20261109_225215590.jpg` | Still rejected; green glare/low contrast remains unsupported for this reference |

The complete private regression run contains twelve full images: seven correct
reads and five rejections. All five background-only crops are also rejected.
There are no wrong accepted addresses in this reference set after the change;
this is not a general accuracy claim. One accepted older blue-tinted photograph
now marks one glyph for careful review. The remaining full addresses still
match the visually inspected references. Real-image class coverage stays at
nine of sixteen.

Added synthetic checks exercise isolated poor fits, multiple ambiguous symbols,
one reviewable correction and invalid quality inputs. The optional image-check
runner reports every case before failing; `-` requires rejection and `?ADDRESS`
permits either rejection or that exact address. The final seventeen-case run
uses strict expected addresses/rejections, with no optional acceptance cases.
