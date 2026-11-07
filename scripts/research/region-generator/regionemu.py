# Runs NMS.exe build 180836's region star generator 0x141367810 in Codex's Unicorn harness.
# The per-star callback 0x141369C30 is replaced by a recorder: it notes each emitted star
# and bumps the counter word the real callback increments, then returns.
# usage: python -I regionemu.py <galaxy> <x hex> <y hex> <z hex>
import os, sys, struct
EMU = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu')  # emulator code
DATA = os.environ.get('NMS_EMU_DATA') or sys.exit('set NMS_EMU_DATA to the folder with the extracted .mbin files (see README.md)')
os.chdir(DATA); sys.path.insert(0, EMU)
import emu_sys as E
import globals as _globals  # noqa: F401 - loads every .global.mbin at its runtime address
from unicorn import UC_HOOK_CODE, UcError
from unicorn import x86_const as X

def rd(a, fmt): return struct.unpack(fmt, bytes(E.mu.mem_read(a, struct.calcsize(fmt))))
stars = []
def on_callback(mu, addr, size, _):
    ctx, star, flag = (mu.reg_read(r) for r in (X.UC_X86_REG_RCX, X.UC_X86_REG_RDX, X.UC_X86_REG_R8))
    counter = rd(ctx, '<Q')[0]
    index = rd(counter, '<H')[0]
    stars.append((index + 1, rd(star, '<4f'), flag & 0xFF))
    E.mu.mem_write(counter, struct.pack('<H', index + 1))
    rsp = mu.reg_read(X.UC_X86_REG_RSP)
    mu.reg_write(X.UC_X86_REG_RIP, rd(rsp, '<Q')[0]); mu.reg_write(X.UC_X86_REG_RSP, rsp + 8)
E.mu.hook_add(UC_HOOK_CODE, on_callback, begin=0x141369C30, end=0x141369C30)

probes = []
def on_threshold(mu, addr, size, _):
    if len(probes) < 12:
        rsp = mu.reg_read(X.UC_X86_REG_RSP)
        x1 = struct.unpack('<f', mu.reg_read(X.UC_X86_REG_XMM1).to_bytes(16, 'little')[:4])[0]
        probes.append((round(rd(rsp + 0x44, '<f')[0], 5), round(x1, 5)))
E.mu.hook_add(UC_HOOK_CODE, on_threshold, begin=0x141369130, end=0x141369130)
gate = []
def on_gate(mu, addr, size, _):
    if len(gate) < 8:
        rbp = mu.reg_read(X.UC_X86_REG_RBP)
        x0 = struct.unpack('<f', mu.reg_read(X.UC_X86_REG_XMM0).to_bytes(16, 'little')[:4])[0]
        gate.append((round(x0, 5), rd(rbp + 0x570, '<12f')))
E.mu.hook_add(UC_HOOK_CODE, on_gate, begin=0x14136899E, end=0x14136899E)
def xf(mu, reg): return round(struct.unpack('<f', mu.reg_read(reg).to_bytes(16, 'little')[:4])[0], 4)
density = []
def on_density_in(mu, addr, size, _):
    if len(density) < 6: density.append(['in', xf(mu, X.UC_X86_REG_XMM0), xf(mu, X.UC_X86_REG_XMM1), xf(mu, X.UC_X86_REG_XMM2), xf(mu, X.UC_X86_REG_XMM3)])
def on_density_out(mu, addr, size, _):
    if len(density) <= 6 and density and len(density[-1]) == 5: density[-1].append(('out', xf(mu, X.UC_X86_REG_XMM0)))
E.mu.hook_add(UC_HOOK_CODE, on_density_in, begin=0x1413687D3, end=0x1413687D3)
E.mu.hook_add(UC_HOOK_CODE, on_density_out, begin=0x1413687D8, end=0x1413687D8)

coverage = {}
WATCH = [0x14136806B, 0x141368262, 0x141368348, 0x14136847A, 0x141368650, 0x141368680, 0x1413686A0, 0x1413687D3, 0x141368BAF, 0x141369092, 0x141369130, 0x141369589, 0x14136959B, 0x1413695AE, 0x141369684, 0x141369A08, 0x141369A62]
def on_cov(mu, addr, size, _): coverage[addr] = coverage.get(addr, 0) + 1
for a in WATCH: E.mu.hook_add(UC_HOOK_CODE, on_cov, begin=a, end=a)
galaxy, x, y, z = int(sys.argv[1]), int(sys.argv[2], 16), int(sys.argv[3], 16), int(sys.argv[4], 16)
address = (galaxy & 0xFF) << 32 | (y & 0xFF) << 24 | (z & 0xFFF) << 12 | (x & 0xFFF)
# Startup initializers that fill the noise tables at 0x145924420..0x1459246D0 (BSS in the file).
for init in (0x140031340, 0x14003A060, 0x14003A070, 0x14003A110, 0x1401997A0, 0x140199890, 0x1401998A0, 0x1401998B0, 0x1401998C0, 0x140199930, 0x140199990, 0x1401999D0):
    E.call(init, ())
print('gradient[0..2]', rd(0x1459245E0, '<12f'))
slot = E.alloc(0x280); E.call(0x1404C5620, (slot,))
offset = E.alloc(0x10)
try:
    E.call(0x141367810, (address, slot, offset))
except UcError as e:
    print('emu error', e, hex(E.mu.reg_read(X.UC_X86_REG_RIP)))
print('address', hex(address), 'slot count', rd(slot, '<I')[0], 'slot+0xC', rd(slot + 0xC, '<I')[0], 'emitted', len(stars))
print('first', stars[:3]); print('last', stars[-3:])
print('density vs threshold', probes)
print('gate', gate[:3])
print('gate x0', [g[0] for g in gate])
print('density io', density)
print('global offset 0x146ED3DC0', rd(0x146ED3DC0, '<4f'))
print('coverage', {hex(k): v for k, v in sorted(coverage.items())})
print('flags', sorted({s[2] for s in stars}))
print('imports used', E.used, 'zero pages', len(E.zeromapped))

ordinary = [s for s in stars if s[2] == 0]; purple = [s for s in stars if s[2] == 1]
print(f'ordinary {len(ordinary)} indices {ordinary[0][0]}..{ordinary[-1][0]} contiguous={[s[0] for s in ordinary] == list(range(1, len(ordinary) + 1))}')
print(f'purple {len(purple)} indices {purple[0][0]}..{purple[-1][0]}')
if len(sys.argv) > 5:
    import json, subprocess
    truth = json.loads(sys.argv[5])
    emitted = {s[0] for s in stars}
    print('truth', len(truth), 'missing from emulation:', sorted(set(truth) - emitted))
idx = sorted(s[0] for s in stars)
low = [i for i in idx if i < 1000]; high = [i for i in idx if i >= 1000]
print(f'below 1000: {len(low)} ({low[0]}..{low[-1]}, contiguous={low == list(range(1, len(low) + 1))}); 1000+: {len(high)} ({high[0] if high else None}..{high[-1] if high else None})')
