# Architectural Specification

## 1. Container Format (ArtifactX.Core / SaveStreamProcessor)
Save files (`save*.hg`) are a sequence of blocks, each a 16-byte header (4-byte magic
`0xFEEDA1E5`, 4-byte compressed size, 8-byte decompressed size) followed by an
LZ4-encoded chunk. Hello Games' own writer caps each chunk at 512KB decompressed,
with a final smaller chunk for the remainder — `SaveStreamProcessor` mirrors this
exactly and has been verified to produce byte-for-byte identical output to a real
Steam save on an unmodified round-trip (`SaveStreamProcessor.WriteSaveContainerAsync`
is the write-side counterpart to `DecompressSaveToStreamAsync`).

## 2. Editing Model: Generic Tree, Not Hand-Modeled POCOs
The hand-mapped classes in `ArtifactX.Core.NmsModels` (`NmsSaveFile`, `NmsUniverseState`,
etc.) only cover a fraction of the save's obfuscated keys. Deserializing into them
and reserializing would silently drop every unmapped key, so they're used only for
lightweight, read-only metadata (e.g. the folder-select slot preview) — never as the
basis for a write.

Actual editing parses the decompressed JSON into a generic `Newtonsoft.Json.Linq.JObject`
tree, so every key survives regardless of whether it's been modeled yet. `SaveSessionManager`
holds the active save's tree plus a **staged edits dictionary**, keyed by JSON path
segments (e.g. `["vLc", "6f=", "wGS"]`). Reads check staged edits first, falling back
to the loaded tree. Nothing is written to disk until a single, explicit commit —
individual edits can be reverted, or all of them discarded, purely in memory.

## 3. Manifest Files (`mf_save*.hg`)
Each save file is paired with a manifest (`mf_save.hg` next to `save.hg`, etc.).
**Verified on Steam:** editing `save*.hg` content and leaving its paired `mf_*.hg`
completely untouched still loads correctly in-game — no manifest regeneration is
implemented or currently believed necessary. This is unverified on other platforms;
see the Platform Support Status table in the root `README.md`.

## 4. Relational Data Layer (ArtifactX.GameData / ArtifactX.Tools.DataCataloger)
This layer is unrelated to save-editing state. It normalizes static item/text/icon
data extracted from the game's own `.pak` files into a local SQLite instance via
EF Core, used for reference lookups (item names, icons) when displaying save
contents — not for holding or diffing the player's save data itself.

## 5. Reward/Entitlement Data — Twitch Drops and Expedition Rewards

`accountdata.hg`'s `B89.<5B` array holds Twitch Drop reward tokens (`^TWITCH_NNN`).
**Confirmed working, with one important precondition:** the Quicksilver Synthesis
Companion only trusts this local array when the game cannot reach Hello Games'
servers — while online, it queries the server directly and won't even show the
"Collect Twitch Rewards" menu option, regardless of local file content. While
**offline**, the game falls back to the local array, and a reward added to it
becomes genuinely claimable. Once claimed (the claim action itself, performed
offline), the reward is written into real save/inventory data and is fully
durable — confirmed to survive reconnecting to the network and a full game
reload.

**Required flow for granting a Twitch reward:** add the token to `<5B`, launch
the game with no network connection, claim the reward at the Quicksilver
Synthesis Companion, then reconnect. This is a real precondition the UI needs
to walk the user through — not just a file edit.

Expedition rewards (`^EXPD_*` entries, found across `B1h`, `fyX`, and `d4U` in
the same file) behave differently and more simply: locking/unlocking them holds
regardless of network state, online or offline, with no special sequence
required.

## 6. Shared Libraries and Future MAUI Helper

The owner plans to add a MAUI helper project to this solution after more NMS
logic is verified (2026-10-27). Keep that future client on the same shared rules
as the desktop app. Implement and maintain portable algorithms in the existing
libraries as features evolve; do not copy them into a second app.

Current project boundaries, verified from the project files:

| Project | Responsibility | Reuse boundary |
| --- | --- | --- |
| `ArtifactX.Core` (`net10.0`) | Save-domain models/path helpers, generation algorithms and validation | Keep APIs independent of WinUI, Windows process access, rendering controls and platform storage paths. Pass explicit inputs and return portable results. |
| `ArtifactX.Almanac` (`net10.0`) | Existing shared ship, multi-tool and freighter reference models | Reuse and extend appropriate reference models; extractable game content still comes from the catalog. |
| `ArtifactX.GameData` (`net10.0`) | Read the player's game files and build the catalog; references Core and vendored libMBIN | Preserve the single extraction implementation. Its image/compression/database native dependencies require platform validation before another client references it. A portable target framework alone does not establish mobile support. |
| `ArtifactX.WinUI3` | Desktop UI, app orchestration and Windows platform integrations | Keep WinUI controls, OCR/capture, game-process reads and desktop installation discovery here or behind platform adapters. Do not make a helper reference this project. |

New procedural work should accept catalog-backed data and explicit runtime
inputs in Core. Desktop services acquire those inputs and adapt results to
view-models. For example, animation timing calculations can be shared, while
reading a running Windows game's memory remains a desktop integration. Unknown
or unavailable inputs must stay explicit rather than being silently replaced
with a desktop assumption in another client.

Before introducing MAUI, establish its target platforms and workflows. Then
identify the portable catalog/query contracts and data transfer requirements
actually needed. Extract shared catalog-reading or application services from
WinUI where justified, keeping PAK/MBIN decoding in GameData. Review native
packages, filesystem access and rendering support for each chosen target;
reuse of a WinUI preview control is not automatically possible in MAUI.
The helper must not require access to a running PC game merely to use verified
portable calculations. Do not assume mobile targets can discover a PC install,
open its process or write its saves.

Shared-library changes should have targeted native/reference or in-game checks
appropriate to the logic. Desktop save writes continue to use staged edits and
the running-game save gate. Any future editing client must preserve those
safety semantics through an explicit editing service rather than bypassing them
with direct JSON mutation. New UI remains in each client's pages/UserControls,
separate from the rules both clients consume.

Backlog dependencies and acceptance criteria are tracked in
[Feature 49](ISSUES_LOG.md#feature-49-shared-libraries-for-a-future-maui-helper-app).
No MAUI project or package upgrade is introduced by this planning update.
