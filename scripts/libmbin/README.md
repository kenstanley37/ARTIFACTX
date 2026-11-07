# libMBIN structure generator

Regenerates libMBIN's data structure classes (`src/ArtifactX.Tools/NMS.Tools.libMBIN/Source/NMS`) from the structure
metadata NMS.exe carries for every game data class: name, NameHash, GUID, size, and each field's name, type, size,
offset, nested class and enum values. Used when a game update changes structures before an upstream libMBIN release
(first used for Forsaken 7.10, 2026-10-28). It is also the start of ArtifactX's own knowledge of the game's data
formats.

- `nmsmeta.py`: reads the metadata from NMS.exe (class records 0x28 bytes, field records 0x58 bytes, enum value lists).
  `LiveImage` falls back to the running game's memory, read-only, for the few classes whose field list is only filled
  at run time (the big globals).
- `libsrc.py`: parses libMBIN's existing C# classes.
- `gen.py`:
  - `python gen.py selftest`: regenerates every class whose GUID still matches libMBIN and compares the field lines
    (2,697 of 2,702 identical on build 183791; the rest are one-off hand edits the generator keeps for existing fields).
  - `python gen.py diff`: classes changed or new in the game.
  - `python gen.py write [Class ...]`: writes the changed and new classes. Set `NMS_LIVE=1` with the game running to
    include classes whose field list is only in memory; without it those are skipped, never written empty.
  - Existing libMBIN names (renamed fields, enum names, `KeyField`, `EnumType`) are kept, so app code keeps compiling.

After writing: build libMBIN and the app, run a full `DataCataloger build-all` into a scratch file and check the log
has no "failed to decode", note the change in `THIRD-PARTY-NOTICES.md` (LGPL), and update
`src/ArtifactX.GameData/TestedGameRelease.cs`.

`NMS_EXE` overrides the NMS.exe path. Static reads of NMS.exe only; nothing extracted from the game is committed.
