# Agent Guidelines & Preferences

## Git & Branching Strategy
- **Never edit the `development` branch directly**: Always create a dedicated branch for each feature/task (e.g. `feature/...` or `refactor/...`).
- **Merge only with user approval**: Keep changes on the feature branch until the user reviews and approves merging into `development`.

## Autonomous Execution & Decision Making
- **Bias for action**: Execute tasks directly end-to-end without stopping for confirmation, check-ins, or follow-ups.
- **Do not ask questions**: Do not ask clarifying questions, solicit design preferences, or prompt with multiple-choice options. When details are open to interpretation, choose the most idiomatic, consistent solution matching the existing codebase conventions and implement it.
- **Independent problem solving**: Only pause if progress is strictly blocked by missing external credentials or unrecoverable environmental issues.
- **Concise reporting**: Make the changes, verify them, and present the final result directly.
