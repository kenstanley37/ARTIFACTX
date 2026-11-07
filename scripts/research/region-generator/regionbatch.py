# Emulates NMS.exe's region star generator for every region in a save's discovery records and
# checks each visited system index is one the generator emits. Read-only on the save.
# usage: python regionbatch.py <save.hg.json>
import os, sys, json, struct, time
SAVE = os.path.abspath(sys.argv[1])
EMU = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu')  # emulator code
DATA = os.environ.get('NMS_EMU_DATA') or sys.exit('set NMS_EMU_DATA to the folder with the extracted .mbin files (see README.md)')
os.chdir(DATA); sys.path.insert(0, EMU)
import emu_sys as E
import globals as _globals  # noqa: F401
from unicorn import UC_HOOK_CODE
from unicorn import x86_const as X

def rd(a, fmt): return struct.unpack(fmt, bytes(E.mu.mem_read(a, struct.calcsize(fmt))))
stars = []
def on_callback(mu, addr, size, _):
    ctx = mu.reg_read(X.UC_X86_REG_RCX)
    counter = rd(ctx, '<Q')[0]; index = rd(counter, '<H')[0]
    stars.append(index + 1)
    E.mu.mem_write(counter, struct.pack('<H', index + 1))
    rsp = mu.reg_read(X.UC_X86_REG_RSP)
    mu.reg_write(X.UC_X86_REG_RIP, rd(rsp, '<Q')[0]); mu.reg_write(X.UC_X86_REG_RSP, rsp + 8)
E.mu.hook_add(UC_HOOK_CODE, on_callback, begin=0x141369C30, end=0x141369C30)
for init in (0x140031340, 0x14003A060, 0x14003A070, 0x14003A110, 0x1401997A0, 0x140199890, 0x1401998A0, 0x1401998B0, 0x1401998C0, 0x140199930, 0x140199990, 0x1401999D0):
    E.call(init, ())

def region_stars(galaxy, x, y, z):
    stars.clear()
    heap = E.heap[0]
    slot = E.alloc(0x280); E.call(0x1404C5620, (slot,)); offset = E.alloc(0x10)
    E.call(0x141367810, ((galaxy << 32) | (y << 24) | (z << 12) | x, slot, offset))
    E.heap[0] = heap  # reuse scratch memory between regions
    return list(stars)

root = json.loads(open(SAVE, encoding='utf-8-sig').read().rstrip('\0'))
regions = {}
for r in root['fDu']['ETO']['OsQ']['?fB']:
    v = r['8P3']['5L6']; a = int(v, 16) if isinstance(v, str) else int(v)
    key = ((a >> 32) & 0xFF, a & 0xFFF, (a >> 24) & 0xFF, (a >> 12) & 0xFFF)
    regions.setdefault(key, set()).add((a >> 40) & 0xFFF)
ok = bad = 0
t0 = time.time()
for key, seen in sorted(regions.items(), key=lambda kv: -len(kv[1])):
    emitted = region_stars(*key)
    ordinary = sum(1 for i in emitted if i < 1000)
    missing = sorted(seen - set(emitted))
    ok += not missing; bad += bool(missing)
    print(f'galaxy {key[0]} region {key[1]:03X}/{key[2]:02X}/{key[3]:03X}: visited {len(seen):3} max {max(seen):4}  N={ordinary:3} purple={len(emitted) - ordinary:2}  {"OK" if not missing else "MISSING " + str(missing)}')
print(f'regions {len(regions)}: {ok} consistent, {bad} with indices the generator never emits; {time.time() - t0:.0f}s')
