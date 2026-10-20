# Agent Guidelines & Preferences

## Git & Branching Strategy
- **Never edit the `development` branch directly**: Always create a dedicated branch for each feature/task (e.g. `feature/...` or `refactor/...`).
- **Merge only with user approval**: Keep changes on the feature branch until the user reviews and approves merging into `development`.
- **Tag every release**: Always create an annotated git tag (`git tag -a vX.Y.Z -m "Release vX.Y.Z"`) for every production release to ensure the exact build state is permanently preserved and reproducible. Use hotfix branches (`hotfix/...`) off release tags if historical patching is required.

## Autonomous Execution & Decision Making
- **Bias for action**: Execute tasks directly end-to-end without stopping for confirmation, check-ins, or follow-ups.
- **Do not ask questions**: Do not ask clarifying questions, solicit design preferences, or prompt with multiple-choice options. When details are open to interpretation, choose the most idiomatic, consistent solution matching the existing codebase conventions and implement it.
- **Independent problem solving**: Only pause if progress is strictly blocked by missing external credentials or unrecoverable environmental issues.
- **Concise reporting**: Make the changes, verify them, and present the final result directly.

## Engineering Standards & Architecture
- **Library & Project Layering**: Create or add to class libraries as needed (`ArtifactX.Core`, `ArtifactX.GameData`, `ArtifactX.Tools`). Domain models, save schema abstractions, and business logic belong in dedicated libraries, never coupled to UI code-behind.
- **Best Practice on Everything**:
  - **Zero UI-Thread Freezes**: All page navigations must complete visually and immediately (`await Task.Yield()`). Use active `ProgressRing` spinners and offload disk I/O, SQLite queries, and JSON traversals to `Task.Run` background threads.
  - **Componentization First**: Break complex or heavy views into dedicated drill-down pages or reusable controls (e.g. `BaseDetailsPage`, `PlanetDetailsPage`) rather than monolithic mega-pages.
  - **MVVM Pattern**: Back UI elements with `ObservableObject` ViewModels and reactive data binding rather than procedural code-behind UI tree construction.
  - **Virtualization & Memory Management**: Employ UI virtualization for large collections to avoid element explosion and memory bloat.
- **Professional Polish**:
  - Never expose raw engine keys, unformatted IDs, or internal hashes (e.g. `STACONNECT`, `^UP_SCAN0`) to the user; provide clean, title-cased labels, catalog icons, or styled fallback glyph badges.
  - Ensure zero compiler errors and proactively resolve warnings (unintended reference comparisons, potential null dereferences, AOT marshalling warnings).
  - Proactively identify and report non-professional patterns, performance bottlenecks, or architectural debt.
