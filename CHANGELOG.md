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

### Fixed

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

- 3D previews were drawn mirrored (left and right swapped).
- Explorer wings: the left and right wing were mixed up in the preview, and an Explorer added from the designer came out with the wrong wings in game.
- Some builder-only parts (e.g. the Explorer's Skywave, Prismatic, Planar and Vortex wings) all previewed alike.
- Starships: switching ships kept showing the previous ship for a few seconds, and the parts list could appear twice.
- If ArtifactX closes unexpectedly, the error is now written to `%AppData%\ArtifactX\logs\crash.log`, so it can be reported.

## 1.0.0-alpha1 — 2026-10-15

First public test release.
