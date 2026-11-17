# Planet flora (and minerals): what a discovery record holds (2026-11-12)

Goal: predict a planet's flora species (names, count) the way fauna is predicted (docs/research/2026-11-11-PLANET-FAUNA.md),
so Planet Details can show undiscovered plants and minerals and the totals ("x / y flora").

## The discovery key (NMS.exe 184271 0x14044e1e0)

The same routine builds every discovery key. For a planet object (not a creature) it stores two values in `bEr`:

1. **bEr[0]**: the object's seed (node data `FUN_141880890(node) + 0x10`, sometimes mixed through Hash128 with another
   value at +0x128 for one branch, type 8 = Flora).
2. **bEr[1]**: the djb2 hash (h = 5381; h = h * 33 + signed char) of the object's resource name, i.e. its **scene
   path in capitals** (`MODELS/PLANETS/BIOMES/COMMON/PLANTS/MEDIUMPLANT.SCENE.MBIN`).

Checked on the owner's seven Steam saves (read-only): **257/257 Flora and 298/298 Mineral records** have bEr[1] =
djb2 of a scene path in the game's PAKs (`DataCataloger listpak NMSARC` lists them; scratch flora_probe2.py).

## Where bEr[0] comes from

All **257/257** Flora bEr[0] values are 64-bit seeds of the planet's **child seed 8** stream
(`PlanetProfileGenerator.ChildSeeds(planetSeed)[8]`, StartState, then pairs of draws `lo, hi` mixed with Mix64): the
N-th pair, N from 1 to about 50 on these planets (fauna-check `FLORASEED=<file>` mode, 2026-11-12).

- One seed can carry two scenes on one planet (13+ cases), so a seed belongs to something that places several models:
  most likely one spawn entry (GcObjectSpawnData: Resource + AltResources + QualityVariants) or one placed list.
- The same scene on one planet can carry several seeds (50 of 186 planet/scene pairs): several entries place the same
  model.

## The routine (NMS.exe 184271, Ghidra 2026-11-12)

- Planet data top routine **0x1416c5a40** reseeds the generator stream (+0xBA4/+0xBA8, StartState) from each child
  seed (16-byte slots from local_248: slot 0 life 0x1416cc2e0, 4 terrain 0x1416cc040, 12 weather 0x1416cd3a0,
  19 resources 0x1416cda00, ...) and, for **slot 8**, calls **0x1416d3b70**(gen, planet, input+0x3C20, +0x3C10,
  +0x3C00, +0x3BF0) when the input data is present.
- 0x1416d3b70: **0x1416d3300** gathers the planet's placed object lists (planet data +0x3210, count +0x3218, in the
  object step's placement order): for each, the chosen option file (GcExternalObjectList), loaded and filtered per
  sublist by **0x1416d3210** with the graphics level ("ULTRA" when setting 22 == 3, else "STANDARD" - quality variants;
  check whether this changes entry counts). Then one MWC step, then for each file the four sublists in this order:
  DetailObjects (+0x10), Objects (+0x40), Landmarks (+0x30), DistantObjects (+0x20), each through **0x1416d3f60**.
- 0x1416d3f60 (one sublist): one pair -> Mix64 -> the sublist seed S. Per spawn entry (GcObjectSpawnData, 0x168):
  one pair -> the entry seed; if the entry's Seed.UseSeedValue (+0xF8), Hash128-style mix of S with Seed.Seed (+0xF0)
  instead. Per AltResource of the entry (+0x90, 0x48 each): one more pair (same override rule) -> that resource's seed.
- So a plant's bEr[0] is its entry's (or alternative resource's) seed. The empirical pair index (FLORASEED) and this
  draw pattern should line up once the files' entry/alt counts are known; the one-step before the loop vs the
  harness's pairing from StartState still needs checking against the data.

## What's still needed for a prediction

1. The object step port (PlanetProfileGenerator.Objects PlacedHints) must keep which option file each placed list
   chose (its "model" draw: index = draw * Options >> 32) and the placement order.
2. A catalog step for the option files' spawn entries (resource scene, alternatives, quality variants, type), since the
   catalog keeps only each list's name and option count today.
3. Which entries are discoverable flora vs minerals (the discovery type the key builder writes: 8 for this branch),
   then the names (SpeciesNames.Flora / Mineral from the seeds, as on Flora and Mineral record tiles).

Ground truth: the 257 Flora records (address, bEr[0], bEr[1]) are in the scratch file flora_keys.txt (rebuild with
the probe); Mineral records the same way.
