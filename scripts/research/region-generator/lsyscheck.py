# Checks lsysport against the game's stored L-system samples read live (scripts/diagnostics/read_lsystem_samples.py):
# node models in order, positions and rotations relative to the root (the live root node holds the re-centring
# offset; the other nodes are in the root's frame), bounds-derived radius and root offset.
# usage: LSYS_DATA=<LsysDump folder> python lsyscheck.py <samples.json>
import json, sys
import numpy as np
from lsysport import generate, bounds, radius, short

live = json.load(open(sys.argv[1]))
total = ok_layout = ok_radius = 0
for ls in live:
    for k, s in enumerate(ls['samples']):
        total += 1
        nodes = generate(ls['path'], int(s['seed'], 16))
        names = [short(n.model) for n in nodes]
        want = [short(n['model']) for n in s['nodes']]
        pos = rot = 0.0
        for a, b in list(zip(nodes, s['nodes']))[1:]:
            q = np.array(b['m']).reshape(4, 4)                 # rows: X, Y, Z axes, position
            pos = max(pos, float(np.abs(a.world[:3, 3] - q[3, :3]).max()))
            rot = max(rot, float(np.abs(a.world[:3, :3].T - q[:3, :3]).max()))
        lo, hi = bounds(nodes)
        r, r_live = radius(lo, hi), max(ls['diag'][k] / 2, 10.0)
        offset = np.array([-(lo[0] + hi[0]) / 2, -(lo[2] + hi[2]) / 2])
        offset_live = np.array(s['nodes'][0]['m'])[[12, 14]]
        layout = names == want and pos < 1e-3 and rot < 1e-4
        ok_layout += layout
        ok_radius += abs(r - r_live) < 5e-4
        print(f"{short(ls['path'])[:34]:34} {k}: layout {'OK ' if layout else 'DIFF'} {len(names)}/{len(want)} nodes "
              f"(pos {pos:.4f}, rot {rot:.1e}); radius {r:8.3f} live {r_live:8.3f}; root offset error "
              f"{np.abs(offset - offset_live).max():.3f}")
print(f'{ok_layout}/{total} layouts, {ok_radius}/{total} radii exact')
