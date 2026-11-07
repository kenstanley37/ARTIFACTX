# Python port of the scalar path of NMS.exe's simplex fBm 0x1419BBAE0 (build 183791; docs/research/PLANET_BUILDING_PLACEMENT.md):
# the only path its two terrain uses run (the site query's domain warp passes 2 octaves, the region mask 3; the
# 4-octaves-per-lane SIMD path needs more than 3). Same simplex as the uber noise (uberport.simplex), without the
# derivatives; sum += noise * amplitude, amplitude *= 0.5, frequency *= 2; result * norm[octaves] (0x144BE9418).
# usage: python fbmport.py <fbm4_vectors.json>
import json, sys, struct
import numpy as np
from uberport import simplex, f, ONE, HALF, _rd

NORM = np.frombuffer(_rd(0x144BE9418, 4 * 16), dtype=f)

def fbm(p, octaves):
    if octaves > 3: raise NotImplementedError('SIMD path (more than 3 octaves) not ported')
    px, py, pz = (f(c) for c in p)
    freq = ONE; amp = ONE; total = f(0)
    for _ in range(octaves):
        n, _ = simplex(f(px * freq), f(py * freq), f(pz * freq))
        total = f(total + f(n * amp))
        amp = f(amp * HALF); freq = f(freq + freq)
    return f(total * NORM[octaves])

if __name__ == '__main__':
    V = [v for v in json.load(open(sys.argv[1])) if v['octaves'] <= 3]
    ok = sum(struct.unpack('<I', struct.pack('<f', float(fbm(v['point'], v['octaves']))))[0] == v['bits'] for v in V)
    print(f'{ok}/{len(V)} bit-exact (octaves 1-3)')
