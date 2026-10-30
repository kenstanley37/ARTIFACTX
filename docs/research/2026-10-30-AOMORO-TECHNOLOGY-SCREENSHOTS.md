# Aomoro technology screenshot reference - reviewed 2026-10-30

Source: owner-provided `Aomoros Resistance.odp`, 23 screenshot slides.
The owner confirmed this is the first of five same-named wrecks on the planet,
not necessarily the original wreck at -39.87, +120.99. Do not combine its stats,
site seed, intrinsic rolls or upgrade seed with the original reference.
The new wreck's exact coordinates/site seed have not been identified.

## New reference shown throughout the document

S-class Exotic, Aomoro's Resistance. Pre-Compare interaction screen:
Damage Potential 355.7; Shield Strength 338.7; Hyperdrive Range 179.1;
Maneuverability 530.3. These are a distinct reference from the original
Compare-screen 387.1 / 305.6 / 190.1 / 522.2. Screen context also differs.

Technology positions below are zero-based (column, row), read directly from the screenshots.

| Position | Technology | State | Evidence slide |
| --- | --- | --- | --- |
| (0,0) | Pulse Engine | Critically damaged, charged 100% | 2 |
| (1,0) | Launch Thruster | Working, charged 100% | 3 |
| (2,0) | Hyperdrive | Critically damaged, charged 0% | 4 |
| (3,0) | Deflector Shield | Critically damaged, charged 100%, supercharged slot | 5 |
| (4,0) | Photon Cannon | Critically damaged; tooltip Peak Damage Potential 20,193 DPS | 6 |
| (5,0) | Induction Polarizer, A-class Deflector Shield Upgrade | Critically damaged; Shield Strength +27% | 7 |

There is only one installed upgrade beyond the five basic technologies.
Other occupied technology cells are damaged-slot components, not additional weapons or upgrades:

| Component | Positions | Evidence slides |
| --- | --- | --- |
| Rusted Circuits | (6,0), (7,0) | 8-9 |
| Shattered Bulwark | (9,0), (0,1) | 10-11 |
| Radiation Leak | (2,1), (3,1), (5,1) | 12-14 |
| Containment Failure | (6,1), (9,1) | 15-16 |
| Damaged Gears | (0,2), (1,2), (3,2) | 17-19 |
| Hydraulics Damage | (4,2), (6,2), (7,2) | 20-22 |
| Exploded Panel | (9,2) | 23 |

Supercharged cells visible: (3,0), (1,2), (6,2). A fourth may be unavailable
or not visibly distinguishable; do not infer it from these screenshots alone.
The damage-component tooltip explicitly says core starship functionality is
not affected; these components reserve storage/technology slots until repaired.
Cargo is visible on the overview but was not separately inspected by tooltip.

## Consequences for the investigation

- The screenshots resolve the technology identities and their damaged states for this new wreck.
- They do not identify its site seed or intrinsic rolls, so no full numeric match is claimed.
- The A-class shield module is a useful new constraint on selection and module materialization.
- The photon tooltip's 20,193 DPS and aggregate 355.7 are different measurements;
  preserve the exact screen contexts when tracing them.
- No repair, save edit or runtime modification was performed during this review.

## Candidate recovered from procedural site enumeration (2026-10-30)

A read-only scratch runner enumerated the six faces of planet 2 in system
04095B6DA013, using the installed catalog's building placement, ship pool and
inventory rules. It found 15 Exotic candidates before terrain rejection.
Exotics must be selected by their shown S-class, not by discarding every site
whose underlying inventory class roll is not S. The existing application already
handles shown class through `CrashedShipSite.ShownClass` and effective class in
`StarshipInventoryGeneration`; no product class change was made.

Candidate `DA66E63ACBED9173`, latitude +38.23834, longitude -63.68751, has
intrinsic rolls 74.98706 / 74.01346 / 78.12094 / 41.98331. This is an inferred
candidate, not an owner-confirmed location. Its hyperdrive value uniquely rounds
to the screenshot's 179.1 among these 15 candidates.

With that site key and the existing native-test context (normal ship category 6,
wealth-budget row 0, 28 technology slots, known-tech/level inputs from Slot 8):

- Native selector and placement produce the five basic technologies followed by
  `UP_S_SHL3#99948` at (5,0), matching the screenshot's single A-class upgrade.
- The supplied procedural-roll implementation gives additive shield 0.1779587269;
  divided by the base shield 0.65 this is 27.378%, consistent with the +27% tooltip.
  Module materialization is still substituted; this is not a full native materializer proof.
- Native broken-technology results are damaged / working / damaged / damaged /
  damaged / damaged, matching all six screenshot tooltips.
- Native final shield = 338.7223816, rounding to screenshot 338.7.
- Native final hyperdrive = 179.1209412, rounding to screenshot 179.1.
- No native unmapped input reads occurred. GetTickCount64 remains an explicit
  zero-return emulator import; do not infer full game initialization from this harness.

Damage and maneuverability are still unresolved: the same supplied inventory with
old active-resource seed F4A8AD8D781A4F1C gives damage 303.2008972 and maneuverability
503.3550415, versus screenshot 355.7 / 530.3. The damage stat query's filter type 1
excludes the damaged Photon Cannon, while types 0 and 2 include its 559.9586182
intrinsic-adjusted damage, 7.5 fire rate and 1500 range. The screenshot's Photon
Cannon tooltip shows 20,193 DPS, so do not equate that tooltip to this isolated
supplied-inventory result. Correct caller/filter/resource context must be traced.

Harness correction: the substituted procedural template's Group string belongs
at +0x50, not +0x00. Corrected in scratch only. This did not change the old shield
module group, which already matched its template, but avoids corrupting the clone colour.

Scratch commands: `shipstatscheck --screenshot-sites`, then
`SHIP_SITE_SEED=DA66E63ACBED9173 shipstatscheck --module-rolls`, then
`ship-reference-native.py 0 --placement` with the same environment variable.
The exported module JSON now belongs to this candidate; regenerate it with the
original site key before rerunning the original wreck's oracle.
