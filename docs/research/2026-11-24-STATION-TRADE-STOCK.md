# Station trade terminal stock (issue #9)

2026-11-24. What a space station's trade terminal sells is fully generated from the system address, game
files and the save's Item Shop Availability setting. **Confirmed in game the same day** on Ialenn (Slot 8, build
185034): the 17 items on the terminal are exactly the 17 generated. The port reproduces every captured item for 159 of 170 non-pirate stations; the other 11 captures
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

"Offered" (`0x140887450`): not in `NeverOfferedForSale`, not in the save's Item Shop Availability list
(`GcDifficultyConfig.ItemShopAvailabilityOptionData[setting].NeverSoldItems`; save key `vLc.6f=.LyC.:fe.FB5.TYf`,
index 20 of GcDifficultySettingsData), and in expeditions not in a season list. Normal never sells OXYGEN,
CATALYST1, CATALYST2, POWERCELL, PRODFUEL2: two of those are fixed station products, so they are not on sale when the
trade-goods step runs and each draws there. Leaving the list out keeps the products right but shifts the substances
(Ialenn showed Pyrite/Gold instead of Uranium/Phosphorus until it was applied).

Step 5 draws once per product in all three tables. **The draw count depends on the game build's table sizes**, so
the catalog must use the player's installed tables, never a fixed number.

## Verification

- With the right draw count, all 159 exact stations predict every captured item (always items, the optional
  product picks, the 1-5 trade goods, the optional substance picks).
- The draw count fitted per station (`sweep`, `PER=1`) is exactly `entries - products already on sale`. With the
  Normal list applied (`core`, `OLD`/`NEW` offsets), captures before 2026-10-28 fit 228 fewer entries and captures
  from 2026-10-29 to 2026-11-13 one fewer than the 185034 install: each game update added products. Ialenn on
  185034 fits the installed tables with no offset (`save <decrypted save>`).
- Not exact (11): the earliest captures (2026-10-17/18 and a few later) contain `LAUNCHFUEL`, `NAV_DATA` and other
  `Shop`-only items, which the station list can't sell: planet trading-post screens filed as the station.

## Planet trading posts (solved 2026-11-24)

A trading post is building class 6 (`Outpost`) of the planet building grid (`PlanetBuildingSites`). It has no
L-system: PLANETBUILDINGTABLE gives one scene per race (`TRADINGPOST{TRADER,WARRIOR,SCIENTIFIC}.SCENE`, Gek/Vy'keen/
Korvax), whose `Terminal_Tradingpost` reference holds the `Shop` interaction at scene-local (37.4, 17.795189, z) with
z = -3.905937 / -3.287193 / -3.779248 (`outpost` mode). The building is not clustered, so its spawn point is the site
point raised to the Far-region spawn height (`PlanetShopCabinetGenerator.SpawnHeight(site, site)`), with the shop
cabinets' axes (up = site direction, forward from `BuildingClusterPlacement.Facing(site, key)`). Terminal world
position -> `SeedForPosition` -> Mix64 -> the same generator with `TradeSettings.Shop` (Shop interaction outside a
station: `0x14087ae50` falls through to Shop).

Confirmed in game 2026-11-24 on Oodleibe Major (Ialenn planet 3, Slot 8): the site nearest the visor reading
(+16.61, -152.18) is predicted at +16.60, -152.19, and its terminal (Repository IA7/S-BV1/I) sold exactly the 18
generated items (`post <decrypted save> <lat> <lon>`). An id missing from this build's tables is passed over without a
draw (Copper `YELLOW1` is on both lists but in neither table, so no station or post sells it; marking it known shifted
every later pick).

The October/November trading-post captures can't confirm a site on their own: they don't record which post was read,
and their few, mostly fixed items fit dozens of the ~1,800 Outpost sites per planet (`posts`).

## Buy prices (solved 2026-11-24)

What the terminal charges, `0x1408a0000` -> `0x140eec9e0` -> `0x140eecda0`. All 35 prices on the Ialenn station and
Oodleibe Major trading post screenshots match to the unit (`price` mode).

1. Item seed: the shop's seed plus the item ID's two 8-byte halves (u64 wrap). The shop's seed is the terminal's
   raw interaction seed (position hash, not Mix64-ed) at a trading post, and the **system seed** (solar system data
   +0x2480 = the packed system address, `SolarLayoutGeneration` layout address) at a space station
   (`0x14088edc0`: the station flag picks the system seed). Start the stream from it (no mix), four words, `r` = the
   mean of the four as floats (w2 + w1 + w3 + w4) * 0.25.
2. Base = BaseValue * ((HighPriceMod - LowPriceMod) * r + LowPriceMod + 1) (`GcItemPriceModifiers` on products
   and substances).
3. Buying: markup = r * BuyMarkupMod + BuyBaseMarkup, times the save's Currency Cost
   `GcDifficultyConfig.CurrencyCostOptionData[setting].TradeBuyPriceMarkupMod` (Normal 1.5; save key `tsk.Ubk`)
   except for the system's own trade goods; price = (markup + 1) * base.
4. Economy: for items of the trade category the system's trading class sells, M = lerp(MinSelling, MaxSelling, u) -
   TradeProductsPriceImprovements[wealth] (the station or Shop list's), u = the first word of Start(Mix64(system
   seed)) / 2^32, the same for every item in the system; other items M = 1. Then M = M + (1 - Economy Influence) *
   (1 - M), clamped to the trading class table's 0.2..3.0. Price = (int)(price * M).
5. `SpaceStationMarkup` is applied at stations only with a flag not identified yet; none of the screenshot items has a
   nonzero markup, so it isn't confirmed either way.

Selling prices (what a system pays) take the other branch (no markup; `MinBuying`/`MaxBuying` plus the improvement
for the class's `Needs` category, two runtime globals for other cases) and are not confirmed yet.

## Open

- Pirate stations (`SmugglerStation` + `PirateStationExtraProds`, gated by a string check at engine+0x57aa6e and
  `DAT_146f8249c`). One capture, 1/7.
- Tech merchants (ship/suit/weapon/vehicle specialists, issue #9's real ask): NPC interactions (types 0 to 2 in
  `0x141032d20`), not placed in the station scene. Their positions (and so their seeds) still need tracing.
- Prices: `0x140eec9e0`, seeded from the system seed (+0x2480, Mix64), the trading class's price multipliers and
  `TradeProductsPriceImprovements[w]`.
- Stock amounts are drawn but not compared (captured stock already reflects purchases).
