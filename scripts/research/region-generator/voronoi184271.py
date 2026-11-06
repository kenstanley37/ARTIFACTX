# Runs build 184271's building-site Voronoi query (wrapper 0x142DEC840 -> worker 0x1419C2CB0) in Unicorn on given
# queries and compares the key and point A with Core's PlanetBuildingSites (docs/research/PLANETARY_MULTITOOL_LOCATIONS.md).
# usage: python voronoi184271.py cells.json   (cwd = this folder; cells.json = {radius, divisions, sector, cabs: [{query,
#        cab, key, point}]} as written by a Core probe)
import sys, os, json, struct, math
cells = json.load(open(sys.argv[1]))
sys.argv = ['emu_uber.py', '0']
exec(open('emu_uber.py').read().split("# --- reference vectors for the scalar uber noise")[0])
def query(seed, radius, divisions, p):
    pos = alloc(16); mu.mem_write(pos, struct.pack('<4f', *p, 1.0))
    outs = [alloc(16) for _ in range(5)]
    rsp = (STACK + 0x3F0000) & ~0xF; rsp -= 0x200
    mu.mem_write(rsp, struct.pack('<Q', SENTINEL)); mu.reg_write(X.UC_X86_REG_RSP, rsp)
    for i, v in enumerate(outs): mu.mem_write(rsp + 0x28 + 8 * i, struct.pack('<Q', v))
    mu.reg_write(X.UC_X86_REG_RCX, pos); mu.reg_write(X.UC_X86_REG_RDX, seed)
    for r, v in ((X.UC_X86_REG_XMM2, radius), (X.UC_X86_REG_XMM3, divisions * 0.5)):
        mu.reg_write(r, int.from_bytes(struct.pack('<f', v), 'little'))
    mu.emu_start(0x142DEC840, SENTINEL, count=50_000_000)
    rd = lambda a, fm: struct.unpack(fm, bytes(mu.mem_read(a, struct.calcsize(fm))))
    return rd(outs[0], '<Q')[0], rd(outs[1], '<f')[0], rd(outs[2], '<4f'), rd(outs[3], '<4f'), rd(outs[4], '<4f')
R, D, seed = cells['radius'], cells['divisions'], cells['sector']
for c in cells['cabs']:
    for name, p in (('query', c['query']), ('cabinet', c['cab'])):
        k, fl, a, b, n = query(seed, R, D, p)
        core = c['point']
        dist = math.dist(a[:3], core)
        print(f"{name:8} game key {k:016X} core {c['key']}  {'SAME' if f'{k:016X}' == c['key'] else 'DIFF'}  "
              f"point A {tuple(round(x, 3) for x in a[:3])} vs core {tuple(round(x, 3) for x in core)}  |d| {dist:.3f} m  "
              f"dist out {fl:.3f}  cell {tuple(round(x, 3) for x in b[:3])} normal {n[:3]}")
