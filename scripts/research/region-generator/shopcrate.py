# Vy'keen shop crate from the address (build 184271; docs/research/PLANETARY_MULTITOOL_LOCATIONS.md, "L-system expander
# ported"): planet seed -> stage seed 3 -> building-record seeds (0x14163B270 / 0x14163C300) -> the shop's and the
# shelter's L-system samples (lsysport) -> record radii -> cluster members, placement and re-centring (clusterport) ->
# radian facing -> crate (FLOORLAYOUT_NPC's WEAPONSCRATE_ reference) in the building frame. Compared with the three
# Kaim 30/19 cabinets (whole-metre world points that hash to the offered tools, MULTITOOL_GENERATION.md).
# usage: LSYS_DATA=<LsysDump folder> python shopcrate.py
import math
import numpy as np
from buildingseeds import stage_seeds, Mwc
from clusterport import choose_members, place_members, recentre, SHELTER, WAYPOINT, M
from lsysport import generate, bounds, radius, scene, short

C1, C2, Q = 0x64DD81482CBD31D7, 0xE36AA5C613612997, (1 << 64) - 1
SHOP = 'MODELS/PLANETS/BIOMES/COMMON/BUILDINGS/SHOP/SHOPWARRIOR.LSYSTEM.MBIN'
SHACK = 'MODELS/PLANETS/BIOMES/COMMON/BUILDINGS/SHACK/SHACK_WARRIOR.LSYSTEM.MBIN'


def record_seeds(planet_seed, count):
    """Two Voronoi draws on MWC(stage seed 3), then per building record two steps mixed into its L-system seed."""
    r = Mwc(stage_seeds(planet_seed)[3]); r.next(10000); r.next(10000)
    out = []
    for _ in range(count):
        t1 = r.lo * M + r.hi; a = t1 & 0xFFFFFFFF
        t2 = a * M + (t1 >> 32); r.lo, r.hi = t2 & 0xFFFFFFFF, t2 >> 32
        x = (((t2 & 0xFFFFFFFF) >> 1) ^ ((t2 << 32) & Q | a)) * C1 & Q
        x = ((x >> 33) ^ x) * C2 & Q
        out.append((x >> 33) ^ x)
    return out


def sample(path, seed, n):
    """Record sample n (n % 3): the L-system's seed with lo + 100 n."""
    s = (seed & ~0xFFFFFFFF) | (((seed & 0xFFFFFFFF) + 100 * (n % 3)) & 0xFFFFFFFF)
    nodes = generate(path, s)
    return nodes, *bounds(nodes)


def facing(A, key):
    """0x141635570: forward = the 0x140576380 basis axis projected on the tangent plane, turned about the up vector by
    (first MWC word of the site key >> 29) * 45 - an angle the game passes to sin/cos as radians."""
    n = A / np.linalg.norm(A)
    ax = int(np.argmax(np.abs(A))); pos = A[ax] > 0
    basis = {(0, True): ((1, 0, 0), (0, 1, 0)), (0, False): ((-1, 0, 0), (0, 0, 1)), (1, True): ((0, 1, 0), (0, 0, 1)),
             (1, False): ((0, -1, 0), (-1, 0, 0)), (2, True): ((0, 0, 1), (1, 0, 0)), (2, False): ((0, 0, -1), (0, 1, 0))}
    a, b = (np.array(v, float) for v in basis[(ax, pos)])
    X = np.cross(a, b); X /= np.linalg.norm(X)
    f0 = np.cross(np.cross(n, np.cross(X, a)), n); f0 /= np.linalg.norm(f0)
    lo, hi = key & 0xFFFFFFFF, key >> 32
    word = (((((lo >> 16) | (lo << 16)) & 0xFFFFFFFF) ^ hi ^ lo) + (lo or 1) * M) & 0xFFFFFFFF
    ang = (word >> 29) * 45.0
    return n, f0 * math.cos(ang) + np.cross(n, f0) * math.sin(ang)


if __name__ == '__main__':
    PLANET = 0x07E3F9990696DCC9     # Kaim 30/I9: SystemGenerator.Bodies(0x1FA00016452DB, galaxy 0) body 0
    seeds = record_seeds(PLANET, 13)
    # Kaim's record list (live type classes): 0-3 Shelter (1 override + 3 models), ..., 8-10 Shop, 12 Waypoint; an
    # L-system path is registered once, with its first record's seed; record i uses sample i % 3.
    shop, slo, shi = sample(SHOP, seeds[8], 0)
    r_shop = radius(slo, shi)
    r_rec = {i: radius(*sample(SHACK, seeds[0], i)[1:]) for i in range(4)}
    r_rec[12] = 10.0                # Waypoint: GcBuildingGlobals.MinRadius
    print(f'record seeds: shelter {seeds[0]:016X}, shop {seeds[8]:016X}')
    print(f'radii: shop {r_shop:.3f} (live 50.412); shelter records ' + ', '.join(f'{r_rec[i]:.3f}' for i in range(4))
          + ' (live 21.379, 23.329, 21.255, 21.379)')
    centre = np.array([(slo[0] + shi[0]) / 2, 0.0, (slo[2] + shi[2]) / 2])
    npc = next(n for n in shop if short(n.model) == 'FLOORLAYOUT_NPC')
    crate_root = (npc.world @ next(m for r, m in scene(npc.model).refs if 'CRATE_WEAPON' in r))[:3, 3]
    crate = crate_root - centre     # the stored sample is re-centred on its XZ bounds
    print(f'root offset ({-centre[0]:.3f}, 0, {-centre[2]:.3f}); crate in the root room ({crate_root[0]:.3f}, '
          f'{crate_root[1]:.3f}, {crate_root[2]:.3f}) -> building frame (side {crate[0]:.3f}, up {crate[1]:.3f}, '
          f'forward {crate[2]:.3f})')

    types = [SHELTER] * 4 + [-1] * 4 + [5] * 3 + [-1] + [WAYPOINT]
    # (site key, Core point A planet-relative, cabinet world point planet-relative: system point minus the floored
    # planet centre (-224000, 0, -134000)), read 2026-11-05.
    cabinets = [(0x8924A2E5C39F854D, (116591.49, -15370.817, 55521.562), (116843, -15386, 55638)),
                (0x66386EC5762B09FB, (114312.875, -17583.473, 59463.184), (114530, -17596, 59598)),
                (0x627968CDF4A27660, (117627.62, -9044.393, 54721.32), (117871, -9033, 54843))]
    for key, A, cab in cabinets:
        A, cab = np.array(A), np.array(cab, float)
        chosen = choose_members(key, [(WAYPOINT, 1, 1, 1.0), (SHELTER, 1, 1, 1.0)], types)
        radii = [r_shop] + [r_rec[i] for i in chosen]
        P = recentre(A, [A] + place_members(A, radii), radii)[0]
        n, F = facing(A, key); side = np.cross(n, F)
        shift = (P - A) - np.dot(P - A, n) * n
        miss = cab - (P + side * crate[0] + F * crate[2]); miss -= np.dot(miss, n) * n
        m = cab - P
        print(f'  shop {key:016X}: members {chosen}, shift {np.linalg.norm(shift):.3f} m; cabinet in the building frame '
              f'(side {np.dot(m, side):.2f}, forward {np.dot(m, F):.2f}); crate horizontal miss {np.linalg.norm(miss):.2f} m '
              f'(cabinet whole metres: up to ~0.7)')
