# Python port of NMS.exe's terrain height 0x14128AA20 (build 183791; docs/research/PLANET_BUILDING_PLACEMENT.md) on a
# planet's TkVoxelGeneratorData: base-seed hash, per noise layer the evaluator 0x142C3D4E0 (core 0x142DE3F20 + scalar
# uber noise + region step 0x142C37340), and the layer combination. Checked against emulator heights (emu_height.py
# vectors). usage: python heightport.py <terrain.json> <hvec.json>
import json, sys, struct, math
import numpy as np
from uberport import uber, f, ONE, HALF, _rd
from fbmport import fbm

def rf(va): return np.frombuffer(_rd(va, 4), dtype=f)[0]
SEED_K = rf(0x144BECFF0)                                   # seed offset step (core and region mask)
RATIO_MIN = rf(0x142C3D84E + 0x1FAED3A)                     # max(RegionRatio, this) before sqrt (evaluator)
QUANT_MUL, QUANT_STEP = rf(0x142C375BA + 0x1FB5BDA), rf(0x142C37605 + 0x1FB2E9B)
REGION_OCTAVES = 3                                          # global 0x145097F6C (3 in the file)
REGION_RANGE = np.frombuffer(_rd(0x144AFAD00, 4 * 32), dtype=f)

UBER_FIELDS = ['AltitudeErosion', 'AmplifyFeatures', None, 'Gain', 'Lacunarity', None, 'PerturbFeatures', 'RemapFromMax',
               'RemapFromMin', 'RemapToMax', 'RemapToMin', 'RidgeErosion', 'SharpToRoundFeatures', 'SlopeBias', 'SlopeErosion', 'SlopeGain']

def layer_at(terrain, i):
    o = 0xAC0 + 0x84 * i; b = terrain[o:o + 0x84]
    fl = lambda off: np.frombuffer(b[off:off + 4], dtype=f)[0]
    u = {name: fl(4 * k) for k, name in enumerate(UBER_FIELDS) if name}
    return dict(uber=u, octaves=struct.unpack_from('<i', b, 0x14)[0], noise_type=struct.unpack_from('<i', b, 0x08)[0],
                Height=fl(0x40), RegionGain=fl(0x5C), RegionRatio=fl(0x60), RegionScale=fl(0x64), SeedOffset=struct.unpack_from('<i', b, 0x68)[0],
                PlateauRegionSize=fl(0x50), PlateauSharpness=struct.unpack_from('<i', b, 0x54)[0], PlateauStratas=fl(0x58),
                Width=fl(0x7C), Active=b[0x80], Subtract=b[0x81])

def seed_offsets(seed):
    q13 = int(seed / 13) if seed >= 0 else -int(-seed / 13); q17 = int(seed / 17) if seed >= 0 else -int(-seed / 17)
    return f(f(seed - 13 * q13) * SEED_K), f(f(seed - 17 * q17) * SEED_K)

def region_mask(x, y, z, scale, octaves, seed):
    """0x142DE3E70 -> 0x1419BE730: fBm at (p + offsets) / scale, normalised by the per-octave-count range table."""
    o13, o17 = seed_offsets(seed)
    inv = f(ONE / scale)
    q = (f(f(x + o13) * inv), f(f(y + o13) * inv), f(f(z + o17) * inv))
    v = fbm(q, octaves)
    lo, hi = REGION_RANGE[2 * octaves - 2], REGION_RANGE[2 * octaves - 1]
    return f(f(v - lo) / f(hi - lo))

def gain(m, power):
    t = m if HALF > m else f(ONE - m)
    g = f(f(float(f(t + t)) ** float(power)) * HALF)      # powf: double result rounded to float (as the CRT / emulator)
    return g if HALF > m else f(ONE - g)

def layer_value(L, x, y, z, seed):
    o13, o17 = seed_offsets(seed)
    inv = f(ONE / L['Width'])
    q = (f(f(x + o13) * inv), f(f(y + o13) * inv), f(f(z + o17) * inv))
    noise = uber(q, L['octaves'], L['uber'])
    sr = f(np.sqrt(max(L['RegionRatio'], RATIO_MIN)))
    c = min(ONE, max(f(0), noise))
    v = f(f(c - f(ONE - sr)) * f(ONE / sr))
    m = ONE
    if L['RegionScale'] > ONE and L['RegionRatio'] > 0 and ONE > L['RegionRatio']:
        srr = f(np.sqrt(L['RegionRatio']))
        mask = region_mask(x, y, z, f(L['RegionScale'] * L['Width']), REGION_OCTAVES, seed)
        mm = min(ONE, max(f(0), f(f(mask - f(ONE - srr)) * f(ONE / srr))))
        m = gain(mm, L['RegionGain'])
    v = f(v * m)
    if L['PlateauStratas'] != 0:
        pm = f(0)
        if L['PlateauRegionSize'] != 0: pm = region_mask(x, y, z, L['PlateauRegionSize'], 1, seed + 1)
        s = L['PlateauStratas']; sv = f(s * v); fl = f(np.floor(sv))
        frac = min(ONE, max(f(0), f(sv - fl)))
        plateau = f(f(gain(frac, f(L['PlateauSharpness'])) + fl) * f(ONE / s))
        v = f(f(f(v - plateau) * pm) + plateau)
    v = f(f(f(v * L['Height']) * QUANT_MUL) + HALF)
    t = int(np.trunc(v)); fv = f(t) - (ONE if f(t) > v else f(0))
    return f(fv * QUANT_STEP)

def base_hash(seed64):
    M = 0xFFFFFFFF
    lo = seed64 & M; hi = seed64 >> 32
    ecx = ((lo & 0xFFFF) + 0x1B873593) & M
    edx = ((lo >> 5) & 0x7FFF800) ^ ((ecx << 16) & M) ^ ecx
    ecx = edx; edx = (edx + (hi & 0xFFFF)) & M; edx = (edx + (ecx >> 11)) & M
    r8 = ((hi >> 5) & 0x7FFF800) ^ ((edx << 16) & M) ^ edx
    ecx = ((r8 >> 11) + r8) & M
    edx = ((ecx << 3) & M) ^ ecx
    ecx = ((edx >> 5) + edx) & M
    edx = ((ecx << 4) & M) ^ ecx
    ecx = ((edx >> 17) + edx) & M
    eax = ((ecx << 25) & M) ^ ecx
    return ((eax >> 6) + eax) & M

def base_height(terrain, p, radius):
    """0x14128CA10: only the base (layers 7 and 6, combined as in height) at normalize(p) * radius; the flatness
    test samples the ground with it."""
    import math
    x, y, z = (f(c) for c in p)
    inv = f(ONE / f(np.sqrt(f(f(f(x * x) + f(y * y)) + f(z * z)))))
    q = (f(f(x * inv) * f(radius)), f(f(y * inv) * f(radius)), f(f(z * inv) * f(radius)))
    h = base_hash(struct.unpack_from('<Q', terrain, 0xAB0)[0])
    Ls = [layer_at(terrain, i) for i in range(8)]
    def seed(i): return ((Ls[i]['SeedOffset'] + i + h + 2**31) % 2**32) - 2**31
    h7 = layer_value(Ls[7], *q, seed(7)) if Ls[7]['Active'] else f(0)
    h6 = layer_value(Ls[6], *q, seed(6)) if Ls[6]['Active'] else f(0)
    base = max(h6, f(0))
    if Ls[7]['Active']: base = f(f(f(h7 / Ls[7]['Height']) * base) + h7)
    return base

def height(terrain, p):
    x, y, z = (f(c) for c in p)
    h = base_hash(struct.unpack_from('<Q', terrain, 0xAB0)[0])
    Ls = [layer_at(terrain, i) for i in range(8)]
    def seed(i): return (Ls[i]['SeedOffset'] + i + h) & 0xFFFFFFFF if False else ((Ls[i]['SeedOffset'] + i + h + 2**31) % 2**32) - 2**31
    h7 = layer_value(Ls[7], x, y, z, seed(7)) if Ls[7]['Active'] else f(0)
    h6 = layer_value(Ls[6], x, y, z, seed(6)) if Ls[6]['Active'] else f(0)
    base = max(h6, f(0))
    if Ls[7]['Active']: base = f(f(f(h7 / Ls[7]['Height']) * base) + h7)
    acc = np.frombuffer(terrain[0x1140:0x1144], dtype=f)[0]
    for i in range(4):
        if not Ls[i]['Active']: continue
        v = layer_value(Ls[i], x, y, z, seed(i))
        acc = min(f(acc - v), acc) if Ls[i]['Subtract'] else max(acc, v)
    return f(acc + base)

if __name__ == '__main__':
    J = json.load(open(sys.argv[1])); terrain = bytes.fromhex(J['terrain'])
    V = json.load(open(sys.argv[2]))
    ok = 0; bad = []
    for v in V:
        got = height(terrain, v['point'])
        if struct.unpack('<I', struct.pack('<f', float(got)))[0] == v['bits']: ok += 1
        elif len(bad) < 5: bad.append((float(got), v['height']))
    print(f'{ok}/{len(V)} bit-exact', bad)
