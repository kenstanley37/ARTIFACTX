# Changelog

What changed in each ArtifactX release, written for players. The newest release is at the top; **Unreleased** collects what's done on `development` for the next one.

## Unreleased

### Fixed

- Screen capture no longer files a planet's creature list under another planet whose name is an ordinary word (a planet called "Heat" took a list whose notes said "despite the heat").
- Items you add get the game's real stack size for the inventory they go in: Metal Plating now stacks to 40 in base storage containers (it stopped at 20), freighter and base storage sizes are right, and on Normal or Low inventory stack limits substances stop where the game stops them instead of 9999; Max Qty and Max All Qty also raise stacks an older version saved too small. Update your game data once to get this.

### Added

- Planet Details: creature 3D models now have the body parts the game gives that species (head, body, tail and the rest picked from the creature's seed), instead of the same default parts for every creature of a kind. Update your game data once to get this.
- Planet Details: the anomalous creatures of exotic planets (floating, rolling and crystal ones) show the body that belongs to their kind of planet, crystals with the growth round their base, and the strange burrowing creatures and flocks show the animal itself. Update your game data once to get this.
- Planet Details and Companions: creature 3D models are painted in the game's own skin patterns (fur, scales, underbelly, horns), coloured from their planet's own colour set as in game, instead of one flat colour per part. Update your game data once to get this. A companion with a colour override set shows that colour.
- Planet Details: plant creatures (the floral cat with its root body and leaf plumes) and robot creatures show their own model instead of a stand-in blob or antelope. Update your game data once to get this.
- Multi-Tool Finder (Explore): the multi-tools space station cabinets sell in the star systems around you, visited or not, with each cabinet's class and the tool's kind (Alien, Experimental, Rifle, Pistol...). Filter by kind and class, and see where to save and reload for the cabinet to sell it.
- Creature Finder (Explore): every creature you can adopt on the planets in your saves and the rest of their systems, ranked by its pet-battle Trait Potential, Combat Effectiveness, Agility and Health, with filters for class, element, galaxy and whether you've discovered it. Click one to open its planet and dial the portal address to go there.
- Creature Finder: filter by size (up to Huge only), sort biggest first, and see each creature's size class and both forms' weight and height on its card. The portal address's Copy button is no longer cut off, and each card shows its galaxy as a badge, marked when it isn't the galaxy you're in.
- Creature Finder: search the star systems around you too, even ones nobody has visited (your region, or it and the regions around it), and filter by kind of creature. Pin a creature to keep it at the top of the list, and a badge shows when you're on its planet.
- Planet Details and the Creature Finder show when a creature lives in only one half of its planet, in the game's words ("Always Active · Found in the South"), so you know which side of the equator to search. Update your game data once to get this.
- Planet Details: creatures that fly but come from a planet's ground creatures (large butterflies, small birds) now read "Flying" as in the game, not "Ground", and habitats use the game's own words.
- Planet Details: creatures you haven't scanned yet also show their rarity and when they're about (for example "Predicted · Rare · Ground Fauna · Always Active"), the two forms the species comes in (each with its gender, weight and height), and its age, behaviour, diet, notes and what it produces when fed, as the analysis visor shows them ("Hasty · eats Solar Vine · Photosensitive · produces Wild Milk"), plus a panel with its pet-battle Trait Potential, its Combat Effectiveness, Agility and Health classes (in the game's class colours) and its element, laid out like the game's analysis visor so arena-worthy creatures stand out. Only creatures you can adopt show the battle panel, and they're listed first; creatures that can't be companions (fish, birds, burrowers and so on) come after them. Discovered creatures show their rarity and activity too, and fish, jellyfish, manta rays, moles and the stranger anomalous creatures show their own 3D model instead of a generic four-legged one. Update your game data once to get this.

- Planet Details and Current System keep their top bar on screen while you scroll. Once the planet or system card is out of view, the bar also shows its name, type and key facts (sentinels, weather, star, economy, where you are), so you no longer have to scroll back up.

- The Catalogue page is now part of the Catalog page: its technology is the Catalog's Technology section, which also tells you about broken technology placeholders and has a Forget broken technology button.

- Catalog: Wonders records have pictures. Planet Records show each planet in its type's colour, with rings if it has them, and Fauna, Flora and Mineral Records show the game's own paw, leaf and crystal icons. Update your game data once to get them.

- Ship Designer: paint styles you haven't unlocked yet (expedition rewards such as Titan Stripes) show a padlock. You can still try them on, and a new button unlocks the picked style.

- Ship Designer: "Put these parts in my Exosuit" gives you the design's parts as items, so you can build the ship yourself at a space station. Right-click any part to add just that one.

- Ship Designer: the Paint and My designs cards can be hidden with a button in their heading. Hide Paint to give your saved designs the whole column; the choice is remembered. Painting one of your ships always opens Paint.

- Ship Designer: paint colours can be picked with the keyboard (Tab to a colour group, arrow keys to move, Enter to pick), and screen readers say which colour is picked.

- Mobile camera scanning now shows moving brackets around detected portal details and a green lock before opening the system.

- Mobile system details show the address as glyph pictures, ready to dial at a portal.

- Mobile camera scanning now watches portal glyphs and the galaxy name together, opening system details automatically after stable readings without taking a photo.

- Mobile portal addresses now open system details with planets, moons and possible starships, and recently opened systems appear on Home.

- Mobile portal scanner: take or choose a photo of a glyph strip, review and correct the address, then select its galaxy to look up the system. Unreadable photos leave your entered address intact.

- Procedural Planet Descriptors: Planet type descriptors (e.g. "Tropical Moon", "Paradise Planet", "Glacial Moon", "Ossified Planet") are now generated procedurally from the planet's seed using the game engine's exact algorithm, displaying alongside the broad biome classification ("Tropical Moon (Lush)", "Paradise Planet (Lush)") across planet cards, details headers, location summaries, system maps, and find lists without requiring visor screen capture. Searchable directly in Planet Finder.
- Planetary Hotspot Clusters & Base Site Optimizer: Planet Details can now locate multi-hotspot clusters (Power + Mineral, Power + Gas, and 3-way Trifectas) reachable from a single base. Calculates the mathematically optimal Base Computer coordinates to bring all extraction sites within simultaneous range, with search, sorting (nearest to you, closest to portal, class, name), and one-click actions to place a Save Beacon right at the optimal base site.

### Changed

- Planet Details POI Layout: Converted Portal, Crashed Ships, and Cabinet Multi-Tools from 100% full-width folding sections into compact responsive cards. They now render side-by-side in a responsive row, saving over 200px of vertical scrolling space while matching the 370px card format of the located sites and hotspots grids.

### Fixed

- Game text now uses US English, as the game shows it to US players ("Vectorized", not "Vectorised"). Update your game data once to get it.

- Current System no longer stops with a "file in use" error when a system picture is read while screen recording is saving a new one.

- Planet Details no longer goes blank after your game data is rebuilt, the theme changes or you switch to the save the game is using: the page reopens on the same planet.

- Galaxy Transfer: the galaxy list opens at the galaxy you're in and stays where you scrolled it while the game saves. Filters no longer jump the list back to the top, and a galaxy's base and station badges update as soon as the game saves.

- Planet fauna totals are now right on waterworlds too: they were often a few creatures short, and missed the flying creatures some waterworlds have.

- Planet Details: creatures you haven't scanned yet show the species name the game will give them (marked "Predicted"), and are the right kinds more often (a Triceratops where it used to say Antelope). Each creature you have discovered now shows with its own model and habitat. Update your game data once to get this.

- Save Beacons placed from Planet Details are now real beacons standing on the ground. Before, the game showed only their waypoint.

- Planet Details: the Portal, Crashed Ships and Multi-Tools card labels and descriptions are easier to read.

- Works with No Man's Sky Forsaken 7.14: game data is rebuilt once, and planet buildings and cabinet multi-tools are placed with the right spacing again.

- Mobile live scans now keep matching readings on slower phones and restart the camera after returning to the app.

- Mobile live scans now usually open the system within a few seconds: line up the glyphs in the on-screen box. Scans no longer restart when one symbol is briefly unclear over bright scenery or the phone wobbles.

- Mobile glyph keys and review now use clearer, bolder glyph pictures with their common names.

- Mobile glyph review now draws the glyph symbols correctly, keeps the address in two rows, and opens correction keys when you select a position.

- Mobile camera scans handle blue-tinted monitor photos and screen pixel patterns more reliably, and reject more obscured or ambiguous strips before showing an address for review.

- Planetary Rings: Fixed planetary ring detection so only the single planet chosen by the game's solar system layout shows rings, matching in-game Discoveries instead of marking multiple planets per system.
- Planetary Hotspots & Base Beacons: Resolved Save Beacon elevation calculations by evaluating procedural terrain elevation (`PlanetTerrain.Height`) so placed beacons sit cleanly above the planetary surface (~3m clearance) rather than spawning underground inside procedural bedrock. Added full "Remove" support directly on hotspot and cluster cards to delete and clean up beacons ArtifactX placed; Save Beacons you built yourself nearby are never replaced or removed.
- Planetary Hotspots & Base Sites: Replaced scan event markers with physical in-game Save Beacons (`^BUILDBEACON`) placed directly at the optimal Base Computer and hotspot coordinates. In No Man's Sky, scan events on open terrain are ignored by the game engine, while placed Save Beacons render directly in the world and produce a permanent star HUD waypoint visible across the system.
- Planetary Hotspots Selection: Fixed radio buttons resetting and hiding all hotspot/cluster cards whenever a game save or landing occurred, preserving your active view mode and filter choices across save reloads.
- Galaxy Exploration & Presence Tracking: Galaxy Transfer now scans your bases, settlements, teleport network space stations, and system discoveries across all 256 galaxies. View base counts, space stations, and unvisited status badges on each galaxy, with 1-click filter tabs to isolate unvisited galaxies, visited galaxies, or those with active bases and space stations.

- Planetary Hotspot Locator: Planet Details now predicts and lists all deep-level resource hotspots (Electromagnetic Power, Minerals, and Gases) in your vicinity using the game's procedural algorithms. View S/A/B/C classes, resource types, extraction yields, Survey Device range, visor coordinates, and distance from your position (or portal), with one-click actions to stage navigation markers into your save.

- Galaxy Transfer: A dedicated Galaxy Transfer page under Explore lets you warp your save to any of the 256 galaxies in No Man's Sky. You arrive inside the space station of a star system about 3,700 light-years from the galactic core. Each galaxy gets a system that really exists and has an ordinary station, never one without a station, and its glyphs are shown so you can return. The station is added to your Teleport Network.

- Planet Fauna 3D Models & Silhouettes: Planet Details now displays 3D models for all planetary creatures before and after scanning. Undiscovered native species predictively generate from the planet's seed and display as sleek 3D mystery silhouettes with rim lighting, transitioning to full anatomical 3D models once discovered in game.
- Creature 3D Camera: Creature and companion 3D model previews now face towards the player by default with a front three-quarter view, rather than facing away.
- 3D Creature & Companion Models: Companions page now features an interactive 3D model preview rendered directly from your game install with your pet's active body parts and archetype; planet creature cards now feature a 3D anatomy viewer to inspect planetary fauna and examine 3D rigs.
- Explore: Dedicated "Crashed Ships" and "Cabinet Multi-Tools" pages under Explore, offloading heavy scans and lists from Current System and Planet Details into streamlined views with filters, planet selection, 3D model previews, and in-game coordinate markers.
- Catalog: Wonders records (Treasures, Collected Glitches, Planet, Fauna, Flora, Mineral, and Fishing Records) can now be unlocked or cleared directly on the Catalog page with one-click toggling and Unlock All / Clear All support. Unlocked records are filled in for you: each gets a typical value and a place in the star system you are in, on a planet or moon that system really has. Personal Records are not shown: those are yours to choose in game. Unlock All only fills in the records you don't have yet; your own discoveries are never replaced.
- Space Stations: Added a "Register in Teleport Network" action to sync and register all your directed space stations with your Alliance ownership stamp, ensuring every claimed station appears under "Alliance Stations" in the in-game Teleportation Terminal.
- Space Stations: "Claims in Progress" and "Stations you Direct" sections are now collapsible folding cards that remember their state across sessions.
- Squadron: Expanded seed boxes with generous width so 18-character hex seeds and roll buttons have comfortable breathing room, with the 3D ship preview positioned to the right with responsive wrapping.
- Starships: Corvettes now show a "Corvette 3D model coming soon" placeholder in the preview section rather than leaving the preview area empty.
- Seeds: Clicking Generate on any seed box (Starships, Freighters, Squadron, Frigates, Exocraft, Companions) now includes Previous and Next buttons to step backward and forward through your rolls during your edit session.
- Templates: Previewing a full build starship or freighter template shows an interactive 3D model of the ship alongside its tech and stats, with drag-to-turn rotation.
- Save Selection has a restore button on each account folder's header to pick and restore timestamped backups of account-wide data (accountdata.hg) such as expedition rewards, Twitch unlocks, and guide entries.
- Squadron page shows a live 3D preview and procedural name for each pilot's assigned starship, generated from your game files and the ship's seed.
- Official GitHub Pages project website showcasing features, 3D previews, safety architecture, and release download links.
- In-app automatic updater powered by Velopack: when an update is available on GitHub Releases, players can update and restart directly from the title bar or Settings page without having to manually download installers. Player data and databases in %AppData%\ArtifactX remain completely untouched.
- GitHub Actions automated release pipeline and continuous integration workflows.

### Fixed

- Current System & Planets: Fixed planetary ring detection so planets with rings (and those without) match the game's actual generation across discovered and undiscovered worlds, replacing an earlier heuristic that mistook extreme weather flags for rings.
- Galaxy Transfer: Current location and the "HERE" badge in the galaxy browser now quietly follow your in-game movements and save changes in real time without refreshing or scrolling the list.
- Planet Details: Eliminated UI freezes and delays when opening planet details by loading creature 3D models immediately and removing redundant background crash site counting.
- Planet Details: Removed manual screen capture controls for creatures in favor of automatic procedural 3D model generation and save discovery reading.
- Current System: Removed a full-page modal loading overlay that intercepted clicks and scrolling during system refreshes, replacing it with an inline activity indicator that leaves the page responsive.
- Planet Details: Planetary fauna discovered in your save now display creature cards with authentic Latin species names and 3D anatomy viewer buttons even without prior screen captures.
- Space Stations: Claiming a station now automatically creates and stamps its teleport endpoint marker in your save so that newly claimed stations immediately appear in your Alliance Stations teleport list.
- Catalog: Eliminated a race condition that could cause section navigation buttons at the top of the Catalog page to duplicate during rapid category switching or save reloads.
- Performance: Current System and Planet Details pages now process crashed ship spatial scans, shop distance calculations, and multi-tool model part resolutions entirely off the UI thread, eliminating UI freezes during navigation.
- 3D Previews: Found and bought starships (Fighters, Haulers, Explorers, Solars, Shuttles, and Sentinel Interceptors) now display in full color from their seed rather than defaulting to a blue hologram scan.
- 3D Previews: Fighter, Hauler, Solar, and Shuttle starship wings and hull coatings now render in their authentic primary and secondary seed paint colors rather than falling back to unpainted tan or grey on procedural trim surfaces.
- 3D Previews: Sentinel Interceptor starships now accurately display their hull paint colors, accent decals, and wing trim from their seed (fixing an issue where procedural overlay coats marked for non-named procedural use were omitted).

## 1.0.0 — 2026-11-07

### Added

- Current System has a Multi-tools in cabinets section: which multi-tools every cabinet in the system sells, for each place you can load your game (the space station and each planet), with the place your save loads at highlighted. Every cabinet in a system - station and planet shops alike - sells from the list of the place you last loaded at, so save and reload where the multi-tool you want is.

- A planet's page lists the shops with a trader and a multi-tool cabinet (the game labels them "Minor Settlement") nearest you or its portal with their Analysis Visor coordinates, a "YOU ARE HERE" badge when you are inside a shop, Visited and Unvisited badges read directly from your save's interaction history, distance and visited filters, and an Add marker button to lead you straight there. 3D models at the top preview the multi-tools available across cabinets on that planet.

- Right-click an item in any inventory and choose View larger picture to see it at the size the game has it (twice as big as the slot picture), read from your own game files.

- The title bar shows where you are, on every page: the planet you're on, the space station, your freighter or the Space Anomaly, and the system. Hover it for the region, galaxy and portal address. It follows the game as it saves.

- A planet's page shows how many of its species you've found ("3 of 8 species found") and a tile for each one still to find, using the count the game showed you or, if you haven't seen it, the count the game's generator gives.

- Catalog page for the loaded save, laid out like the game's own Catalog: its sections with the game's counts ("Blueprints Known 964/1,024"), each section's categories in the game's order, and the items as tiles that are coloured when known and a dark silhouette until then. Learn or forget Technology and Building Parts blueprints there, such as greyed-out Space Station Exterior parts, and refiner and cooking recipes (a recipe tile is what it makes; learning it learns every recipe that makes it). Wonders shows what the game has recorded - planet, fauna, flora and mineral records, treasures, glitches, fish caught and fossils - with the game's own count. Needs the game data read again (Settings).

- Treasures and other procedural items (fossils, bio samples, salvaged loot, dismantled parts) show their own name from the game, like "Shiny Bones", in the Catalog's Wonders and in your inventories. Needs the game data read again (Settings).

- The Catalog's Fauna, Flora and Mineral records show each creature's, plant's and mineral's name from the game, like "J. Mustlimii" or "Omkovaite", and the Companions page shows your companion's species name. Companions you never renamed show the name the game gives them, like "Ana-Ana", instead of "Protoroller #2". Needs the game data read again (Settings).

- Egg Sequencer page under Reference, laid out like the game's sequencer: pick a catalyst slot to see only the items it takes, filter by the effect you want (bigger, toward Gentleness and so on) or to only the items you carry, put an item in and see the dose the game will show and, in plain words, what will happen to the new companion. Choose one of your eggs to see its exact personality before and after ("Gentleness 50% becomes Gentleness 30%") and which changes its kind can't take. Needs the game data read again (Settings).

- Frigates: for an organic frigate, a Feed Vessel list shows what every food would do to that frigate - its new traits and how each stat changes - best first for the stat you choose. Frigate Details can be folded away while the stats stay in view. Each food has an Add 1 to Exosuit button, so you have it when you reach the Feed Vessel. Needs the game data read again (Settings).

### Fixed

- Uncharted systems (Race, Economy and Conflict "Data Unavailable", often dissonant) no longer list crashed ships that aren't there. Their only claimable crashed ship is one underwater wreck on planets with a sea, and the app now shows that ship and where it lies. Planetary Charts there lead to a fellow Traveller's crash site with a small reward (an upgrade module or some nanites), not a ship, and the planet pages say so.
- Current System's planet cards now show each planet's best crashed ship - the nearest S-class, or the highest class the planet has - instead of only S-class ships.

- The building parts counter takes the construction limit from your own copy of No Man's Sky, so it stays right if a game update changes the limit. Needs the game data read again (Settings).

- Screen readers: buttons that looked alike now say what they act on - each planet's and base's Open, the Max button for units, nanites and quicksilver, each template's Share, each find's Edit and Delete, each inventory's Unlock All Slots and Show all, each companion seed's Generate, each crashed ship's marker (with its coordinates), each station multi-tool's slots and each empty starship slot.

- Corvette Cache: each part in the list says what it is, in the game's words ("Corvette Cockpit"), and where it comes from: bought or traded at the Corvette Workshop, an expedition reward, or not sold at the workshop. Filter by either, and "Add one of each I don't have" adds only the parts shown. Needs the game data read again (Settings).

- Language words list exactly the words the game can teach, so a race whose words you have all learned shows as complete instead of with about 200 left. Words that are forms of one another, like "traveller" and "travellers", are now one word, as in the game. Words you learn through ArtifactX are now saved under their own race; before, every word was saved as a Gek word. Words you learn in game are ticked as soon as the game saves. Saving word changes also updates the "Words Learned" count on the game's milestone screen, which used to stay behind until you learned another word in game. Needs the game data read again (Settings).

- Regions keeps counting systems you visited after the game drops them from your save's own list (it only keeps so many). ArtifactX remembers each save's visits from now on, so a fully explored region can show as complete.

- Cooking Recipes shows the same food effects as the Nutrient Ingestor (Ice Cream is +65% cold resistance, not +164%). Effects whose size depends on your own suit are named without a number.

- Account Data › Catalog is gone, replaced by the new Catalog page. Its account-wide list doesn't decide what the game shows as known, so ticking items there changed nothing in game.

- Expedition Rewards and Twitch Rewards: each section now has its own tick-all checkbox (showing how many of its items are unlocked) and a Locked only filter. The page-wide Lock All and Unlock All buttons, which changed every item on the page at once, are gone.

- Expedition Rewards lists every reward from each expedition, grouped under the right one, including the newest expedition and rewards such as Direwasp armour and figurines that were missing. Needs the game data read again (Settings).

- Finds and Near the Core start from where you are now. Opening them after travelling used to measure distances, and fill in Use my current location, from where you were when the save was loaded, until the game saved again.

- Companions page edits register as you type, so Save and Reset appear straight away instead of only after leaving the field. A half-typed number or seed waits until it is complete.

- Planet Finder's search filters and Glyph Decoder can be collapsed independently. Their open/closed state is remembered, while the result count, Clear filters and Export CSV remain available.

- Purple-star portal addresses now warn that purple-star access is required, including in Planet Finder and the enlarged glyph view. Without access, the game can send the player to a different system.

- Every editable cargo inventory now has Merge Duplicates, including equipment cargo and Base Storage. Merging preserves excess quantities in additional stacks and keeps items with different saved properties separate.

- Ingredient and Cold Storage show their inventory grids before loading catalog pictures and the available-item list. Base Storage containers use buttons that wrap onto additional rows, including long names.

- Industrial Cleanup card totals and reward progress align along the bottom, even when labels wrap.

- Saved Exocraft customization unlocks are now read-only. Newly selected unlocks can be unchecked before Save, undoing their pending requirements while retaining other selected parts.

- Exocraft customization edits now include the game settings unlock list, which could restore a part after it was unchecked. Settings changes are backed up and saved with the slot and account edits.

- Saving Exocraft customization changes no longer refreshes the unlock controls from a background thread, which could close the app.

### Added

- **Cold Storage fish rarity**: fish cards show their rarity, and the item list can be filtered or searched by rarity alongside its existing filters.

- **Cargo**: Base Storage, Ingredient Storage, Cold Storage, Corvette Cache and Dismantled Base Components share one editor page. Each tab loads when first opened and keeps its place and filters when you switch storage types.

- **Industrial Cleanup guide**: find waste, follow the equipment and recycling steps, and see lifetime recovery totals and Colossus reward requirements. Available under Reference after updating game data; a save is optional.

- **Exocraft customization editor**: compact checkboxes unlock parts in the loaded save and across the account; saved unlocks are read-only and new choices can be undone before saving. Hover for requirements; enabling raises verified progress requirements only to the minimum. Save and Reset include these changes.

- **Exocraft customization guide**: each vehicle tab has a collapsible unlock guide. Colossus parts show the game's recycling title requirements, progress and recorded account rewards, including a warning when an earned title's part reward is missing. Update game data to load the new definitions.

- **Choose recipe ingredient sources**: collect an item from a matching base or use enough stored supplies to replace its refining branch. Requirements recalculate, and you can switch back to making the item.

- **Base ingredient sources**: recipe details identify mineral and gas extractors at your saved bases after updating game data. Matching bases appear directly below the ingredient name, before recipe alternatives. Uncollected output stays separate from owned inventory.

- **Cooking ingredient sources**: creature products now explain whether to hunt wildlife or feed it and collect produce. Dirty Meat identifies burrowing creatures as its source after updating game data.

- **Cooking and Refiner Recipes**: separate pages make it easier to find food recipes or refine minerals and other materials. Both use the recipe planner; food-effect filters stay on Cooking Recipes.

- **Recipe planner**: browse recipes by item or Nutrient Ingestor effect, compare bonuses strongest first, and follow connected, equal-size cards through every preparation step. Includes equipment pictures, ingredient sources, a combined gathering list, and Back and main-recipe navigation. Replaces the previous recipe drill-down pages.

- **Crashed ship details**: cards show unit values and clearly labeled intrinsic bonuses. Supported Shuttle, Fighter, Hauler, Explorer, Exotic, Solar and Sentinel wrecks also show Damage Potential, Shield Strength, Hyperdrive Range and Maneuverability using the active ship in your loaded save. Sentinel comparison stats are limited to inhabited systems.

- **S-class crashed ships** (Current System): each planet card shows the nearest S-class crashed ship on that planet (Exotics included), with how many the planet has, and a button to add a marker to your save. In systems without a race these aren't confirmed in game yet, and the card says so.
- **Crashed ships that aren't there**: crash sites on ground too steep for the game to build on (inside mountains, on cliffs) or under the sea are no longer listed, so the lists and markers point only at wrecks the game actually places.
- **Underwater crashed ships**: planets with a sea also list the wrecks that lie on the sea floor, with roughly how deep they are. You can add a marker for them too.
- **Crashed Sentinel ships**: dissonant planets also list their crashed Sentinel Interceptors (type "Sentinel" in the search), with their class and a marker button (their names aren't shown yet).
- **Crashed ship search** (Planet Details): search a planet's crashed ships by class (S by default, A, B, C or any) and type. The type list shows only the types the planet has in the chosen class, with how many of each (the game ties a wreck's type to its class, so for example C-class wrecks may only be Fighters and Haulers). It lists the nearest matches - from 5 up to all of them, as you choose - each with a marker button, and says how many the planet has. Your choices are remembered.
- **Recipe requirements tree** (Recipes): click a recipe card to see every ingredient needed, step by step, all the way down to the raw materials - amounts scale with how many you want to make.

### Fixed

- **Recipe trees**: conversion families now expand automatically through their preparation recipes. Circular branches end at a clearly labeled starting-supply card instead of hiding the whole branch. Owned ingredients do not hide preparation steps.

- **Recipe supplies**: ingredient totals include cargo in owned ships, freighter, Exocraft, shared storage containers, ingredient and cold storage, and confirmed item caches. Ingredient details list where supplies are stored; live saves update the quantities in place.
- **Ingredient sources**: restored the game's acquisition descriptions for substances, including Tritium asteroid mining and the planet types described for atmospheric gases.

- **Recipe alternatives**: ingredient details now show every available recipe as a card, including conversions for starting ingredients. Choosing a circular conversion lists the starting supply it needs instead of hiding the recipe.

- **Recipe quantity**: a wider quantity field leaves room for all five digits while typing, alongside the clear and step buttons.

- **Recipe planner space**: search now folds away after choosing a recipe, and recipe details can be collapsed too. Compact headers keep the selected recipe and filters visible while giving the map, ingredients and details more scrolling room.
- **Recipe selection**: the chosen requirement now has a clear selected marker and stronger highlight. Its details receive a brief accent cue and scroll to the top, making ingredient changes easier to follow.

- **Recipe cards**: source and recipe cards now have matching heights, with aligned results and controls. Long details scroll inside each card.

- **Recipe requirements**: starting ingredients now have source cards with the game's item description and the steps that need them, including animal byproducts such as Syrupy Nectar.

- **Recipe steps**: requirements now appear as numbered recipe cards with ingredients, results, a starting shopping list, and a choice of alternative recipes.

- **Recipe requirements**: opening a recipe no longer endlessly expands circular refiner recipes on the app thread. Large trees stop at a clearly marked limit.

- **Game data updates**: a completed rebuild now replaces the catalog safely while the app is open, preventing the update prompt from returning because older database data was still being read.

- **Game data updates**: skip an obsolete test file bundled with the game that could trigger a null-reference error while rebuilding the catalog. Unknown types now produce a useful diagnostic.

- **Planet Finder**: results are cards like the planet cards on Current System, side by side, instead of full-width rows.
- **Water worlds**: their underwater crashed ships are listed (the planet showed none before).

- **Current System planet cards**: every card shows the same rows in the same order (a dash where a planet has nothing), each on one line with the full text on hover, so the crashed ship and your details line up across cards. Fish moved to the bottom.

- **Planet Details markers**: a portal or crashed ship that's marked in your save but no longer shown by the game can be marked again (Undo puts things back as they were). The crashed-ship rows now say when No Man's Sky has to be closed before you can add a marker.

- **Possible system starships** (Current System): the list now shows the ships that really appear in each system. Since Forsaken (7.10) it had shown the wrong names and seeds. Rebuild game data to apply the fix.

- **Current System planet cards**: The cards now line up from the left and wrap onto new rows instead of sitting centred, with some space below the section heading. Cards are no longer all stretched to the height of the tallest one, so short cards don't leave a big empty gap.

- **No Man's Sky Forsaken (7.10)**: ArtifactX reads the Forsaken update's game files correctly again. Before this, a game data rebuild on Forsaken left the Regions map without its star settings, the Corvette Cache without its buildable parts, and some multi-tool and space station details wrong. Rebuild game data to apply the fix. The title bar now shows the No Man's Sky release ArtifactX was tested with, and says when your game is newer than that.

- **Marker coordinates on Planet Details**: Sites found with a planetary chart, signal booster or beacon now show their real latitude and longitude. They were worked out from the wrong point and could be far off. Where the planet's position can't be worked out exactly, the site is still listed without coordinates.

- **Coordinates on prime planets**: Planet Details now shows marker coordinates on prime planets too, including every planet in a purple-star system. Before, these planets listed their sites without coordinates.

- **Regions page pauses**: The page no longer freezes for a moment each time the game saves. Unchanged systems stay as they are, and only the cards on screen are drawn.

- **Station multi-tools**: Food bonuses unrelated to weapon damage or scanner range no longer hide every cabinet. Rebuild game data to enable this correction.

- **Station multi-tools with X-class upgrades**: Tools carrying any X-class (illegal) upgrade, a Sentinel upgrade or a salvaged "0" upgrade now appear, not just those with X-class scanners. In your normal game they no longer need No Man's Sky to be running. During an expedition they still need the game open.

- **Upgrade module classes**: Upgrade modules in inventory grids now show the game's C/B/A/S/X class badge, so an X-class upgrade no longer looks like an ordinary one. This applies everywhere, including your own multi-tools, ships and exosuit. Rebuild game data to enable the badges.

- **Station multi-tools in some normal saves**: Saves that still carry old expedition settings (for example "use legacy stations near the start") no longer hide every station tool in your normal game.

- **Current System live updates**: Keep unchanged planet cards, station multi-tool previews, market lists and system pictures in place. Damage-only tool updates retain the model and its rotation instead of recreating every card.

### New

- **Crashed ships**: Planet Details lists the 5 crashed ships nearest you, or nearest the planet's portal when you're not there, with their coordinates and distance. Each has an optional button to add a marker to your save (the game must be closed), so the game leads you to it. Each shows the name, type and class (C to S) of the ship lying there. For far-away wrecks the row warns that the game may lead the marker to a different wreck, so check the coordinates when you arrive. Rebuild game data to enable this.

- **Portal locations**: Every planet card on Current System shows where that planet's portal stands, in the same coordinates as your Analysis Visor. It's worked out from the game's own rules, so it works on planets nobody has visited. Open a planet to add a marker for its portal to your save; the game then leads you straight to it. Adding a marker needs No Man's Sky closed, and you save your changes as usual. Rebuild game data to enable this.

- **System map**: Current System shows the system you're in as a 3D map, with every planet and moon where the game actually places them and each moon tied to the planet it orbits. The space station is marked too, and so are you when you're in space (in your ship or aboard your freighter). The map starts looking down on the planets. Discovered planets show their colour and rings, undiscovered ones are a dashed outline, and the one you're on is outlined. Turn, move and zoom it like the region map, and click a planet to open it. It takes the place of the system picture and the orbit drawing.

- **Region map**: The Regions page has a 3D map of every star in the region, placed where the game puts them. Unvisited systems are bright yellow, visited ones are dim, unvisited purple systems are violet and your system is a large white dot. It turns around the star you pick, like the game's galaxy map: click a star to centre it and see its name, distance and portal glyphs, drag to turn, right-drag to move, and use the mouse wheel to zoom toward the pointer (or the arrow keys, + and -, and Home to return to your system). **Expand map** fills the page; Esc closes it. Distances are in light-years, the same as the game shows. Systems you haven't visited show how far away they are and can be sorted nearest first. It works in every region, including spiral-arm regions, where the stars curve along three arms. Rebuild game data to enable it.

- **Systems you haven't visited**: The Regions page shows how many star systems the region holds, how many you've visited, and lists every system you haven't been to yet with its name, star, race, economy and portal glyphs, ready to copy. Rebuild game data to enable it.

- **Region system totals**: Current System shows how many star systems your current region holds (and its purple systems), worked out with the game's own galaxy generator, plus how many of them you've visited. Rebuild game data to enable it.

- **Station multi-tools after reloading a save**: Current System shows supported station cabinets with the tool's name, class, damage, scanner range and system position. Results are tied to the latest save's reload location; unsupported cases stay hidden.
  - Compact cards: a small 3D preview beside the details, and a row of installed technology with class badges. **Show all slots** opens the full inventory grid.
  - Includes reload locations on a system's additional prime bodies, which previously hid the section.
  - Supports the recovered scanner module used by some station tools, allowing their inventories and scanner ranges to resolve.
  - Includes supported tools with X-class scanner upgrades that were previously missing from station listings. Rebuild game data to enable these cabinets.
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
- **Every planet's details before you land**: Current System shows every planet and moon in the system, found or not, with its type, weather (with extreme weather and storms), sentinel level (Low, High, Aggressive or Corrupted, as the game words them) and what its scanner lists - its plant (Star Bulb, Cactus Flesh...), the three resources you can mine, and Ancient Bones, Salvageable Scrap, Vile Brood or Swarm Debris where the game puts them. They're worked out from the system's address the same way the game does it, using your own game files, so you don't need to scan or record anything. Planets you open show them too.
- **Planet creature totals**: each planet shows how many kinds of fauna live on it ("2 / 7 fauna", or "no fauna" on barren and dead worlds) before you land or discover them, calculated using the game's creature generation tables from your install.
- **Planet creature totals**: each planet card on Current System and in planet details shows how many kinds of fauna live on it ("8 species", or "None" on dead and barren worlds) before you land or discover them, calculated using the game's creature generation tables from your install.
- **Possible system starships, S-Class stats & spawn chances**: Current System lists the starships that can appear in the system you're in — its 21 native starships (Exotic, Fighters, Haulers, Explorers, Shuttles and Solars) plus its **Sentinel Interceptor** when a dissonant planet is present — with each ship's exact **S-Class base stats** (Damage, Shield, Hyperdrive and Maneuverability), **max S-Class slots** (Cargo and Tech), and **S-Class spawn chance** for the system's economy (or crashed-only status in uncharted and abandoned systems).
- **Planet locations**: Planet Details shows surface coordinates for sites marked in your save and existing recorded Finds.

### Improved

- **Planet Details**: removed possible multi-tool and starship lists, class odds and hypothetical S-Class stats, along with unlocated building availability cards. Saved site markers remain separate from actual equipment details.

- **Frigates**: newly recruited ships appear quietly as the game saves. The fleet updates in place while keeping your selection and scroll position.
- **Frigates**: fleet cards and details show generated names for conventional and organic frigates. Organic frigates are labeled Organic in the fleet summary.
- **Planet Details**: notes, save facts, bases, finds, creatures and locations sections now have gradient headers with Show/Hide buttons that remember their state.
- **Current System**: the system-change notice stays centered in the app window, even when the page is scrolled.
- **Current System**: a full-page grey overlay with a centered spinner appears while system data is being prepared.
- **Collapsible sections**: Explore sections use the same themed gradient header and explicit Show/Hide button, with their open state remembered. Inventory grids use **Show all**/**Show less** labels instead of a chevron.
- **Guides**: opens with a full-page grey loading overlay and centered spinner while the guide list and catalog data load off the UI thread.
- **Guide**: opens with the same loading overlay; category cards are virtualized so opening the page doesn't create every card at once.
- **Frigates**: **Use generated name** clears a custom name and lets No Man's Sky name the frigate again.
- Explore: **renamed planets and systems** show the name the game shows, read from your save - your own renames and other players' - with the original name underneath ("Originally Irskya XIX"). No need to land on each one first.
- Explore: the Planets page is split in two. **Current System** shows the system you're in and its planets, with a System Data box laid out like the game's own; **Planet Finder** has the finder and the glyph decoder. Moons are now shown around the right planet.
- Starships: ships you found or bought are shown as a cyan **hologram scan** (their exact parts, but colours can't be shown yet). Ships built at a station, or repainted, show their real paint.
- Starships: a ship built at a station shows the parts it was built with, by name and icon.
- Starships: the name box selects the whole name when you click it, and **Save** appears as you type, without leaving the box.
- The reload notice says that reloading discards your unsaved changes.
- Ship Designer: Add, Apply and Repaint wait until No Man's Sky is closed, so changes can't be lost to the game's autosave.
- Starships shows how many of your starship slots are used, with the empty ones as placeholders (click one to open the Ship Designer). The Ship Designer shows slots used and free (e.g. "7/12 ship slots used · 5 free") before you add a ship.
- The Ship Designer works in narrower windows: the layout rearranges and the page scrolls.
- **Planetary hotspots**: pin individual resource or multi-hotspot combo cards to keep them visible at the top of the grid while exploring. Pinned cards feature a glowing accent border and stay in place as you move across the planet.
- **Save Beacons at hotspots**: placed beacons now match the planet's actual terrain elevation, bringing the in-game HUD marker down from the sky to ground level.
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
- **Station multi-tool damage**: When the supported running game has no overheat animation for the currently equipped tool, use that preview state for cabinet damage instead of assuming a loaded animation. This preserves projectile damage and corrects affected laser values.
- **Station multi-tool cards**: Tools now appear in cards that wrap with the window width, with a rotatable 3D preview using the existing seed-based model and colour renderer. Unchanged live updates preserve each preview.
- **Station multi-tool technology grids**: Each listed cabinet tool now shows its generated technology layout, installed technologies, empty and locked slots, supercharged slots and slot counts in a read-only grid.
- **Pirate station multi-tools**: Current System can now list supported pirate-station cabinets, using the selected station shape and the actual weapon-shop cabinet position. Pirate tools show their name, class and scanner range; damage is omitted until its display calculation is verified. Rebuild game data to load the new cabinet geometry.

- Current System keeps planets and moons in a collapsible section before station stock.
- Current System no longer shows generated Multi-Tool pool possibilities.
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
- Current System lists every planet and moon in the system, including ones nobody has discovered yet, with its type (lush, toxic, frozen...) worked out the way the game does - no scanning or typing needed. Matched the game on 1,072 of 1,074 planets in a real save. Needs a game data rebuild (Settings > Game data).
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
