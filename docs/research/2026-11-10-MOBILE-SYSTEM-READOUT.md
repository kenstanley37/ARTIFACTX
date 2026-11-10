# Mobile portal system readout

Branch: `feature/maui-foundation`.

The real `PortalScannerViewModel` now navigates to `SystemDetailsPage` with the accepted portal address and explicitly selected galaxy. Address generation runs off the UI thread in the details ViewModel. Revisiting the same loaded page preserves its collections, filters and selected tab.

Core `PortalSystemReadout` composes the existing system, planet/ring and standard starship generators. Equipment names use sequential labels until mobile naming tables exist. S-Class statistics are previews, not a located ship's measured stats. Interceptors are omitted because dissonance has not been established for the entered address. Multi-tool pools are unavailable until versioned game tables are supplied; the UI explains this. Removed the unfinished prototype's sample planet annotations, invented equipment names, guessed tool kinds and default pool definitions. No game tables or assets were added.

Home shares the ten most recently opened systems for the current app session. Identity excludes the body glyph and includes galaxy; history is currently in memory, not persisted across restarts.

Validation: Core, PortalScanning, Cargo, VehicleUnlocks, StationCards, RecipeRequirements and GameData synthetic checks passed. New checks cover address/galaxy rejection, body identity, repeatable seeds, missing-table boundaries, scanner navigation and recent-history identity/limit. MAUI Windows, MAUI Android and WinUI builds are checked separately. No app, device or in-game interaction was performed; owner validation is still required for layout, navigation and clipboard behavior.
