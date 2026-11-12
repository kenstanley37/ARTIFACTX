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

## What's still needed for a prediction

1. The routine that consumes child seed 8 (the planet generator's step after the object step 0x14169bf50, which uses
   child 7) and the exact order in which it walks the placed lists, their chosen option files
   (GcExternalObjectListOptions.Options) and the spawn entries inside (GcExternalObjectList.Objects =
   GcEnvironmentSpawnData), and which entries/variants get a pair.
2. A catalog step for the option files' spawn entries (resource scene, alternatives, quality variants, type), since the
   catalog keeps only each list's name and option count today.
3. Which entries are discoverable flora vs minerals (the discovery type the key builder writes: 8 for this branch),
   then the names (SpeciesNames.Flora / Mineral from the seeds, as on Flora and Mineral record tiles).

Ground truth: the 257 Flora records (address, bEr[0], bEr[1]) are in the scratch file flora_keys.txt (rebuild with
the probe); Mineral records the same way.
