# In-game Catalog sections, categories and known counts

Build 184271, investigated 2026-11-03. Read-only: game PAKs, the owner's account
`accountdata.hg`, and ArtifactX's decrypted Working copies of Slot 1 (main) and
Slot 8 (DELETEME). Nothing was written.

## Categories come from data, not code

The Catalog & Guide screen's sections are `GcWiki` files under `METADATA/REALITY/`.
Each `GcWikiCategory` holds an explicit `Items` list (ids) or, for record-type
categories, a `Type` that the game fills at run time.

| File | Section | Categories | Items | In game |
|---|---|---|---|---|
| `cataloguematerials.mbin` | Materials & Items | 9 (`UI_PORTAL_CAT_MAT_*`: Raw Materials - Core 17, Raw Materials - Exotic 42, Fuel & Utilities 55, Crafting Components 42, Exotic Goods 42, Curiosities 31, Trade Goods 43, Plants 28, Cooking 353) | 653 | 653 |
| `cataloguecrafting.mbin` | Technology | 7 (`UI_PORTAL_CAT_TECH_*`: Exosuit 59, Starship 78, Multi-Tool 23, Weapons 54, Freighter 39, Exocraft 78, Exotic 27) | 358 | 358 |
| `cataloguebuilding.mbin` | Building Parts | 10 (`UI_PORTAL_CAT_BLD_*`: Basic Modules 192, Buildable Technology 46, Core Structures 44, Freighter Construction 31, Power & Industry 20, Farming 15, Decoration 256, Posters & Decals 156, Exotic Parts 165, Space Station Exterior 99) | 1,024 | 1,024 |
| `cataloguerecipes.mbin` | Recipes | 4 by type (RecipesRefiner1/2/3, RecipesCooker) | - | 432 |
| `cataloguewonders.mbin` | Wonders | 9 by type (Treasures, Collected Glitches, Planet/Fauna/Flora/Mineral/Personal/Fishing/Fossil Records) | - | 428 |
| `catalogue.mbin` | (older top-level groups: Raw Materials, Crafted Products, Equipment...) | 9 by type | - | - |

The `UI_PORTAL_CAT_BLD_*` keys do not appear in NMS.exe at all; only the
technology and top-level keys do (`0x1406c9469` etc. reference `UI_PORTAL_CAT_TECH_*`).
Collected Knowledge (`UI_PORTAL_CAT_STORIES`, referenced at `0x1408e1e04`) has no
catalogue file and is not yet mapped.

## Which lists the known counts read

Counts below are the catalogue file's items found in each list; "in game" is the
owner's Catalog screen for that slot.

| Section | Rule (matches both slots) | Slot 1 | Slot 8 |
|---|---|---|---|
| Technology | per-save `vLc/6f=/4kj` (KnownTech) + account `B89/B1h`; the two never overlap | 170 + 187 = 357 (game 357) | 147 + 187 = 334 (game 334) |
| Building Parts | per-save `eZ<` (KnownProducts) + `24<` (KnownSpecials) + the 10 firework packs (`EXPD_FIREWORK01-04`, `SPEC_FIREWORK01-06`) | 1,011 (game 1,011) | 964 (game 964) |

- The fireworks are never in `eZ<` yet count on both saves. All 10 are in account
  `B1h`, but B1h holds all 1,024 building parts, so B1h is not the general rule.
  Why fireworks count is **inferred, not confirmed**.
- Account `B1h` holds 187 of the 358 catalogue technologies and `>Qn` the other 171
  (disjoint, union 358). Only B1h's share counts toward the known total.
- Greyed Space Station Exterior parts on Slot 1 (`STA_09`, `STA_16`...) are absent
  from `eZ<`; the coloured ones checked (`STA_08`, `STA_23`) are present.
- Materials "seen" (653/653 on both slots) and Recipes (Slot 8 game 21; `Ddk` holds
  28) are not yet matched.

## Still open

- A controlled Slot 8 write test: add one greyed building part to `eZ<`, save,
  confirm it shows in the in-game Catalog. Until then editing `eZ<` for building
  parts is supported by the count match only.
- Materials seen, Recipes, Collected Knowledge and Wonders sources.
- How the firework packs become known.
