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
