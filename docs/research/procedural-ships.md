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

## After the rule is known

Showing the result needs the part models. Options, cheapest first:

1. Text/diagram preview ("Engine D, Wings F, Cockpit A") with reference
   pictures of each part.
2. Pre-rendered picture of each part (from the game's geometry), layered.
3. Real 3D rendering of the ship (geometry + textures + colour palette from
   the seed). Biggest job; best result.
