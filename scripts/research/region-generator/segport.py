# Port of NMS.exe 180836 region path-segment builder 0x14136A930, diffed against the emulator.
# usage: python segport.py <galaxy> <x> <y> <z>   (hex region words)
import os, sys, struct
SCR = os.path.dirname(os.path.abspath(__file__))
galaxy, X_, Y_, Z_ = int(sys.argv[1]), int(sys.argv[2], 16), int(sys.argv[3], 16), int(sys.argv[4], 16)
sys.argv = [sys.argv[0], 'unused.hg.json']  # the shared header only needs a path
exec(compile(open(os.path.join(SCR, 'regionbatch.py')).read().split('\nroot = json.loads')[0], 'regionbatch.py', 'exec'))

# ---------- emulator: segment list the game builds into slot+0x1D0 ----------
def emulated_segments():
    stars.clear(); heap = E.heap[0]
    slot = E.alloc(0x280); E.call(0x1404C5620, (slot,)); offset = E.alloc(0x10)
    E.call(0x141367810, ((galaxy << 32) | (Y_ << 24) | (Z_ << 12) | X_, slot, offset))
    n = rd(slot + 0x1D4, '<I')[0]; arr = rd(slot + 0x1D8, '<Q')[0]
    segs = [(rd(arr + 32 * i, '<4f'), rd(arr + 32 * i + 16, '<4f')) for i in range(n)]
    E.heap[0] = heap
    return segs

# ---------- port ----------
M32 = 0xFFFFFFFF
def f32(x): return struct.unpack('<f', struct.pack('<f', x))[0]
def s32(v): v &= M32; return v - (1 << 32) if v >> 31 else v
def mix64(v):
    v &= (1 << 64) - 1
    v = ((v >> 33) ^ v) * 0x64DD81482CBD31D7 & (1 << 64) - 1
    v = ((v >> 33) ^ v) * 0xE36AA5C613612997 & (1 << 64) - 1
    return (v >> 33) ^ v
class Stream:
    def __init__(self, lo, carry): self.lo, self.carry = lo, carry
    def word(self):
        v = self.lo * 0x5A76F899 + self.carry
        self.lo, self.carry = v & M32, v >> 32
        return self.lo
def unit(word): return f32(word * 2.3283064370807974e-10)

def face_hash(face, X, Z, Y):
    # jump table 0x14136B2C4: (r9, eax, edx) per face; region words are (X, Z, Y)
    r9, a, d = [(Z - 1, X, Y), (Z, X, Y), (Y - 1, Z, X), (Y, Z, X), (X - 1, Z, Y), (X, Z, Y)][face]
    d = (d ^ a) & M32
    t = s32(r9 - 0x80000000)
    rcx = t & ((1 << 64) - 1)                    # movsxd
    r8 = rcx if r9 >= 0 else (t & 0x7FFFFFFF)    # btr r8d,31 zero-extends; cmovns keeps rcx
    c = ((r8 & 0xFFFF) + d) & M32
    h = (((r8 & 0xFFFFFFFF) >> 5) & 0x7FFF800) ^ (c << 16 & M32) ^ c
    hi = r8 >> 32
    h = (h + (h >> 11) + (hi & 0xFFFF)) & M32
    h2 = ((hi & 0xFFFFFFFF) >> 5 & 0x7FFF800) ^ (h << 16 & M32) ^ h
    c = (h2 + (h2 >> 11)) & M32
    c = (c ^ (c << 3)) & M32; c = (c + (c >> 5)) & M32
    c = (c ^ (c << 4)) & M32; c = (c + (c >> 17)) & M32
    c = (c ^ (c << 25)) & M32; c = (c + (c >> 6)) & M32
    return c

def build(galaxy, X, Y, Z):
    P = [None] * 7
    M = [[-2] * 6 for _ in range(7)]
    pt = [0] * 6
    # Region words are signed (12-bit X/Z, 8-bit Y), as 0x140384C20 sign-extends them.
    sx = X - 0x1000 if X & 0x800 else X; sz = Z - 0x1000 if Z & 0x800 else Z; sy = Y - 0x100 if Y & 0x80 else Y
    for face in range(6):
        h = face_hash(face, sx, sz, sy)
        s = Stream(h if h else 1, ((h >> 16) | (h << 16)) & M32 ^ h)
        u = unit(s.word()); v = unit(s.word())
        idx = face; pt[face] = idx
        M[idx][face] = -1
        P[idx] = [(u, v, -1.0), (u, v, 1.0), (u, -1.0, v), (u, 1.0, v), (-1.0, u, v), (1.0, u, v)][face]
    for i in range(6):
        for j in range(i + 1, 6):
            a, b = pt[i], pt[j]
            d = sum(f32(f32(P[a][k] - P[b][k]) ** 2) for k in range(3)) ** 0.5
            if d <= 1.0:
                M[a][j] = b; M[b][i] = a
    # centre point from the region-coordinate stream
    packed = ((((Y << 12 | (Z & 0xFFF)) & 0xFFFFF) | galaxy << 20) << 12) | (X & 0xFFF)
    h = mix64(packed)
    lo = h & M32
    s = Stream(lo if lo else 1, ((lo >> 16) | (lo << 16)) & M32 ^ (h >> 32) ^ lo)
    w1 = s.word(); w2 = s.word(); w3 = s.word()
    centre = 6
    P[centre] = (f32(unit(w1) - 0.5), f32(unit(w2) - 0.5), f32(unit(w3) - 0.5))
    # Remaining unlinked (-2) entries become the centre; the game's hash set then emits each
    # undirected edge once, in row-major order of the face rows (0x14136AF30..0x14136B285).
    segs, seen = [], set()
    for i in range(6):
        for j in range(6):
            m = M[i][j]
            if m == -2: m = centre
            if m == -1 or (m, i) in seen or (i, m) in seen: continue
            seen.add((m, i)); seen.add((i, m))
            segs.append((tuple(f32(c * 50.0) for c in P[i]), tuple(f32(c * 50.0) for c in P[m])))
    return P, M, segs

if len(sys.argv) > 1 and sys.argv[1] == 'batch':
    pass
P, M, port_segs = build(galaxy, X_, Y_, Z_)
segs = emulated_segments()
same = [(tuple(a[:3]), tuple(b[:3])) for a, b in segs] == port_segs
print('SEGMENTS', 'MATCH' if same else 'DIFF', len(segs), len(port_segs))
print('emulated segments', len(segs))
for s in segs: print('  ', tuple(round(c, 4) for c in s[0][:3]), '->', tuple(round(c, 4) for c in s[1][:3]))
print('port points (x50):')
for i, p in enumerate(P): print('  ', i, tuple(round(c * 50, 4) for c in p))
print('port matrix', M)
