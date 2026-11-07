# Port of a regular site's cluster layout (build 184271; docs/research/PLANETARY_MULTITOOL_LOCATIONS.md):
#  - member choice, 0x141635570: the cluster layout's entries (GcBuildingClusterLayoutEntry Max/Min/Probability/Building)
#    on the site key's MWC; Shelter entries gated by GcBuildingGlobals.ChanceOfAddingShelter on a side stream;
#  - placement, 0x1416360A0's first loop (first 3 members, no draws): member i at A + D_i * dir(theta_i) on the tangent
#    plane, D_i = r0 + a0 + ri + ai, theta advanced by asin((ri + ai) / D_i) degrees twice per member;
#  - re-centring, 0x1412B6BA0: every building (main included) moves by -sum(r_i (p_i - A)) / sum(r_i).
# r = building record radius (+0xC0), a = margin (+0xA4). Radii are inputs here: they come from each record's generated
# L-system sample bounds (half the XZ diagonal, at least GcBuildingGlobals.MinRadius), not ported yet.
# usage: python clusterport.py   (checks the Kaim 30/19 shops against live pre-shift records and the shop-1 root node)
import math
import numpy as np

M = 0x5A76F899
CHANCE_OF_ADDING_SHELTER = 0.75   # GcBuildingGlobals +0xB94 (live, build 184271)
SHELTER, WAYPOINT = 2, 7


def step(s):
    return ((s & 0xFFFFFFFF) * M + (s >> 32)) & 0xFFFFFFFFFFFFFFFF


def choose_members(key, layout, types):
    """layout: [(cls, min, max, probability)]; types: the planet's building-type classes in order. -> record indices."""
    lo, hi = key & 0xFFFFFFFF, key >> 32
    carry = (((lo >> 16) | (lo << 16)) & 0xFFFFFFFF) ^ hi ^ lo
    s = step((lo or 1) * M + carry)          # slot draw (0x1412B6BA0 passed the key's MWC stepped once)
    s = step(s)                              # chance draw; this is *param_2 from here on
    side = step(s)                           # side stream (not written back): its first word gates shelters
    shelter_roll = (side & 0xFFFFFFFF) * 2.0 ** -32
    chosen = []
    for cls, lo_n, hi_n, p in layout:
        if cls == SHELTER:
            if not shelter_roll < CHANCE_OF_ADDING_SHELTER:
                continue
        elif p < 1.0:
            s = step(s)
            if not (s & 0xFFFFFFFF) * 2.0 ** -32 < p:
                continue
        s = step(s)
        count = (((s & 0xFFFFFFFF) * (hi_n - lo_n + 1)) >> 32) + lo_n
        for _ in range(count):
            first = next((i for i, c in enumerate(types) if c == cls), 0)
            same = sum(1 for _ in iter_same(types, first, cls))
            s = step(s)
            chosen.append(first + (((s & 0xFFFFFFFF) * (same + 1)) >> 32))
            # (seeds for the chosen members take further draws; classes 2/7/21 from the side stream - not needed here)
    return chosen


def iter_same(types, first, cls):
    for c in types[first + 1:]:
        if c != cls:
            return
        yield c


def place_members(A, radii, margins=None):
    """Pre-shift positions of members 1..3 (0x1416360A0 first loop). A: planet-relative point A; radii[0] = main."""
    margins = margins or [0.0] * len(radii)
    A = np.array(A, dtype=np.float64)
    n = A / np.linalg.norm(A)
    ax = int(np.argmax(np.abs(A)))
    v = np.zeros(3)
    v[[2, 2, 1][ax]] = 1.0                   # face table 0x145A01AB0 via the dominant axis (start-up data, read live)
    t1 = np.cross(n, v)
    t2 = np.cross(n, t1)
    acc, out = np.float32(0), []
    for r, a in list(zip(radii, margins))[1:4]:
        d = np.float32(radii[0] + margins[0]) + np.float32(r + a)
        t = min(1.0, max(-1.0, (r + a) / d))
        step_deg = np.float32((math.pi / 2 - math.acos(t)) * 57.29578)
        acc = np.float32(acc + step_deg)
        th = float(acc * np.float32(0.017453292))
        acc = np.float32(acc + step_deg)     # 0x14163720C adds the step again
        direction = math.cos(th) * t1 + math.sin(th) * t2
        out.append(A + d * direction / np.linalg.norm(direction))
    return out


def recentre(A, positions, radii):
    A = np.array(A)
    shift = -sum(r * (np.array(p) - A) for p, r in zip(positions, radii)) / sum(radii)
    return [np.array(p) + shift for p in positions]


if __name__ == '__main__':
    # Kaim 30/19 (system 0x1FA00016452DB, planet 1): live type classes (types 0-3 Shelter, 8-10 Shop, 12 Waypoint)
    # and radii, read 2026-11-05 with read-only memory reads.
    types = [SHELTER] * 4 + [-1] * 4 + [5] * 3 + [-1] + [WAYPOINT]
    radius = {0: 21.379, 1: 23.329, 2: 21.255, 3: 21.379, 12: 10.0}
    layout = [(WAYPOINT, 1, 1, 1.0), (SHELTER, 1, 1, 1.0)]       # SHELTER_WAYPOIN, the Shop class's layout
    live = {
        0x8924A2E5C39F854D: ((116591.492, -15370.817, 55521.562), [(116587.938, -15430.443, 55512.520)]),
        0x66386EC5762B09FB: ((114312.875, -17583.473, 59463.180), [(114308.336, -17643.055, 59454.285), (114323.273, -17643.504, 59425.438)]),
        0x627968CDF4A27660: ((117627.625, -9044.394, 54721.324), [(117627.250, -9104.119, 54712.254), (117641.062, -9103.396, 54682.695)]),
    }
    for key, (A, members) in live.items():
        chosen = choose_members(key, layout, types)
        radii = [50.412] + [radius[i] for i in chosen]
        pred = place_members(A, radii)
        errs = [np.linalg.norm(p - np.array(m)) for p, m in zip(pred, members)]
        main = recentre(A, [A] + pred, radii)[0]
        print(f'{key:016X}: members {chosen} (live count {len(members)}), position errors '
              + ', '.join(f'{e:.3f} m' for e in errs) + f'; main shifted {np.linalg.norm(main - np.array(A)):.2f} m')
    # shop 1's spawned root node, horizontal offset from A (read live): (0.60, 9.89, 1.50)
    A = np.array(live[0x8924A2E5C39F854D][0])
    main = recentre(A, [A] + place_members(A, [50.412, 10.0]), [50.412, 10.0])[0]
    print('shop 1 predicted shift', np.round(main - A, 2), 'live root node (0.60, 9.89, 1.50)')
