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

MAUI Android and Windows builds, and iOS/Mac Catalyst managed compile targets, complete with zero warnings/errors. Apple packaging/signing still requires a Mac. The WinUI build is also checked. No app or device was launched during those initial build checks; subsequent owner-authorized Pixel tests are recorded below.

These are structural and build checks, not a camera accuracy or timing benchmark. The previous recognizer still has limited real-image class coverage (nine of sixteen in the earlier private reference set). Three-frame agreement reduces transient errors but cannot eliminate a consistently wrong read. The new galaxy OCR and spatial pairing need owner phone tests against real screens, glare, distance and orientation. Numerical glyph data remains generated/ignored, not committed game data; builds without it keep manual entry and show a clear message before starting the camera.

## Owner-authorized Pixel testing and lock-on guide

The owner explicitly authorized testing on the development-only Pixel. ADB identifies it as a Pixel 7 Pro running Android 17. The signed Debug APK was updated in place, preserving app data. Android 17 blocked the Android CLI instrumentation helper; testing used native ADB UI dumps and screen capture instead, without overriding Play Protect.

The first live camera run read `1045FBFFB004` and `Nudquathsenfe` (galaxy 110), matching the visible game screen. It exposed two runtime problems: ten-second processing intervals reset agreement indefinitely, and backgrounding could leave the scanner processing a frozen preview. The fixes distinguish analysis time from actual capture idle gaps, attach window lifecycle handlers after the Shell page is loaded, and restart cancelled sessions on activation. Regression checks include consecutive fifteen-second analyses and a separate idle gap.

A subsequent Pixel run completed three matching readings, automatically opened `Gelatanes XVIII` with address `1045FBFFB004` in Nudquathsenfe, and released the rear camera. Back returned to address entry. Background/foreground testing showed an empty active-camera list while stopped, followed by a new camera connection and fresh scan on return. No game controls or save files were touched.

Frame timings in the Debug APK remained approximately 9-11 seconds for this target. Tracking tries a smaller region after a detection and falls back to the full frame on loss; a broad speed improvement has not been established. More lighting, angle and address coverage remains necessary.

The owner's requested visual guide is a separate custom-drawn `PortalScanOverlay` above the camera view. It uses normalized bounds from the detected strip and nearby galaxy band, smoothly moves corner brackets to that area, and turns green for a 450 ms confirmation after three matching pairs. The camera is closed before that confirmation completes and before navigating. The overlay is not part of the camera pixels passed to recognition. Guide, tracking and lock colors use theme resources in both themes. Synthetic checks cover normalized scaling, boundary clamping, invalid bounds and accepted lock state. Pixel visual checks verify the guide and its relation to the camera preview.

## Recognizer speed, result-preserving (2026-11-10, Claude)

Desktop stage timing on `PXL_20261109_193430115.MP.jpg` showed the threshold/slope band search dominating every pass, and each of up to 81 threshold/slope/pass searches per frame allocated roughly 12 MB of shear, flood-fill and column-prefix buffers. Changes in `PortalGlyphRecognizer`, none of which alter which strip is chosen:

- Local contrast and saturation are evaluated once per colour pass instead of once per brightness threshold.
- Shear, flood-fill and column-prefix buffers are rented per worker from `ArrayPool` and cleared to the used length.
- The 27 threshold/slope searches per pass run on at most four workers (one core left for the camera preview and UI), then merge in the original sequential order with the same strict `<` comparison, so ties resolve exactly as before.
- Within a band, each run's shape match is computed once and reused by every overlapping 12-run window (previously up to twelve times).

Evidence: the eleven private Pixel references (seven reads, five rejections in the earlier table, minus the unlocated original screenshot) produce byte-identical candidates, per-glyph distances and margins before and after. Release-mode desktop time for that run fell from 35.8 s to 16.2 s including process start and the tracking re-check; the slowest photo fell from 5.4 s to 2.3 s. Phone timing was not measured after this change; the earlier 9-11 s figure is a Debug APK and needs a fresh Pixel measurement, ideally Debug and Release.

## Pixel live tests, cropped tracking and multi-frame agreement (2026-11-10, Claude)

Owner granted standing use of the test Pixel. Debug APK, phone held at the owner's monitor, address `3045FBFFB004` (Nudquathsenfe, galaxy 110). Glyph 3 had not appeared in the earlier photo set, so real-image class coverage is now ten of sixteen.

Per-step Debug timings: decode 35-85 ms; full glyph search 2.1-3.9 s (no strip) / 2.5-3.2 s (strip); ML Kit galaxy OCR 140-480 ms; ~1 s between frames. OCR is not the bottleneck.

- The masked tracking search cost the same as a full search (~2.6 s), and a miss doubled the frame (6.1 s). `PortalGlyphRecognizer.Recognize` now crops to the tracking area and maps the region back. `TrackingArea` is shared by the phone and the reference runner, which now requires the tracked re-read to return the same address and strip position: all seven readable references pass, tracked reads 13-267 ms on desktop.
- The game draws the strip on a transparent background. Over bright scenery, position 8 `F` alternated between confident and d=0.187/m=0.15, and position 12 `4` once read d=0.230/m=0.09; the all-confident rule kept resetting. `LivePortalScanState` now counts frames with at most one uncertain symbol (the existing quality limit) and locks only when every position was confident in at least two of three or more agreeing frames. Synthetic checks cover one/two uncertain symbols, confident-once and never-confident positions.
- Results: four locks onto `Gelatanes XVIII`; the last, with the new rule, locked 13 s after the strip first appeared, with an uncertain final symbol on the locking frame.

Open: one cropped tracked hit took 4.9 s on the phone (0.2 s on desktop for comparable crops) while the phone's CPU sensor read 90 C after ~30 min of continuous scanning and charging. Debug-on-Android timings are unreliable; the next measurement should be a Release APK. Tracking still misses on some hand-held frames; the vertical margin (half a strip height) may be too tight.

## Release timing, guide-box search and empty-frame tolerance (2026-11-10, Claude)

Release APK with `-p:ScanDiagnostics=true` (logs are compiled out of normal Release builds). Before these changes a Release frame cycle was ~2.2 s: PNG snapshot ~600 ms, decode ~80 ms, full-frame search 1.05-1.26 s (Debug: 3.8 s), 350 ms pause.

- Snapshot as JPEG: 30-130 ms.
- Search order: tracked strip area, then `PortalScanTarget.Guide` (the bracket box, widened by 4% of the frame), and the full frame only every third miss. Box search 0.4-0.7 s.
- Pause between frames 100 ms.
- Tracking margin widened to one strip height vertically and two horizontally; half a strip missed most hand-held frames. Reference runner re-reads all seven strips in place.
- `LivePortalScanState` ignores up to two consecutive frames with nothing usable (no strip, or strip without a read galaxy name). A strip that disagrees still resets.

Pixel result, hand-held at the monitor: frames 0.55-0.9 s; locked 2.4 s after the strip first appeared, 4.6 s after the camera opened.

Glyph artwork: `ArtifactXPortalGlyphs` replaced with ArtifactX's own filled cartoon silhouettes (48x48, even-odd), named Sunset, Bird, Face, Diplo, Eclipse, Balloon, Boat, Bug, Dragonfly, Galaxy, Voxel, Fish, Tent, Rocket, Tree, Atlas. No game assets used. Verified on the Pixel: the keypad entered `3045FBFFB004` and System details shows the strip.
