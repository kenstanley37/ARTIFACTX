# Python port of NMS.exe's site flatness test 0x14128C350 (build 183791; docs/research/PLANET_BUILDING_PLACEMENT.md),
# non-settlement path: 8 samples on a ring around the site (Rodrigues rotation of a tangent about the site's up axis,
# angle i*pi/4), the cheap base height at each (heightport.base_height), slope = max |h0 - hi| / 64. Ring radius = the
# building type's +0xC0, or 64 when building-data byte +0x4D is 0. Also returns the ring's min/max heights.
import struct
import numpy as np
from uberport import f, ONE, _rd
import heightport as hp

def _bits(b): return np.frombuffer(struct.pack('<I', b), dtype=f)[0]
PI, TWO_PI, HALF_PI, INV_TWO_PI = _bits(0x40490FDB), _bits(0x40C90FDB), _bits(0x3FC90FDB), _bits(0x3E22F983)
C0, C1, C2, C3 = _bits(0x348BDD11), _bits(0x37CFB4C2), _bits(0x3AB609AA), _bits(0x3D2AAAA3)
S0, S1, S2, S3, S4 = _bits(0x32CCAF86), _bits(0x3638B1AD), _bits(0x39500BB2), _bits(0x3C088885), _bits(0x3E2AAAAB)
def _reduce(x):
    q = f(x * INV_TWO_PI); q = f(q - f(0.5)) if x < 0 else f(q + f(0.5))
    return f(x - f(f(int(q)) * TWO_PI))
def game_cos(x):   # 0x140201520 (Core GameTrig.Cos)
    r = _reduce(x); sign = ONE
    if r > HALF_PI: r = f(PI - r); sign = f(-1)
    elif -HALF_PI > r: r = f(-PI - r); sign = f(-1)
    y = f(r * r); p = f(C1 - f(y * C0)); p = f(f(p * y) - C2); p = f(f(p * y) + C3); p = f(f(p * y) - f(0.5)); p = f(f(p * y) + ONE)
    return f(p * sign)
def game_sin(x):   # 0x140201600 (Core GameTrig.Sin)
    r = _reduce(x)
    if r > HALF_PI: r = f(PI - r)
    elif -HALF_PI > r: r = f(-PI - r)
    y = f(r * r); p = f(S1 - f(y * S0)); p = f(f(p * y) - S2); p = f(f(p * y) + S3); p = f(f(p * y) - S4); p = f(f(p * y) + ONE)
    return f(p * r)

STEP, RING_DEFAULT, SLOPE_K = (np.frombuffer(_rd(a, 4), dtype=f)[0] for a in (0x144BEC920, 0x144BECFF0, 0x144BEA850))
def v4(*a): return np.array(a, dtype=f)

def flatness(terrain, p4, planet_radius, type_radius, use_type_radius):
    P = v4(*p4)
    sq = P * P
    ln = f(np.sqrt(f(sq[2] + f(sq[1] + sq[0]))))
    n = np.full(4, f(ONE / ln), f) * P
    t = v4(f(n[0] * n[1]) - f(0), f(f(-n[2] * n[2]) - f(n[0] * n[0])) if False else f(f(f(-n[2]) * n[2]) - f(n[0] * n[0])), f(0 - f(f(-n[2]) * n[1])), f(n[3] * f(1) - n[3] * f(1)))
    h0 = hp.base_height(terrain, P[:3], planet_radius)
    tsq = t * t
    tl = f(np.sqrt(f(tsq[2] + f(tsq[1] + tsq[0]))))
    T = np.full(4, f(ONE / tl), f) * t
    radius = f(type_radius) if use_type_radius else RING_DEFAULT
    n0, n1, n2 = n[0], n[1], n[2]
    A20, A2_0, A21, A11, A10, A00 = f(n2 * n2), f(n2 * n0), f(n2 * n1), f(n1 * n1), f(n1 * n0), f(n0 * n0)
    maxslope = f(0); lo = h0; hi = h0
    for i in range(8):
        th = f(f(i) * STEP); s = game_sin(th); c = game_cos(th)
        omc = f(ONE - c)
        col0 = v4(f(f(omc * A00) + c), f(f(n2 * s) + f(omc * A10)), f(f(omc * A2_0) - f(n1 * s)), ONE)
        col1 = v4(f(f(omc * A10) - f(n2 * s)), f(f(omc * A11) + c), f(f(n0 * s) + f(omc * A21)), ONE)
        col2 = v4(f(f(n1 * s) + f(omc * A2_0)), f(f(omc * A21) - f(n0 * s)), f(f(omc * A20) + c), ONE)
        q = (((col0 * T[0]) + (col1 * T[1])) + (col2 * T[2])) * radius + P
        h = hp.base_height(terrain, q[:3], planet_radius)
        d = f(abs(f(h0 - h)) * SLOPE_K)
        maxslope = max(d, maxslope); lo = min(h, lo); hi = max(h, hi)
    return maxslope, lo, hi
