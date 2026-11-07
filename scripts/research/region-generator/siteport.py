# Python port of the building-site Voronoi cell feature point and site key (NMS build 180836, worker 0x1419883E0;
# docs/research/PLANET_BUILDING_PLACEMENT.md). For a face index (0..5, the worker's r9) and an integer cell (X, Y, Z)
# in face units (the face axis is +-divisions/2), gives the feature point (the worker's [r13] output) and site key.
# usage: python siteport.py <sector seed> <face> <X> <Y> <Z>
import struct, sys
def f32(x): return struct.unpack('<f', struct.pack('<f', x))[0]
def bits(x): return struct.unpack('<I', struct.pack('<f', x))[0]
def from_bits(u): return struct.unpack('<f', struct.pack('<I', u & 0xFFFFFFFF))[0]
M = (1 << 64) - 1
T1 = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, -1), (0, 0, 1)]   # 0x1450AF140
T2 = [(0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1), (1, 0, 0), (-1, 0, 0)]   # 0x1450AF0F0
C1, C2, SCALE = 113.0, 57.0, f32(0.01)                                        # 0x144B29DC0, 0x144B29D58, 0x144B27480
def wang(u):
    x = (((u ^ 0x3D0000) >> 16) ^ u) & 0xFFFFFFFF
    x = (x * 9) & 0xFFFFFFFF
    x = ((x >> 4) ^ x) & 0xFFFFFFFF
    x = (x * 0x1B873593) & 0xFFFFFFFF
    return ((x >> 15) ^ x) & 0xFFFFFFFF
def jitter(u): return f32(f32(from_bits((wang(u) & 0x7FFFFF) | 0x3F800000) - 1.0) * SCALE)
def feature(face, X, Y, Z):
    X, Y, Z = f32(X), f32(Y), f32(Z)
    a = jitter(bits(f32(f32(f32(X * C1) + Y) + f32(Z * C2))))
    b = jitter(bits(f32(f32(f32(Y * C1) + f32(X * C2)) + Z)))
    c = jitter(bits(f32(f32(f32(Y * C2) + X) + f32(Z * C1))))
    t1, t2 = T1[face], T2[face]
    return (f32(f32(f32(t2[0] + t1[0]) * c) + X), f32(f32(f32(t2[1] + t1[1]) * b) + Y), f32(f32(f32(t2[2] + t1[2]) * a) + Z))
def mix64(r):
    r = ((r >> 33) ^ r) * 0x64DD81482CBD31D7 & M
    r = ((r >> 33) ^ r) * 0xE36AA5C613612997 & M
    return (r >> 33) ^ r
KM = 0x9DDFEA08EB382D69
def h128(u, v):
    a = ((u ^ v) * KM) & M; a ^= a >> 47
    b = ((v ^ a) * KM) & M; b ^= b >> 47
    return (b * KM) & M
def key(point, seed):
    h = [mix64((bits(c) << 32) | bits(f32(c * -2.0))) for c in point]
    return h128(seed & 0xFFFFFFFF, h128(h[2], h128(h[1], h[0])))
AXIS = {0: 2, 1: 2, 2: 0, 3: 0, 4: 1, 5: 1}; SIGN = {0: -1, 1: 1, 2: 1, 3: -1, 4: -1, 5: 1}
def nearest_site(p, seed, radius, divisions):
    """The site a planet-relative point falls in (the game's Voronoi query 0x142DE7B30, build 183791): for every face
    the point faces, the cells around its projection onto that face; a cell counts for a face only if its jittered
    feature point lies on that face (cells on cube edges belong to the face their jitter keeps them on); nearest
    feature point on the sphere wins. Returns (key, face, cell, distance). Checked 600/600 against the emulator
    (300 random points, 300 near edges and corners; voronoi183791.py)."""
    import math
    half = divisions // 2; best = None
    for face in range(6):
        ax = AXIS[face]
        if p[ax] * SIGN[face] <= 0: continue
        q = [c / abs(p[ax]) * half for c in p]; oth = [j for j in range(3) if j != ax]
        for du in range(-2, 3):
            for dv in range(-2, 3):
                cell = [0, 0, 0]; cell[ax] = SIGN[face] * half
                cell[oth[0]] = math.floor(q[oth[0]]) + du; cell[oth[1]] = math.floor(q[oth[1]]) + dv
                if abs(cell[oth[0]]) > half or abs(cell[oth[1]]) > half: continue
                fp = feature(face, *cell)
                if not (fp[ax] * SIGN[face] > 0 and all(abs(fp[ax]) > abs(fp[j]) for j in oth)): continue
                m = math.sqrt(sum(c * c for c in fp)); dist = sum((fp[j] / m * radius - p[j]) ** 2 for j in range(3))
                if best is None or dist < best[0]: best = (dist, key(fp, seed), face, tuple(cell))
    return best[1], best[2], best[3], math.sqrt(best[0])
if __name__ == '__main__':
    seed, face = int(sys.argv[1]), int(sys.argv[2])
    p = feature(face, *(float(v) for v in sys.argv[3:6]))
    print(f'feature {p}  key {key(p, seed):016X}')
