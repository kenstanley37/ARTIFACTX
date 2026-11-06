# Spawn height of a cluster building and the shop crate's whole-metre point, from the address (build 184271;
# docs/research/PLANETARY_MULTITOOL_LOCATIONS.md, "Spawn height from the address").
# Cluster buildings are created by the Far terrain region's decorator (0x1412B7F70) on that region's surface. Inside
# the cluster's flat zone every Far grid node holds (Height(A) - BaseHeight(node)) / 4, and the creator samples grid
# index floor(local / 16 m), which holds the node one cell back. So, for a building at point p:
#   spawn = Height(A) + BaseHeight(p) - plane(BaseHeight at nodes (i-1, j-1), (i, j-1), (i-1, j)), fractions of p,
# with i / j = cube z / y of p divided by 16 m (face +X here). The cabinet is rolled at spawn: crate = spawn origin +
# root offset + the FLOORLAYOUT_NPC WEAPONSCRATE_ point, rounded to whole metres.
# Checked 2026-11-06 on Kaim 30/19: 4/4 spawn heights within 2.6 cm of the live instances; 3/4 cabinets exact, the
# fourth 1 cm across a rounding boundary (the game works in float32, ~0.8 cm at these coordinates).
# usage: LSYS_DATA=<LsysDump folder> python shopspawn.py   (needs HeightProbe built and the player's catalog)
import json, math, os, subprocess, sys, tempfile
import numpy as np
from shopcrate import record_seeds, sample, facing, SHOP, SHACK
from clusterport import choose_members, place_members, recentre, SHELTER, WAYPOINT

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
PROBE = os.path.join(HERE, 'HeightProbe', 'bin', 'Release', 'net10.0', 'HeightProbe.dll')
CATALOG = os.path.expandvars(os.path.join('%APPDATA%', 'ArtifactX', 'GameData', 'nms_catalog.sqlite'))
R, CELL = 130048.0, 16.0          # Kaim 30/I9 base radius; Far grid cell (scale 4 x 4 local units)


def core_heights(points, address='1FA00016452DB', body='1'):
    """[[Height, BaseHeight]] at each planet-relative point's direction (Core, via HeightProbe)."""
    with tempfile.TemporaryDirectory() as d:
        src, dst = os.path.join(d, 'p.json'), os.path.join(d, 'v.json')
        json.dump([list(map(float, p)) for p in points], open(src, 'w'))
        subprocess.run(['dotnet', PROBE, CATALOG, address, body, src, dst], check=True, capture_output=True, cwd=REPO)
        return json.load(open(dst))


def spawn_inputs(p):
    n = p / np.linalg.norm(p); cube = n / n[0] * R      # face +X
    gi, gj = cube[2] / CELL, cube[1] / CELL
    i, j = math.floor(gi), math.floor(gj)
    nodes = [[R, (jj - 1) * CELL, (ii - 1) * CELL] for ii, jj in ((i, j), (i + 1, j), (i, j + 1))]
    return gi - i, gj - j, nodes


if __name__ == '__main__':
    seeds = record_seeds(0x07E3F9990696DCC9, 13)
    shop, slo, shi = sample(SHOP, seeds[8], 0)
    from lsysport import radius
    r_shop = radius(slo, shi)
    r_rec = {i: radius(*sample(SHACK, seeds[0], i)[1:]) for i in range(4)}; r_rec[12] = 10.0
    ro = np.array([-(slo[0] + shi[0]) / 2, 0.0, -(slo[2] + shi[2]) / 2])
    types = [SHELTER] * 4 + [-1] * 4 + [5] * 3 + [-1] + [WAYPOINT]
    C = np.array([-224000.0, 0, -134000.0])
    # (site key, Core point A, cabinet whole-metre point that reproduces the offered tool; owner screenshots)
    shops = [('404DA563E9C5EC88', (99954.344, 83115.625, -3661.159), (-123846, 83303, -137661)),
             ('8924A2E5C39F854D', (116591.49, -15370.817, 55521.562), (-107157, -15386, -78362)),
             ('66386EC5762B09FB', (114312.875, -17583.473, 59463.184), (-109470, -17596, -74402)),
             ('627968CDF4A27660', (117627.62, -9044.393, 54721.32), (-106129, -9033, -79157))]
    prep, pts = [], []
    for key, A, cab in shops:
        A = np.array(A)
        chosen = choose_members(int(key, 16), [(WAYPOINT, 1, 1, 1.0), (SHELTER, 1, 1, 1.0)], types)
        radii = [r_shop] + [r_rec[i] for i in chosen]
        P = recentre(A, [A] + place_members(A, radii), radii)[0]
        fi, fj, nodes = spawn_inputs(P)
        prep.append((key, A, P, cab, fi, fj, len(pts)))
        pts += [A, P] + nodes
    V = core_heights(pts)
    for key, A, P, cab, fi, fj, o in prep:
        b00, b10, b01 = V[o + 2][1], V[o + 3][1], V[o + 4][1]
        spawn = V[o][0] + V[o + 1][1] - (b00 + fi * (b10 - b00) + fj * (b01 - b00))
        n, F = facing(A, int(key, 16)); side = np.cross(n, F)
        crate = P / np.linalg.norm(P) * (R + spawn) + side * (ro[0] + 3.974) + n * 3.734 + F * (ro[2] - 5.0) + C
        pt = tuple(int(v) for v in np.floor(crate + 0.5))
        print(f'{key}: H(A) {V[o][0]:.3f} spawn {spawn:.3f}  crate {np.round(crate, 2)} -> {pt}  cabinet {cab}  '
              + ('MATCH' if pt == cab else f'differs by {np.round(np.array(cab) - crate, 2)}'))
