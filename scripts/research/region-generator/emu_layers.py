# Checks PlanetTerrain.CombineLayers' rule against the game's Far pass: runs emu_far.py (same arguments; ZERO / PATCH as
# there, LIVE=1 for live pages), captures every noise-layer column, prints grid*4 - formula per node; FIXTURE=name prints
# a PlanetSurfaceChecks case. Caves carve local dips: ZERO=10A0-1120 removes them. usage: python emu_layers.py <emu_far args>
# Wraps emu_far.py: optional patches to the voxel settings (PATCH="off:fmt:value;..." relative to TkVoxelGeneratorData),
# captures every noise-layer column (+0x8 + i*0x20) and the table scalars in the density pass, then compares the
# regenerated Far grid (local units * cell scale) with the candidate column formula at every node.
import os, sys, struct, re
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu_far.py')).read()
cap_old = "captured.update(pads=arr(0x390, 4), l0=arr(0x8), l6=arr(0xC8), l7=arr(0xE8), r=arr(0x3C0))"
cap_new = ("captured.update(cols=[arr(0x8 + 0x20 * i) for i in range(8)], r=arr(0x3C0), "
           "sea=struct.unpack('<f', bytes(mu.mem_read(tbl + 0x3B0, 4)))[0], lod=None)")
assert cap_old in src
src = src.replace(cap_old, cap_new)
src = src.split("json.dump(captured, open(sys.argv[4] + '.table.json', 'w'))")[0]
exec(src)
import numpy as np
grid = struct.unpack(f'<{stride * stride}f', bytes(mu.mem_read(grid_ptr, 4 * stride * stride)))
# --- the candidate: density-pass layer combination in 2D (FUN_1412b05d0 per layer, 184271)
o = bytes(mu.mem_read(VOX, 0x1150))
F = lambda off: struct.unpack_from('<f', o, off)[0]
I = lambda off: struct.unpack_from('<i', o, off)[0]
NOSEA, WFD = F(0x1140), F(0x114C)
LOD = int(os.environ.get('LOD', '2'))
cols = [np.array(c) if c is not None else None for c in captured['cols']]
sea = captured['sea']
n = stride * stride
pred = np.zeros(n)
for k in range(n):
    b = max(cols[6][k], 0.0)
    if o[0xAC0 + 7 * 0x84 + 0x80]: h7 = cols[7][k]; b = h7 / F(0xAC0 + 7 * 0x84 + 0x40) * b + h7
    S = NOSEA + cols[0][k]
    S0 = S
    for i in (1, 2, 3, 4):
        L = 0xAC0 + i * 0x84
        if not o[L + 0x80] or LOD > I(L + 0x48): continue
        H, Hoff, offt, smooth, wf_t, sub = F(L + 0x40), F(L + 0x44), I(L + 0x4C), F(L + 0x6C), I(L + 0x78), o[L + 0x81]
        wf = 1.0
        if wf_t == 2: wf = min(max(((sea - 1.0) - (S + b)) / WFD, 0.0), 1.0)
        elif wf_t == 1: wf = min(max(((S + b) - (sea - 1.0)) / WFD, 0.0), 1.0)
        ref = {1: S0, 2: S, 3: sea}.get(offt, 0.0) + Hoff
        inv = 0.0 if H == 0 else 1.0 / H
        v = cols[i][k]
        if not sub:
            cand = min(max(H, 0.0), 128.0 - ref) * inv * wf * v + ref
            Sn = max(S, cand)
            if smooth != 0:
                kk = max(smooth - abs(S - cand), 0.0)
                Sn = Sn + kk * kk * 0.25 / smooth
            S = Sn
        else:
            cand = ref - min(ref, max(H, 0.0)) * inv * wf * v
            S = min(S, cand)
    pred[k] = S
g = np.array(grid)
scale = float(os.environ.get('SCALE', '4'))
diff = g * scale - pred
print(f'nodes {n}: grid*{scale} - formula: mean {diff.mean():.3f} max|.| {np.abs(diff).max():.3f}   formula range {pred.min():.2f}..{pred.max():.2f}')
np.set_printoptions(linewidth=200, precision=2, suppress=True)
print(diff.reshape(stride, stride))
print('pads x', np.array(captured.get('pads') or [0]).size)
print('table sea', captured['sea'], 'voxel SeaLevel', F(0x1148), 'WFD', WFD, 'NoSea', NOSEA)
if os.environ.get('FIXTURE'):
    def layer(i):
        L = 0xAC0 + i * 0x84
        return f'L({F(L + 0x40)!r}f, {F(L + 0x44)!r}f, {I(L + 0x48)}, {I(L + 0x4C)}, {F(L + 0x6C)!r}f, {I(L + 0x78)}, {str(bool(o[L + 0x80])).lower()}, {str(bool(o[L + 0x81])).lower()})'
    nodes = []
    for k in (0, 29, 77, 130):
        b = max(cols[6][k], 0.0)
        cs = ', '.join(f'{cols[i][k]!r}f' for i in range(5))
        nodes.append(f'N([{cs}], {b!r}f, {g[k] * scale!r}f)')
    print(f'        new("{os.environ["FIXTURE"]}", [{", ".join(layer(i) for i in range(5))}], {NOSEA!r}f, {captured["sea"]!r}f, {WFD!r}f,\n            [{", ".join(nodes)}]),')
