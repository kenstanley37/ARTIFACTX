# Runs the region generator with the game's real per-star callback (no recorder hook) and dumps
# the slot's stored star list (system index + position), comparable with
# scripts/diagnostics/read_region_star_positions.py output.
# usage: python regionpositions.py <galaxy> <x hex> <y hex> <z hex> [live.json to compare]
import os, sys, json, struct
EMU = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu')
DATA = os.environ.get('NMS_EMU_DATA') or sys.exit('set NMS_EMU_DATA (see README.md)')
os.chdir(DATA); sys.path.insert(0, EMU)
import emu_sys as E
import globals as _globals  # noqa: F401
from unicorn import UcError
from unicorn import x86_const as X
def rd(a, fmt): return struct.unpack(fmt, bytes(E.mu.mem_read(a, struct.calcsize(fmt))))
for init in (0x140031340, 0x14003A060, 0x14003A070, 0x14003A110, 0x1401997A0, 0x140199890, 0x1401998A0, 0x1401998B0, 0x1401998C0, 0x140199930, 0x140199990, 0x1401999D0):
    E.call(init, ())
galaxy, x, y, z = int(sys.argv[1]), int(sys.argv[2], 16), int(sys.argv[3], 16), int(sys.argv[4], 16)
slot = E.alloc(0x280); E.call(0x1404C5620, (slot,)); offset = E.alloc(0x10)
try:
    E.call(0x141367810, ((galaxy << 32) | (y << 24) | (z << 12) | x, slot, offset))
except UcError as e:
    sys.exit(f'emu error {e} at {E.mu.reg_read(X.UC_X86_REG_RIP):X}')
n, total = rd(slot, '<I')[0], rd(slot + 0x214, '<I')[0]
posp, addrp = rd(slot + 0x218, '<Q')[0], rd(slot + 0x258, '<Q')[0]
stars = [dict(entry=k + 1, system=(rd(addrp + 8 * k, '<Q')[0] >> 40) & 0xFFF, pos=rd(posp + 12 * k, '<3f')) for k in range(total)]
print('emulated n', n, 'total', total, 'first', stars[:2])
if len(sys.argv) > 5:
    live = json.load(open(sys.argv[5]))
    same = sum(1 for a, b in zip(stars, live['stars']) if a['system'] == b['system'] and tuple(a['pos']) == tuple(b['pos']))
    print(f'live total {live["total"]}; identical entries (index and bit-exact position): {same}/{len(live["stars"])}')
    for a, b in zip(stars, live['stars']):
        if a['system'] != b['system'] or tuple(a['pos']) != tuple(b['pos']):
            print('first difference', a, b); break
