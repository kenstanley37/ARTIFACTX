# Live portal scanner - 2026-11-10

Continues `feature/maui-foundation`. The owner's Photo Mode screenshot shows all twelve glyphs immediately above `Nudquathsenfe Galaxy` in the lower-left corner. That name matches galaxy 110 in the existing galaxy list. The screenshot was inspected visually, not passed through the new camera/OCR pipeline.

## Behavior and structure

- `PortalGlyphsPage` now offers **Scan screen**, opening `LivePortalScannerPage`. The live preview has no shutter/confirm step. Photo import and manual entry remain available separately.
- `CameraPreviewSource` uses CameraMaui preview snapshots, not still-photo capture or recording. Frames are owned memory streams; no gallery writes, frame files or uploads. It selects the rear camera, disables flash and requests only camera permission.
- `PortalFrameAnalyzer` caches the existing generated numerical glyph model per scan page, processes one frame at a time off the UI thread, and uses the glyph strip's detected bounds to crop galaxy text below it. Small text is enlarged before OCR. Frames are sampled only after previous analysis finishes, plus a 350 ms pause: there is no queued frame backlog.
- Android `GalaxyTextReader` uses bundled Latin ML Kit OCR. Apple targets use Vision; Windows uses installed English Windows OCR. Android minimum is now API 23 / Android 6, as required by ML Kit (https://developers.google.com/ml-kit/vision/text-recognition/v2/android). AndroidX package alignment avoids NU1608 dependency conflicts.
- Portable `GalaxyNameMatcher` requires a complete known name, optionally followed by `Galaxy`, normalizes case/spacing/punctuation, rejects unknown and conflicting names, and never guesses Euclid.
- Portable `LivePortalScanState` requires three consecutive identical address/galaxy pairs. Each frame must pass the existing strip-quality gate and all twelve glyphs must be certain. Uncertain/missing/conflicting readings, non-increasing timestamps and gaps longer than ten seconds reset agreement. A single frame's galaxy is never combined with another frame's glyph address.
- `LivePortalScannerViewModel` controls scan state and completion. It releases the camera before replacing the scan route with system details. Navigation away or window backgrounding cancels the scan; resuming begins a fresh agreement sequence. Late analysis cannot navigate after cancellation. The page's code-behind only wires lifecycle and camera access.

## Verification and limitations

All seven default synthetic check suites passed. New synthetic coverage includes galaxy 110, multiword names, partial/unknown/conflicting text rejection, three-frame agreement, uncertainty, changed addresses and galaxies, duplicate timestamps, long gaps, cancellation during the final frame, camera release before navigation, frame disposal and missing model behavior.

MAUI Android and Windows builds, and iOS/Mac Catalyst managed compile targets, complete with zero warnings/errors. Apple packaging/signing still requires a Mac. The WinUI build is also checked. No app or device was launched or clicked through.

These are structural and build checks, not a camera accuracy or timing benchmark. The previous recognizer still has limited real-image class coverage (nine of sixteen in the earlier private reference set). Three-frame agreement reduces transient errors but cannot eliminate a consistently wrong read. The new galaxy OCR and spatial pairing need owner phone tests against real screens, glare, distance and orientation. Numerical glyph data remains generated/ignored, not committed game data; builds without it keep manual entry and show a clear message before starting the camera.
