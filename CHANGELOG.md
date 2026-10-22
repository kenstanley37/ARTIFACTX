# Changelog

What changed in each ArtifactX release, written for players. The newest release is at the top; **Unreleased** collects what's done on `development` for the next one.

## Unreleased

### New

- **Ship Designer** (under Starships): build a starship the way a space station's fabricator does. Pick a ship type (Fighter, Hauler, Explorer or Solar), a part for each slot, a paint style and colours, and see it in a live 3D preview you can turn. Everything on offer comes from your own game files, so the choices match the game.
  - **Add as new ship** puts the design in an empty starship slot. **Apply to a ship…** puts it on a ship you already own, and can rename that ship to the design's name.
  - **My designs**: save designs with a picture of the ship, search them, load one to change it, save changes or save as new.
  - Parts are listed A–Z with a search box.
- **Paint this ship** (Starships, under the preview): repaint a ship you found or bought. Its parts, seed and name stay as they are; only the paint changes. **Remove paint** puts its original colours back. You can also borrow the paint of one of your saved designs. The game itself doesn't let you repaint found ships, but it uses and keeps paint set this way.
- **3D previews of multi-tools**, built from your game files, including crafted staffs (head, core and pole) in their real colours.
- **Multi-tool kinds and names**: the Type shows the game's own names (Alien, Atlantid, Voltaic Staff and so on), Pistol or Rifle is worked out by the game's own rule, and unnamed tools show the name the game gives them.
- **Starship names**: unnamed ships show the name the game gives them.
- **Building parts placed**: the title bar shows how many building parts you've placed in all - every base, your freighter, claimed space stations and loose objects like beacons and save points - and the Bases page breaks it down per base. Shown against the game's construction limit of 21,000 (green, yellow from 90%, red at the limit), and each planet base against the 3,000-part limit for uploading it.
- **Game screen reading** (Settings, off by default): while you play, ArtifactX reads what No Man's Sky shows and fills in what your save doesn't hold - planet weather, sentinels, flora, fauna and resources; system economy, wealth, conflict and dissonance; station and trading post prices and stock; creatures with their rarity, habitat, activity and a picture; renamed planets; and a picture of each system. Only the game window is read, only while it's in front, and only the text is kept - no screenshots. Everything stays on your PC.
- **Bases page**: each base shows the picture the game uploaded of it. Click a base to see every part in it by kind and count ("Timber Floor Panel · 237"), with its system and planet beside it over a faded backdrop of the picture. A **Placed objects** tab lists everything you've built outside a base - save beacons, signal boosters, refiners, garages - with where it is, so you can go back for it.
- **Live save updates**: while you play, ArtifactX follows the game's saves without reloading the page you're on. Newly acquired ships and tools appear on their own.

### Improved

- Explore: **renamed planets and systems** show the name the game shows, read from your save - your own renames and other players' - with the original name underneath ("Originally Irskya XIX"). No need to land on each one first.
- Explore: the Planets page is split in two. **Current System** shows the system you're in and its planets, with a System Data box laid out like the game's own; **Planet Finder** has the finder and the glyph decoder. Moons are now shown around the right planet.
- Starships: ships you found or bought are shown as a cyan **hologram scan** (their exact parts, but colours can't be shown yet). Ships built at a station, or repainted, show their real paint.
- Starships: a ship built at a station shows the parts it was built with, by name and icon.
- Starships: the name box selects the whole name when you click it, and **Save** appears as you type, without leaving the box.
- The reload notice says that reloading discards your unsaved changes.
- Ship Designer: Add, Apply and Repaint wait until No Man's Sky is closed, so changes can't be lost to the game's autosave.
- Starships shows how many of your starship slots are used, with the empty ones as placeholders (click one to open the Ship Designer). The Ship Designer shows slots used and free (e.g. "7/12 ship slots used · 5 free") before you add a ship.
- The Ship Designer works in narrower windows: the layout rearranges and the page scrolls.
- **Bases page** is grouped into Freighter, Planet bases, Claimed space stations, Settlements and Placed objects, each of which folds away. **Open** on a base shows its parts on a page of its own, and locations include the galaxy region. Bases is now in the Save Editor group, above Base Storage.
- **Deleting bases and placed objects**: on the Bases page, select planet bases or loose placed objects (save beacons, refiners and so on) and delete them. On a base's own page, delete every part of a kind, or the whole base with **Delete base…**. Each asks you to confirm and lists what goes; the save is backed up before it's written. Your freighter, claimed stations and settlements can't be deleted here.
- Each planet opens on a **page of its own** from Current System.
- **Capture now** buttons on the system view and on creature cards take the picture straight away when automatic screen reading missed it.
- Inventories: **curiosities and other items with a seed** can be added from the normal Add item search, and **Re-roll seed** gives an item in a slot a new one.
- Planets show the game's own description and their group together, e.g. "Crimson Planet (Red / Exotic)".
- Inventory **Sort** groups items by kind (technology, raw materials, components, curiosities and so on), then A-Z.
- Settings shows how big your ArtifactX data file is, its version and where it is, with a button to open the folder.
- Cards you fold away or open stay that way on every page.
- **Regions** (Explore): how much of each region you've mapped. For each system you've found there: whether its system data was read, and for every planet and moon whether its Discoveries hover and its own page were captured - so you can see what's left to do. The region also shows on Current System and a planet's page, and the Planet Finder can filter and search by it.

### Fixed

- Hovering a planet in the game's Discoveries list didn't record its sentinels ("Aggressive Sentinels"). They're filled in from the hover until the planet's own page gives the exact word - the hover shows the level, the page a word for that level ("Aggressive" on hover, "Frenzied" on the page).
- Screen reading missed some of a planet's details on its Discoveries page - often its sentinels on a bright sky, and the fauna count ("0 / 11"). A detail left empty is now read again from a close-up of that part of the screen.
- Current System asked "which planet is this?" about game messages screen recording read as names ("Online Discovery Services Active"). It only asks while a planet of the system has nothing recorded for it yet, and offers just those planets. When the game names a planet differently from ArtifactX (Ehilusti's "Rigie IV", which ArtifactX called "Nigie Major") and only one planet can be it, its details go there and the name is learned without asking.
- A planet's page didn't show how long you'd spent there or how far you'd walked (Visited).
- A planet's page showed made-up values for conditions nobody had recorded ("Standard Activity" sentinels, "Uncharted" flora); it says "Not recorded".
- Editing a planet's details: the pickers show what you recorded, words show their level ("Frenzied (High)"), a typo gets a "did you mean Cobalt?", clicking a suggested resource adds it, Enter adds a note, and Save keeps a note or resource you typed but didn't add.
- Removing a creature from a planet's page asks first.
- Deleting from the Bases page while the game was running could delete nothing.
- A base's own page shows its picture faded in behind the header again.
- Parts the game places itself (freighter bridge connectors, station cores) have plain names instead of made-up ones.
- Inventory **Sort** put some building parts with the maps.
- Fishing Records: a **Collected Flotsam** section, like the game's - Kelp Strand, Tangleweed, Sea Sponge, the corals, Old Boot, Rusted Technology and Message in a Bottle, ticked when you've caught them.
- Freighter: a **3D preview** of your freighter's own shape, built from its type and model seed and your game files (drag to turn) - painted in the colours you picked in game once you've customised it, otherwise a shape scan.
- Freighter: an **Engine flare** choice - Standard, Emeril, Gravity Drive, Golden, Antimatter, Hadal or Crimson, the same seven the game's Customisation offers.
- Generated planet and system names now match the game's: every planet in a fully mapped region (86 of 86) instead of about 4 in 5. The game runs each name through a word filter that the old name rules didn't know about.
- Starships preview: Sentinel Interceptors show in their seed's colours (checked against one in game: off-white with black trim).
- Starships preview: fixed-look ships (The Wraith, Boundary Herald, Starborn Runner and Phoenix, Vintage Interceptor, Golden Vector, Corvettes) show in their own colours instead of a hologram.
- Multi-Tool preview: fixed-look tools (found Voltaic Staffs, expedition tools like the Swarm) show in their own colours - bone and gold, stone and teal - instead of plain grey.
- Starships and Multi-Tool pages follow the game the same way for the ship or tool on screen (name, type, seed, class, stats, tech, cargo, paint). Inventories only pulse when an item is added, removed or moved - fuel and ammo levels update quietly.
- Freighter page follows the game: when the game saves, anything you changed in game (engine flare, paint, name, class, cargo...) shows up with a brief pulse - no reload, and fields you're editing in ArtifactX are never touched.
- Freighter preview: a Dreadnought is painted in the colours you picked for it in game, and its engines glow in your flare's colour.
- Freighter preview: a Dreadnought shows its real hull, no longer hidden inside its smooth shield shell.
- Freighter preview: the engines show as round nozzles glowing in your flare's colour, no longer hidden behind flat sheets of colour.
- Starships, Multi-Tool, Freighter: name, class and type come first in the details, base stats below, and Delete sits at the card's bottom right.
- **Frigates** page redone: your fleet as cards - class rating badge, role, crew race, the stat its role uses and expeditions flown - with a fleet summary ("30 frigates · 8 Trade · 6 Combat..."). Crew races (and a settlement's culture) show as Gek, Vy'keen and Korvax instead of the game's internal Traders, Warriors and Explorers.
- Fishing: each fish says how to catch it - the water depth its size bites in (shallow, 5-15 m, 30-65 m or 45-90 m; deeper on water worlds, any depth on gas giants) and the bait that favours it most, both from your own game files. Update the game data (Settings) once to get it.
- Templates: **Preview** shows what a template holds before you use it - class, where it came from, slots, model, interior, base stats and every tech item with its name - and flags items your installed game version doesn't have. Also in the freighter's "Add a freighter" list.
- Finds: a **Creature** type (wild, companion or battle creature), with its best battle class.
- **Alliance** page (under Milestones): your totals for the five stats a Galactic Alliance is graded on - systems, plants, minerals and creatures discovered, and stations claimed - laid out like the game's alliance screen, with what makes up each number and how to raise it.
- Space Stations: a station you renamed also shows the game's name for it, the only name the game's teleporter and alliance lists use.
- Bases and Parts: cards without a picture keep the picture space, with a note on how to take one in game, so every card lines up.
- Bases and Parts: claimed stations show how many of their parts are removable and unremovable (the 123-part structure every station keeps, which still counts toward the build limit).
- Bases and Parts says how many parts clearing your claimed stations' default furniture would free, and clears it from all of them in one go. Only the furniture every claim comes with goes; stations' structure and anything you built or moved stay.
- The title bar shows the open save's name next to its slot ("Slot 8 · DELETEME").
- The region a system is in (as the game's Galaxy Map names it) now shows on Space Stations (current station, claims in progress, station details), settlement cards and the Finds "you are here" line.
- Space Stations: after Claim Station, the "save your changes" line was missing and Reset Station Changes couldn't undo the claim.
- Space Stations: right after you claim a station, it offers to clear the default furniture the station came with - about 140 parts that count toward your 21,000 build limit. Only that default furniture goes; the station's structure stays, and anything you build later is managed on Bases and Parts.
- Space Stations: the stations you direct are listed with the one you're at first, then nearest first.
- The Bases page is now **Bases and Parts**. Corvette builds and space bases have their own sections - they were listed as claimed space stations (a corvette build showed as a station "in the system you're in"). Corvette builds show their ship's name instead of "Default".
- Bases: on a base's parts page, the parts the game places itself are listed first.
- Bases: claimed space stations can have their parts deleted again - claiming one adds about 140 furnishings that count toward the 21,000 build limit. The station's own structure (connectors, core, paragon) is marked "Built into the station - can't be removed" and can't be selected.
- Bases: a base's parts page now follows your game - parts you remove in game drop off the list when the game saves.
- Wealth you recorded from the game ("Flourishing", "Booming") sometimes showed as the bare word or with no stars, depending on which page you opened first. It now always shows its stars (High ★★★).
- Space Stations: the stations you direct show their system's wealth, economy and conflict over the picture, like the claims in progress.
- Fishing: rarity tags and the Rarity filter use the game's own words - what showed as RARE is Uncommon in game, and EPIC is Rare.
- Fishing: **Clear filters** button (shown once you change a filter) puts the list back to every fish for the planet you're on.
- Fishing: a fish you catch while the page is open now shows - a banner names it ("You caught your first Wispscale Darter. 30 of 42 caught.") and its card moves to the top with a NEW CATCH or JUST CAUGHT tag. Before, its card just slipped off screen.
- Screen reading now picks up exotic planet types the game words its own way, like "Xeno-Colony", from the Discoveries pop-up.
- Anomaly planets showed as "Green / Exotic" (or Red / Blue) - colours the game never shows - and screen reading skipped their type, "Planetary Anomaly". They now show as anomalies, with the game's own words once read.
- Starships, Multi-Tool, Freighter and Exocraft: **Reset** now undoes every change on the page - name, seeds, type, class and crew too, not just the tech and cargo - and no longer disappears after you change a seed. On Freighter it also undoes an applied full build's interior, and adding or deleting the freighter.
- Starships, Multi-Tool and Freighter: a Base Stats box could quietly save a stat rounded to 8 decimals after switching ships; only values you type are changed now.
- Companions: clicking into a number (Trust, Mutation progress, Genes improved, Victories) and away counted as a change - and could nudge Trust or Mutation progress by a rounding error. Only values you type are changed now.
- Settlements: clicking into Population and away counted as a change; it doesn't now. A feature's dropdown also shows its red/green colour when closed.
- General: clicking into Save name, Summary, Units, Nanites or Quicksilver and away counted as a change; it doesn't now.
- Screen readers: about 110 buttons and 440 inputs had no name (Ship Designer parts and slots, Exocraft vehicles, recipe items and pins, Capture, Paint this ship, the Add a find/video and Show overview toggles, details fold buttons). Lists of checkboxes (Catalogue, Guide, language words, fish records, rewards) now read each item's name, and text boxes and dropdowns read their label. The title bar's Save button had no name either, and buttons that read the same are told apart (each seed's Generate, each Add as my freighter). Every page was checked.
- Finds, Fishing and Near Core each read the game's save again on every autosave while open; they now share the one reading the rest of ArtifactX uses.
- Space Stations: a claim card's tags (wealth, economy, conflict) were cut off when they didn't fit on one line, and a pirate system said "Pirate Controlled conflict" plus "Pirate"; tags now wrap and say it once.
- Settlements' Building Materials (and Near Core) took about 1 GB of memory while reading where things are found in your other save slots; it now reads only the part of each save it needs.

- 3D previews were drawn mirrored (left and right swapped).
- Explorer wings: the left and right wing were mixed up in the preview, and an Explorer added from the designer came out with the wrong wings in game.
- Some builder-only parts (e.g. the Explorer's Skywave, Prismatic, Planar and Vortex wings) all previewed alike.
- Starships: switching ships kept showing the previous ship for a few seconds, and the parts list could appear twice.
- If ArtifactX closes unexpectedly, the error is now written to `%AppData%\ArtifactX\logs\crash.log`, so it can be reported.

## 1.0.0-alpha1 — 2026-10-15

First public test release.
