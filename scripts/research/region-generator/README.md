# Region generator research scripts

Research tools for reverse-engineering No Man's Sky's region star generator (build 180836).
Findings, addresses and validation results are in
[docs/research/REGION_STAR_COUNT.md](../../../docs/research/REGION_STAR_COUNT.md).
These scripts are not part of the app.

## Setup

- Python 3 with `unicorn`, `capstone`, `pefile` and `numpy` (`pip install unicorn capstone pefile numpy`).
- `NMS_EXE`: path to `NMS.exe` (defaults to `D:\SteamLibrary\...\Binaries\NMS.exe`).
- `NMS_EMU_DATA`: a folder holding game files extracted from your own install, which the
  emulator loads: `biomelist.mbin`, `biomefiles.mbin`, `solargen.mbin`, and a `globals\`
  folder with every `*.global.mbin`. **Never commit these:** they are game data
  (`*.mbin` is git-ignored).

## Contents

| Script | Purpose |
| --- | --- |
| `emu/emu_sys.py`, `emu/globals.py` | Unicorn harness that maps NMS.exe and loads the globals at their runtime addresses (written by Codex). |
| `regionemu.py` | Runs the region generator `0x141367810` for one region and lists the emitted systems. |
| `regionbatch.py` | Emulates every region in a decrypted save's discovery records and checks visited indices. |
| `regionreads.py` | Lists startup state the generator reads that the file leaves zero. |
| `regionlivecheck.py` | Compares the emulator with `live.json` from `scripts/diagnostics/read_region_star_counts.py`. |
| `regiontruth.py` | System indices in one region from a save's discovery records. |
| `segport.py`, `segbatch.py` | Python port of the galactic path segment builder, diffed against the emulator. |
| `fixedport.py` | Python port of the fixed-star random-draw consumption, diffed against the emulator. |
| `regionpositions.py` | Dumps one region's stored stars (index and position) from the emulator; `POSITIONS_OUT` appends them as JSON lines for `RegionCheck ordinary`. |
| `spiraltrace.py` | Logs the spiral-arm step for one region: setup frame, then each star's inclusion inputs, arm angle, arm point and final position. |
| `bsspeek.py` | Prints runtime (BSS) vectors after the startup initializers. |
| `RegionCheck/` | C# checks of `ArtifactX.Core` against emulator output: `ordinary <jsonl>` (positions), `neighbours x y z Name`, `spiralfind galaxy x y z`. |
| `regionport.py` | Random-stream checkpoints between generator stages. |
| `segconsts.py`, `noisetest.py` | Constant/jump-table dump and direct noise-routine test. |
| `static/` | Static helpers: disassembly, call sites, rip-relative references, byte search, `.pdata` bounds, data peeks. |

Run with plain `python` (not `-I`): the packages are usually user-site installs.
