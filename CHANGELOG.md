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

### Fixed

- 3D previews were drawn mirrored (left and right swapped).
- Explorer wings: the left and right wing were mixed up in the preview, and an Explorer added from the designer came out with the wrong wings in game.
- Some builder-only parts (e.g. the Explorer's Skywave, Prismatic, Planar and Vortex wings) all previewed alike.
- Starships: switching ships kept showing the previous ship for a few seconds, and the parts list could appear twice.
- If ArtifactX closes unexpectedly, the error is now written to `%AppData%\ArtifactX\logs\crash.log`, so it can be reported.

## 1.0.0-alpha1 — 2026-10-15

First public test release.
