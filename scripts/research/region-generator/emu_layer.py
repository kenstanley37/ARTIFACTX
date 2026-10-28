# Per-layer reference values: runs NMS.exe's layer evaluator 0x142C3D4E0 (build 183791) in Unicorn on one point, called
# as the height function 0x14128AA20 calls it (rcx = layer, rdx/r8/r9 = &x/&y/&z, then &index, seed, &out, count 1),
# and compares with heightport.layer_value. usage: python emu_layer.py <terrain.json> <points json (hvec)>
import sys, json, struct, math
argv = sys.argv[:]
sys.argv = ['emu_uber.py', '0']
exec(open('emu_uber.py').read().split("# --- reference vectors for the scalar uber noise")[0])
import heightport as hp
perm = bytes(mu.mem_read(0x144AFAEA0, 256)); g16 = bytes(mu.mem_read(0x144AFADA0, 256))
mu.mem_write(0x146F45EE0, b''.join(g16[16 * (perm[i] & 15):16 * (perm[i] & 15) + 16] for i in range(256)))
def call_raw(fn, regs, stack):
    rsp = (STACK + 0x3F0000) & ~0xF; rsp -= 0x200
    mu.mem_write(rsp, struct.pack('<Q', SENTINEL)); mu.reg_write(X.UC_X86_REG_RSP, rsp)
    for i, v in enumerate(stack): mu.mem_write(rsp + 0x28 + 8 * i, struct.pack('<Q', v & (2**64 - 1)))
    for r, v in zip((X.UC_X86_REG_RCX, X.UC_X86_REG_RDX, X.UC_X86_REG_R8, X.UC_X86_REG_R9), regs): mu.reg_write(r, v)
    mu.emu_start(fn, SENTINEL, count=200_000_000)
J = json.load(open(argv[1])); terrain = bytes.fromhex(J['terrain']); V = json.load(open(argv[2]))[:40]
tbuf = alloc(0x1200, 0x40); mu.mem_write(tbuf, terrain)
px, py, pz, out, idx = alloc(16), alloc(16), alloc(16), alloc(16), alloc(16)
h = hp.base_hash(struct.unpack_from('<Q', terrain, 0xAB0)[0])
for li in range(8):
    L = hp.layer_at(terrain, li)
    if not L['Active']: continue
    seed = ((L['SeedOffset'] + li + h + 2**31) % 2**32) - 2**31
    ok, bad = 0, []
    for v in V:
        x, y, z = v['point']
        for a, c in ((px, x), (py, y), (pz, z)): mu.mem_write(a, struct.pack('<f', c))
        mu.mem_write(idx, struct.pack('<i', 0))
        call_raw(0x142C3D4E0, (tbuf + 0xAC0 + 0x84 * li, px, py, pz), (idx, seed & 0xFFFFFFFF, out, 1))
        ref = struct.unpack('<f', bytes(mu.mem_read(out, 4)))[0]
        mine = float(hp.layer_value(L, *(hp.f(c) for c in (x, y, z)), seed))
        if struct.pack('<f', ref) == struct.pack('<f', mine): ok += 1
        elif len(bad) < 3: bad.append((mine, ref))
    print(f'layer {li}: {ok}/{len(V)}', bad)
