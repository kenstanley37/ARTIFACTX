# Runs the building-site Voronoi query of build 183791 (wrapper 0x142DE7B30 -> worker 0x1419BE7E0) in Unicorn and
# tests rules for which cell a point falls in against it (docs/research/PLANET_BUILDING_PLACEMENT.md).
# usage: python voronoi183791.py <sector seed> <radius> <divisions> <count>
import sys, struct, math, random
argv = sys.argv[:]
sys.argv = ['emu_uber.py', '0']
exec(open('emu_uber.py').read().split("# --- reference vectors for the scalar uber noise")[0])
from siteport import feature, key as site_key, f32
FACE = {(2, -1): 0, (2, 1): 1, (0, 1): 2, (0, -1): 3, (1, -1): 4, (1, 1): 5}
def call_raw(fn, regs, stack, xmms):
    rsp = (STACK + 0x3F0000) & ~0xF; rsp -= 0x200
    mu.mem_write(rsp, struct.pack('<Q', SENTINEL)); mu.reg_write(X.UC_X86_REG_RSP, rsp)
    for i, v in enumerate(stack): mu.mem_write(rsp + 0x28 + 8 * i, struct.pack('<Q', v))
    for r, v in zip((X.UC_X86_REG_RCX, X.UC_X86_REG_RDX), regs): mu.reg_write(r, v)
    for r, v in xmms: mu.reg_write(r, int.from_bytes(struct.pack('<f', v), 'little'))
    mu.emu_start(fn, SENTINEL, count=50_000_000)
def query(seed, radius, divisions, p):
    pos = alloc(16); mu.mem_write(pos, struct.pack('<4f', *p, 1.0))
    outs = [alloc(16) for _ in range(5)]
    call_raw(0x142DE7B30, (pos, seed), outs, ((X.UC_X86_REG_XMM2, radius), (X.UC_X86_REG_XMM3, divisions * 0.5)))
    rd = lambda a, fm: struct.unpack(fm, bytes(mu.mem_read(a, struct.calcsize(fm))))
    return rd(outs[0], '<Q')[0], rd(outs[1], '<f')[0], rd(outs[2], '<4f'), rd(outs[3], '<4f'), rd(outs[4], '<4f')
if __name__ == '__main__':
    seed, radius, divisions, n = int(argv[1]), float(argv[2]), int(argv[3]), int(argv[4])
    half = divisions // 2
    rnd = random.Random(9); stats = {'2d-nearest': 0, 'sphere-nearest': 0, 'key-ok': 0}
    for i in range(n):
        v = [rnd.gauss(0, 1) for _ in range(3)]; d = math.sqrt(sum(c * c for c in v)); p = [f32(c / d * radius) for c in v]
        k, fl, a, b, c = query(seed, radius, divisions, p)
        ax = max(range(3), key=lambda j: abs(p[j])); sign = 1 if p[ax] > 0 else -1; face = FACE[(ax, sign)]
        q = [x / abs(p[ax]) * half for x in p]
        oth = [j for j in range(3) if j != ax]
        cands = []
        for du in (-1, 0, 1, 2):
            for dv in (-1, 0, 1, 2):
                cell = [0, 0, 0]; cell[ax] = sign * half
                cell[oth[0]] = math.floor(q[oth[0]]) + du; cell[oth[1]] = math.floor(q[oth[1]]) + dv
                fp = feature(face, *cell); cands.append((cell, fp, site_key(fp, seed)))
        game = next((cc for cc in cands if cc[2] == k), None)
        if game: stats['key-ok'] += 1
        best2d = min(cands, key=lambda cc: (cc[1][oth[0]] - q[oth[0]]) ** 2 + (cc[1][oth[1]] - q[oth[1]]) ** 2)
        def sph(cc):
            fp = cc[1]; m = math.sqrt(sum(x * x for x in fp)); return sum((fp[j] / m * radius - p[j]) ** 2 for j in range(3))
        bestS = min(cands, key=sph)
        if best2d[2] == k: stats['2d-nearest'] += 1
        if bestS[2] == k: stats['sphere-nearest'] += 1
        if i < 3: print('game point A', a[:3], 'second', b[:3], 'third', c[:3], 'float', fl)
    print(n, 'queries:', stats)
