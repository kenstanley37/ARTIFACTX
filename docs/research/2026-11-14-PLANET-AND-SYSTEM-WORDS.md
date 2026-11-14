# Planet and system words (2026-11-14)

Answer key: the owner's user.db (captured planet descriptors and weather words, recorded system words).
Harness: `scripts/research/descriptor-check` (read-only):

```
dotnet run --project scripts/research/descriptor-check -- <nms_catalog.sqlite> <user.db> [misses | weather <biome> [types] | families]
```

Set `BYBIOME=1` with `families` to key the table by biome and subtype.

## System economy, wealth and conflict words (#66)

Solved: `ArtifactX.Core.Universe.SystemWords` (NMS.exe 184271 `0x141687890`). One draw seeded from the universal
address picks all three words; 8 words per wealth/conflict level, 4 per economy, and one screen (`0x141388160`)
always shows word 1. Matches every system read from the save (wealth 61/61, conflict 58/58).

The "level gap" (about 10% of captures disagreeing with the generated levels) was misfiled captures: their
address-only word numbers matched 8/43 (chance), against 145/150 for the rest. Readings that contradict the
generator are now skipped (`UniverseInfo.GameCouldShow`).

## Planet descriptors

571 of 612 captured descriptors match after the exotic-world fix (`PlanetProfileGenerator.DescriptorWords`): the
exotic word lists follow GcBiomeSubType from Structure = 3, not 0 (57 misses before). The other 41:

- about 29 are misreads (cut-off words, "UNMARK PLANET", "Hold E to Tag") that match when read whole;
- 11 are exactly another body's generated word in the same system: readings filed on the wrong planet (including
  the dead moon captured as "Paradise Planet" and the "[REDACTED]" planet). The planet reader now skips those
  (`PlanetPageReader.OtherBodysReading`);
- 1 is not a descriptor ("Salvageable Scrap").

## Weather words

The generator's weather type is the game's internal one (the storm odds follow it; emulator 807/808), but the words
the game shows follow the biome more than the type. `PlanetProfileGenerator.WeatherWordFamily` (measured, not
ported) fits 1,038 of 1,054 captured words. Lush worlds with corrupted sentinels get the Weird type but humid
words; Bubble weather always shows GREEN words. The 16 misses are dead worlds showing BARREN words; not explained.
The app's plain weather label now follows the family.

Open: star colour from captured star classes disagrees with the generator on 16 of 128 otherwise good captures.
