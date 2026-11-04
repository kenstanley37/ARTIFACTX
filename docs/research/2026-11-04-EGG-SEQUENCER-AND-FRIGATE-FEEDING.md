# Egg Sequencer and organic frigate feeding (2026-11-04)

Static disassembly of NMS.exe build 184271 (capstone, nothing run). Scripts were in the session scratchpad (`eggre/`, `frigre/`). Nothing here has been compared with the game yet.

## Egg Sequencer: solved, ported to `ArtifactX.Core.Companions.EggSequencer`

The full rules are in the class doc comment. In short:

- **Slots.** `0x1410d6400(this, slot)` returns the catalyst in each slot: 1 Growth Hormone, 2 Gene Splitter, 3 Dye Injector, 4 Neural Calibrator.
- **Dose.**
  - `dose = min(count * value, full) / full`.
  - `value` is the item's `BaseValue`. Substances are multiplied by `PetEggSubstanceModifier` (2.5). The trait table's `BaseValueOverride` replaces `BaseValue` when it is above 0; every row is -1.
  - `full` is the `BaseValue` of `PetEggMaxChangeProduct` (Sac Venom, 64,000).
  - The screen clamps the count to `PetEggModificationItemLimit` (999) at `0x140784c59`, so cheap items never reach a full dose. A full slot of Carbon is about a 47% dose.
- **Item hash.**
  - Only the first 8 bytes of the item id (an inline 16-byte string) are used.
  - The hash is `x = a * 0x9ddfea08eb382d69; x ^= x >> 47; x *= same`, then StartState.
- **Neural Calibrator** (`0x1410d7d70`).
  - If the item is in `peteggtraitmodifieroverridetable.mbin` (24 rows), that row's trait and direction are used.
  - Otherwise: first word → `trait = (w * 3) >> 32`; second word as a fraction ≥ 0.5 → raises the trait.
  - Change = `min(TraitRangeModifier * 2 * dose, TraitRangeMax)` = up to 0.2, which is 20 points on screen. The result is clamped to -1..1.
- **Growth Hormone** (`0x1410d7b50`).
  - First word's fraction ≥ 0.5 → bigger.
  - Change = `min((max - min) * ScaleRangeModifier * dose, ScaleRangeMax)` within the species' range. The range comes from the species override table, otherwise from the creature data.
- **Gene Splitter / Dye Injector** (`0x1410d7fc0`, `0x1410d6a10`).
  - The chance is `min(dose * 1 * 4, 1)`, rolled from the game's running random state, so it can't be predicted.
  - The Dye Injector's new colour seed is the item hash, so the same item always gives the same colours.

**Test in game (no save edit needed).** Put an item that isn't in the table into the Neural Calibrator, for example Silicon Egg (`FOOD_P_STELLAR`). The app says it lowers Aggression and makes the creature smaller. The sequencer's Embryo Status should show Aggression "Decreasing".

## Organic frigate "Feed Vessel": mechanism read, predictor not built

The feed is `0x140533070(manager, frigate, item)`. It is called from the fleet UI at `0x1407e1d9f`. A false return shows `UI_FRIGATE_ERROR_SEED_OSD` ("Insufficient nutrient variety").

1. **Seed.** It is a CityHash-style 128→64 mix of the item id's first 8 bytes and the frigate's seed at `frigate+0x80`.
2. **Trait reroll.**
   - All traits are removed, keeping the count `n` (up to 5 slots at `frigate+0..0x20`).
   - The primary trait comes from the class table (`r12+0x1b0 + class*0x68`). Class 7 (DeepSpaceCommon) instead picks from `DeepSpaceCommonPrimaryTraits`.
   - The other `n-1` traits come from `0x14052e3b0`. That function gets a flag: the first word is odd and the class is not 7.
3. **Result.**
   - If the new set equals the old one, the old traits are restored and the feed is refused.
   - Otherwise the new seed is stored at `frigate+0xa0`.
4. **Stats.** Each trait adds its strength to `frigate+0x104 + stat*4` (from the trait-strength table at `r12+0x648`).

**So there is no "food X buffs stat Y" table.** The outcome depends on the item and on that frigate's own seed.

A per-frigate predictor ("feed this frigate X and it gets these traits") needs three more things:

- the trait picker `0x14052e3b0`;
- which save field is `frigate+0x80`;
- whether feeding consumes the item, and how many.
