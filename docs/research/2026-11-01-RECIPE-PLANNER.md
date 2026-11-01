# Recipe planner implementation and validation

Implemented on `feature/recipe-planner`, replacing the Recipes / RecipeTree
navigation pair with one cached `RecipesPage`. The old tree page, requirement
card and presentation models were removed. Crafting remains a separate feature;
it expands buildable product/technology requirements rather than processor recipes.

## Structure

- `ArtifactX.Core/Recipes/RecipePlan.cs`: bounded dependency planning. Shared
  demands are combined before rounding batches, with one step per output.
- `ArtifactX.Core/Recipes/FoodIngestorBonus.cs`: portable nominal food bonus calculation.
- `ArtifactX.GameData/CatalogSteps.FoodIngestor.cs`: extracts the food definitions,
  DefaultReality stat ranges and GameplayGlobals duration into `FOODINGESTOR`.
  Catalog format is now 55. No reference CSV or game images are shipped.
- `RecipePlannerCatalogService`: read-only catalog and exosuit cargo access.
- `RecipePlannerViewModel` and its Plan partial: filters, presentation and navigation.
- Browser, Card, Details and Map UserControls: XAML presentation. The Map's code
  draws only connector geometry behind measured cards.

The results list collapses after selecting a recipe. Navigation stays above the
workspace; map, gathering and detail areas scroll separately. Quantity and recipe
choices survive branch navigation. Existing pins are preserved and editable.
The app-wide snapshot now includes exosuit cargo; inventory updates change only
existing gathering-row properties. No new save watcher or save edits were added.

## Food bonus evidence

On 2026-11-01, inspected native helper `0x140ed70f0..0x140ed72af` in the installed
executable. It looks up ProductData, rejects an invalid stat and returns a positive
explicit amount when available. Otherwise it interpolates DefaultReality's
FoodStatValues using `(CookingValue - 0.1f) / 0.9f`, then applies a deterministic
product-ID factor. The factor hashes all 16 zero-padded ID bytes with FNV-1a,
folds to 32 bits, runs the native mix and scales to 1..1.1. Float operations stay
separate, matching the SSE instruction sequence.

`scripts/research/food-ingestor-oracle.py` executes that native helper under Unicorn
with decoded product and range inputs. Product lookup is the only intercepted call.
All **586** installed food definitions matched the Core port's float bits exactly:

```powershell
dotnet run --project tests/ArtifactX.Core.Checks -- --food-bonus-vectors <local-native-vectors.json>
```

These are the nominal values shown for foods, not the final effects on the
player's equipment. Duration is derived from extracted BaseFoodDuration (600 in
the inspected build) multiplied by each product's CookingValue. The native amount
oracle does not independently verify runtime consumption timing.

## Other checks

- `--recipe-catalog-vectors`: all **1,684** catalog recipes produce finite,
  consolidated, dependency-ordered plans.
- Synthetic checks cover combined demand before batch rounding, quantity scaling,
  circular starting supply and cancellation.
- Core.Checks default suite passed.
- GameData.Checks `--food-catalog-check <PCBANKS> <new-scratch.sqlite>` exercised the
  actual catalog build and extracted 586 definitions plus duration. The installed
  catalog and user database were not modified by this check.
- WinUI build is checked with `dotnet build src/ArtifactX.WinUI3 -r win-x64`.

The app was not launched, per the owner's rules. Remaining owner verification:
rebuild in Visual Studio, update game data once, then check recipe selection,
effect ranking, quantity/alternative changes, long-map navigation and both themes.

## Separate cooking and refining entry points

Follow-up on `feature/recipe-categories`: `CookingRecipesPage` and
`RefinerRecipesPage` are thin cached wrappers around `Controls/RecipePlanner`.
The generic `RecipesPage` was removed. Each wrapper owns a separate ViewModel
mode, keeping filters, selections and scroll positions independent.
Cooking browses only Cooking outputs and keeps the Ingestor features; its route
may still include a necessary refining step for an ingredient. Refiner Recipes
browses Refiner outputs, plans with Refiner recipes only and hides food effects.
The installed catalog already contains 1,323 cooking and 361 refiner recipes;
all 361 refiner-only routes passed the existing route checks. No catalog format
change is needed for this split.

## Ingredient alternatives audit

Read-only inspection of the installed catalog on 2026-11-01 found six Refiner
recipes producing Nitrogen (`GAS3`): REFINERECIPE_49, _133, _136, _378, _380
and _381. The catalog contains these alternatives. This establishes their
presence, not completeness against every current game table.

The planner's acyclic ranking can correctly stop gas conversions at a starting
supply, but the presentation previously populated choices only for planned
steps. Starting supplies therefore lost their existing recipe alternatives.
Ingredient details now obtain alternative cards directly from the mode's entire
recipe set, independently of the selected route. Selecting a conversion that
revisits an ancestor stops at a required starting supply and explains the cycle.
Alternatives remain choices rather than additional simultaneous requirements.

Verification: synthetic chosen-conversion and closed-cycle checks passed;
all 1,684 installed catalog routes remained finite, consolidated and dependency
ordered. WinUI build passed with zero errors. No app or game test was performed.

The approved broader acquisition work remains: inventory totals by cargo
location with shared-storage deduplication, planet-type source data, confirmed
base extractor resources/production, and manual inventory/base records for
save-free mobile use. Existing planner inventory still counts exosuit cargo
only; these alternatives do not imply that storage or extractor work is done.

## Cargo supplies and substance sources

The next increment adds portable `Core/Recipes/CargoSupplies`: exosuit,
owned starships, freighter, Exocraft, the first ten shared Chest containers,
ingredient/cold storage, dismantled-base and Corvette workshop caches. It
uses the confirmed paths documented in NmsInventoryContainer, includes only
Product/Substance slots, and leaves unknown mirrors out. Read-only inspection
of the Slot 8 Working save reconfirmed storage ordering and the distinct
ingredient/cold containers. No save or user database was modified.

LiveSaveReader builds the supply list on its existing background read; no new
watcher is created. Planner fallback scans run on Task.Run. Session and request
identity checks discard stale async results, and inventory strings update on
existing card objects without rebuilding the map. The ingredient details show
totals plus location names; remote storage is not claimed to be immediately
accessible. Save-free use retains the recipe/source information without counts.

The missing substance descriptions were a category-filter bug:
`GcRealitySubstanceTable` was used where the catalog stores `GcSubstanceTable`.
Read-only catalog checks confirmed ROCKETSUB's asteroid-mining guidance and
GAS3's Lush/Toxic atmosphere description now pass the corrected query. These
are game descriptions, not a complete structured planet/source model.

Core checks passed, including storage cap/deduplication, named ship ownership,
technology exclusion, missing-save behavior and input immutability. WinUI build
passed with zero errors; owner app testing remains. Base extractor contents,
resource type and production are not implemented yet, and manual mobile
inventory/base records and structured source rules remain future work.

Read-only inspection of the Slot 1 Working reference save found 25 matching
extractor/silo object records under base `@ZJ`, identified by their `r<7` part
IDs (including U_EXTRACTOR_S and U_SILO_S). Those records expose placement and
object data, but that inspection did not establish a resource type, power state,
production rate or stored quantity. Do not use part presence alone to claim a
base supplies a particular ingredient. A fresh-save controlled extraction test
is still needed to map and verify those fields.

## Expand conversion families to their cycle boundaries

The finite-rank route preference remains for recipes with acyclic preparation
paths. When a conversion family has no such rank, the planner now chooses a
recipe and expands it until an actual ancestor repeats, rather than treating
the entire family as gathered ingredients. Explicit alternatives still win.
The repeated input is a terminal starting supply with its own required amount;
it does not become a dependency on the operation that will produce it later.

Core returns parent-specific circular supplies. The map gives those supplies
distinct node keys while retaining their real item IDs for names, inventory
and alternatives. This keeps connectors from pointing backwards to the result
card. Inventory remains informational and never prunes preparation recipes.

Verified against 2,045 installed-catalog routes (1,684 combined plus 361
refiner-only): bounded unique operations, dependency order and total supply
covering all consumed inputs plus the requested result. The Activated Quartzite
route in the reported case expands from one to nine operations, with three
circular supplies; the original Aronium route retains 13 operations. Synthetic
checks cover automatic and explicitly chosen cyclic gas conversions. No app
was launched; owner verification of map/step-order and branch navigation remains.

## Creature ingredient sources

The owner's Soiled Soup example exposed a separate acquisition gap: the item
description for Dirty Meat is generic and does not identify the source animal.
The installed GCCREATUREGLOBALS.MBIN KillingProducts list maps FOOD_M_DIGGER to
PLOUGH and PROTODIGGER. Joining the installed creature data table confirms their
structural kinds and Ground move area. No particular planet biome restriction
was established by this mapping; the UI must not invent one.

Catalog format 56 adds CREATUREPRODUCTS, containing the direct hunting and
harvesting mappings joined to structural creature kinds and habitats. The
player-facing helper translates supported structural kinds into plain source
labels and never shows the internal creature IDs. Dirty Meat now directs the
player to hunt burrowing ground creatures. Existing item descriptions remain
below the source hint. Other creature products distinguish hunting from feeding
and collecting produce. The raw MinBlobs value is retained for future study but
is not described as a physical animal-size restriction without evidence.

The actual extraction check built a new scratch catalog and verified 103
creature-product rows, including both Dirty Meat hunting sources and their
ground habitats. The player's installed catalog and user.db were not patched.
One normal game-data update is required after rebuilding the app. In-app and
in-game source hunting checks remain with the owner. Hotspot resource identity
investigation is still separate and remains unfinished.
