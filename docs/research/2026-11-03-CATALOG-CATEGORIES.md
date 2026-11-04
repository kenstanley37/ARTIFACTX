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
| Technology | per-save `vLc/6f=/4kj` (KnownTech) + the section's 187 not-craftable items; never overlapping | 170 + 187 = 357 (game 357) | 147 + 187 = 334 (game 334) |
| Building Parts | per-save `eZ<` (KnownProducts) + `24<` (KnownSpecials) + the section's 10 not-craftable firework packs (`EXPD_FIREWORK01-04`, `SPEC_FIREWORK01-06`) | 1,011 (game 1,011) | 964 (game 964) |

- The 10 fireworks and the 187 counted technologies are exactly the section's
  products the game data marks not craftable (`GcProductData.IsCraftable` false), so
  known = the save's list + the section's not-craftable items. (Account `B1h` happens
  to hold those same 187 technologies.)
- Greyed Space Station Exterior parts on Slot 1 (`STA_09`, `STA_16`...) are absent
  from `eZ<`; the coloured ones checked (`STA_08`, `STA_23`) are present.
- Materials "seen" (653/653 on both slots) and Recipes (Slot 8 game 21; `Ddk` holds
  28) are not yet matched.

## Recipes (catalog format 63)

The four Recipes categories list no items; the game fills each with the distinct
results of its recipes from `nms_reality_gcrecipetable.mbin`: refiner recipes
(`Cooking` false) by ingredient count 1/2/3, and cooking recipes. 31 + 45 + 23 + 333 =
432, the game's total. A result is known when any recipe making it is in the save's
`vLc/6f=/Ddk` (KnownRefinerRecipes): Slot 1 20 + 9 + 0 + 54 = 83 (game 83), Slot 8
11 + 4 + 0 + 6 = 21 (game 21); Slot 8's 11 simple-refiner results match the owner's
screenshot by name. Write test: the owner learned Gold and Living Slime on the
Catalog page (Slot 8, 21 -> 23), saved, and both showed known in game.

The game's tile order within a category differs from recipe-table order (it starts
Simple Refiner with Carbon, Ferrite Dust, then Nanite Cluster sixth); not yet matched.

## Wonders (catalog format 64)

The game's 428 = 66 records + 219 fishing + 143 fossils; Personal Records' 12 slots
are shown but not counted. Exact on both slots: Slot 1 61 + 219 + 143 = 423 (game
423), Slot 8 45 + 60 + 143 = 248 (game 248).

- Records: fixed-size `GcWonderRecord` arrays (`UqY` GenerationID, `1OQ` value,
  `y87` seen); a slot is recorded when its GenerationID isn't 0. Save keys in libMBIN
  field order: `dgD` Planet (11), `MZ1` Fauna (15), `fp9` Flora (8), `bYs` Mineral
  (8), `e?Y` Treasures (13), `<8Q` Collected Glitches (11), `Svy` Personal (12). Slot
  names are `GcUIGlobals.Wonder*CategoryConfig[].LocID` ("Hottest Temperature").
  Personal names are player-entered (not read yet).
- Fishing Records: 219 tiles on the owner's screenshots = 210 species + 9 flotsam,
  known when caught (`bTf/yv6` count above 0).
- Fossil Records: 143 tiles = the ExhibitBone products minus the `BONE_TEMP` template.
  All 143 count as known on both slots; where that comes from is not found.
- Owner confirmed in the app 2026-11-03: Slot 8 shows Oddities Recorded 248/428 with
  the game's record names (Treasures 4/13, Planet 11/11, Fishing 60/219, Fossils 143/143).
  Planet Records' planet names match the game's tile by tile; Fishing (fish-table order,
  outlines) and Fossil Records match its screens. Fauna and Flora are recorded in the
  same slots, but the game names the species, which isn't generated yet.

## Write test (2026-11-03)

On Slot 8 the owner ticked greyed Titanic Skull (`STA_09`) on ArtifactX's Catalog
page (staged into `eZ<`), saved with the game closed, and loaded the save: it showed
coloured under Space Station Exterior. Editing `eZ<` unlocks building blueprints.

## Still open

- Materials seen and Collected Knowledge sources; where fossils' known state comes from.
