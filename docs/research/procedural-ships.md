# Ship previews from seeds: research notes

Goal (wish list #10): show what a starship would look like for a seed, with a
"reroll" button, before writing the seed into a save.

## What the save stores

Each ship (`@Cs[i].NTx`) stores only:

- `93M`: the model scene, e.g. `MODELS/COMMON/SPACECRAFT/FIGHTERS/FIGHTER_PROC.SCENE.MBIN`
- `@EL[1]`: the 64-bit seed, e.g. `0x1CD40D0620985D13`
- `QlJ` / `<d2.bnT`: always empty on every ship in the test saves (129 ships)

So the parts are never stored. They are worked out from the seed each time
the game loads the ship.

## How a ship is made of parts (from the game files)

Every procedural model has a `*.DESCRIPTOR.MBIN` next to its scene
(`TkModelDescriptorList`): a list of groups, each with options; the game
picks one option per group. An option can have nested groups (`Children`),
and the part scene it points to has its own descriptor with more groups.

Fighter (`FIGHTER_PROC.DESCRIPTOR.MBIN`), top level:

| Group | Options |
|---|---|
| `_ENGINE_` | B, C, D |
| `_WINGS_` | A, B, D, E, F, G, H, I, J, K (most with an `_ACC_` accessory group) |
| `_COCKPIT_` | A, B, D, E, F (A has 5 `_ANOSE_` noses) |

Every `Chance` in the fighter descriptor is 0, which suggests equal odds.

`DataCataloger descriptors <path-substring> <out-folder>` exports these trees
as JSON (226 files under `models/common/spacecraft/`).

## The open question: seed to picks

No public description exists. Community ship builders (nms.center, an NMS
Discord's "brute force generator") appear to have solved it, but keep it
closed.

The game's random generator is already known: the multiply-with-carry stream in
`ArtifactX.Core/Universe/UniverseRandom.cs` (from nms_namegen), used for
systems and planets. Working guess: a ship uses the same generator, seeded
from the ship seed, taking `Next(optionCount)` per group in file order.

Candidate rules being tested:

- **A** seed as-is, nested groups picked straight away (depth-first)
- **B** low 32 bits of the seed only, depth-first
- **C** seed hashed first (`UniverseHash.IndexPrimed`), depth-first
- **D** seed as-is, all top-level groups first, then nested ones

## Test 1 (in game)

`ship-seed-test-Program.cs.txt` picked a reference seed and four test seeds.
Each test seed matches the reference's engine + wings + cockpit **only** if
its rule is right (random chance of a false match is about 1 in 150).

| Ship | Seed | Same shape as the reference only if |
|---|---|---|
| Reference | `0x1CD40D0620985D13` | |
| Test 1 | `0x532C5818745C7027` | rule A |
| Test 2 | `0x598D948631F83F66` | rule B |
| Test 3 | `0x5A2C132A1CA08649` | rule C |
| Test 4 | `0x75B56C831AB584D1` | rule D |

All five as **Fighter** type. If no test ship matches, none of the rules is
right, but the screenshots still become ground truth for the next round.

### Result (2026-10-14)

Screenshots (top and rear) of the five ships: **Test 1 matches the
reference** (twin round engines, long multi-pane canopy, swept wings with a
tall fin). No other test ship does. Only rule A predicts that:

| Ship | Rule A prediction |
|---|---|
| Reference | `_ENGINE_D _WINGS_F _COCKPIT_A` |
| Test 1 | `_ENGINE_D _WINGS_F _COCKPIT_A` |
| Test 2 | `_ENGINE_D _WINGS_J _COCKPIT_E` |
| Test 3 | `_ENGINE_C _WINGS_I _COCKPIT_E` |
| Test 4 | `_ENGINE_D _WINGS_F _COCKPIT_E` |

Test 2 and Test 3 also look alike from behind (same boxy rear, both
`_COCKPIT_E` under rule A), but their canopies differ from above and Test 4
(also `_COCKPIT_E`) doesn't obviously share that rear - so which visible
piece each group controls isn't pinned down yet (canopies may be nested
picks). Rule A is the working rule; a second, sharper
round (exact twins including the nested groups) should confirm it before any
UI is built on it.

## Test 2 (in game)

Rule A, every pick in the fighter descriptor (nested ones too). Reference's
full picks: `_ENGINE_D _WINGS_F _ACC_NONE3 _COCKPIT_A _ANOSE_A`.

| Ship | Seed | Rule A picks | Expected |
|---|---|---|---|
| Twin A | `0x5E060BD87F938EEA` | same as reference | identical shape |
| Twin B | `0x57A61A7D596EFBBA` | same as reference | identical shape |
| Engine only | `0x7E5A910663656BA4` | `_ENGINE_C`, rest the same | only the engine differs |
| Wings only | `0x53A1D1F248985B29` | `_WINGS_E _ACC_NONE2`, rest the same | only the wings differ |

The part scenes have descriptors of their own (not modelled yet), so small
details could still differ between twins; the big shapes should not.

## After the rule is known

Showing the result needs the part models. Options, cheapest first:

1. Text/diagram preview ("Engine D, Wings F, Cockpit A") with reference
   pictures of each part.
2. Pre-rendered picture of each part (from the game's geometry), layered.
3. Real 3D rendering of the ship (geometry + textures + colour palette from
   the seed). Biggest job; best result.
