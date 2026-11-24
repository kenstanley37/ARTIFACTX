# Station trade terminal stock (issue #9)

2026-11-24. What a space station's trade terminal sells is fully generated from the system address and game
files. The port reproduces every captured item for 159 of 170 non-pirate stations; the other 11 captures
contain planet trading-post items, so those screen reads were misfiled (details below). No game or save writes.

Harness: `scripts/research/trade-check` (read-only; ground truth = `market_items` in the pre-migration-22 user.db
backup, because migration 22 dropped the captured stock). Ghidra project: build 184271.

## The chain

1. **Terminal position.** `MODELS/SPACE/SPACESTATION/SPACESTATIONTYPEB.SCENE.MBIN`, every launch tube
   (A/B/C) -> `_BackSectionREF_` -> `REFTerminalShop1` -> `.../DOCK/SHOPS/TERMINALSHOP.SCENE.MBIN`, attachment
   `STATIONBTERMINALSHOP.ENTITY.MBIN` with `GcInteractionComponentData` type `Shop`. Station-local position
   (-5.0109487, 1.875, -942.5), the same in all three tubes. World position = the station pose
   (`StationPoseGeneration.GenerateForSystem`) applied as in `StationCabinetLocations` (right/up/forward + position).
2. **Interaction seed.** The interaction's seed is the hash of its world position (`MultiToolSiteGeneration.SeedForPosition`,
   NMS.exe `0x140504eb0`): the same rule as the multi-tool cabinets. Getter `0x1404594f0` reads it from the
   interaction component (+0x48, override +0x68).
3. **Stock seed.** Caller `0x14085d720` Mix64-es it (`0x64DD81482CBD31D7`, `0xE36AA5C613612997`) and calls the
   generator `0x14087b4d0` with it. The RNG starts like every other generator: s0 = low word (1 if 0),
   s1 = rol16(low) ^ high ^ low, step v = s0 * 0x5A76F899 + s1.
4. **Which list.** `0x14087ae50` picks the `GcTradeData`: a station terminal -> `TradeSettings.SpaceStation`; planet
   shops -> `Shop`; pirate systems -> `SmugglerStation`/`PirateTech` and others by interaction type and the
   system's pirate flag (+0x2e17).

## The generator (`0x14087b4d0`, Shop interaction, normal game)

Wealth `w` = system +0x2524 (Poor/Average/Wealthy/Pirate), trading class = system +0x2520 (the generator's raw
economy draw, `GcTradingClass` order). Every "draw" is one RNG step; ranges are `((max-min+1) * draw >> 32) + min`.

1. count = range(MinItemsForSale, MaxItemsForSale); products = clamp(trunc(count * PercentageOfItemsAreProducts),
   0, always+optional products); substances = clamp(count - products, 0, always+optional substances).
2. Each `AlwaysPresentProducts` entry: one draw (stock amount, Min/MaxAmountOfProductAvailable[w]); added unless in
   `NeverSellableItems` or not offered (below); products -= 1 either way. `CHART_SETTLE` is skipped when a feature
   flag (`0x1403f43b0`) is off.
3. Each `AlwaysPresentSubstances` entry: the same with the substance amounts; substances -= 1.
4. Optional products: Fisher-Yates shuffle of the indices from the end (draw per swap, j = draw * (i+1) >> 32), then
   the first `products` indices: skip if already on sale (no draw); else one draw, then skipped if Category is
   BuildingPart (8), never sellable or not offered.
5. Trade goods (Shop interaction, not pirate): extra = range(MinExtraSystemProducts[w], MaxExtraSystemProducts[w]).
   Then every entry of the three product tables in order (`ProductTables`: Main, BaseParts, ModularCustomisation):
   skip if already on sale (no draw); else one draw; added while extra > 0 if TradeCategory equals the trading
   class's `Sells` (and the same Category/never-sellable/offered checks). In practice the first N `TRA_*` goods.
6. Optional substances: shuffle as in 4, then the first `substances` the same way as products.

"Offered" (`0x140887450`): not in `NeverOfferedForSale`, not in the difficulty preset's shop ban list
(`GcItemShopAvailabilityDifficultyOption`), and in expeditions not in a season list. The harness only checks
`NeverOfferedForSale` (normal difficulty bans nothing that a station sells, as far as the captures show).

Step 5 draws once per product in all three tables. **The draw count depends on the game build's table sizes**, so
the catalog must use the player's installed tables, never a fixed number.

## Verification

- With the right draw count, all 159 exact stations predict every captured item (always items, the optional
  product picks, the 1-5 trade goods, the optional substance picks).
- The draw count fitted per station (`sweep`, `PER=1`) is exactly `entries - products already on sale`:
  captures before 2026-10-28 fit 226 fewer entries (an update added 226 products); captures from 2026-10-29 to
  2026-11-13 fit one more entry than the 185034 install has now. Every station of a build fits the same table size.
- Not exact (11): the earliest captures (2026-10-17/18 and a few later) contain `LAUNCHFUEL`, `NAV_DATA` and other
  `Shop`-only items, which the station list can't sell: planet trading-post screens filed as the station.
- The current build (185034) has no captures. First in-game test: any station terminal in Slot 8.

## Open

- Pirate stations (`SmugglerStation` + `PirateStationExtraProds`, gated by a string check at engine+0x57aa6e and
  `DAT_146f8249c`). One capture, 1/7.
- Planet trading posts (`Shop` list): the same generator; needs the trade terminal's world position on the planet
  (building placement + terrain height), like the planetary cabinets.
- Tech merchants (ship/suit/weapon/vehicle specialists, issue #9's real ask): NPC interactions (types 0 to 2 in
  `0x141032d20`), not placed in the station scene. Their positions (and so their seeds) still need tracing.
- Prices: `0x140eec9e0`, seeded from the system seed (+0x2480, Mix64), the trading class's price multipliers and
  `TradeProductsPriceImprovements[w]`.
- Stock amounts are drawn but not compared (captured stock already reflects purchases).
