# Runs the game's building-site Voronoi query (wrapper 0x142D76D40 -> worker 0x1419883E0) in the emulator.
# usage: python voronoiemu.py <sector seed> <radius> <divisions> <x> <y> <z>   (planet-relative query position)
# Prints the site key, the float output and the three returned points. docs/research/PLANET_BUILDING_PLACEMENT.md
import os, sys, struct
EMU = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu')
DATA = os.environ.get('NMS_EMU_DATA') or sys.exit('set NMS_EMU_DATA (see README.md)')
os.chdir(DATA); sys.path.insert(0, EMU)
import emu_sys as E
import globals as _globals  # noqa: F401
from unicorn import x86_const as X
for init in (0x140031340, 0x14003A060, 0x14003A070, 0x14003A110, 0x1401997A0, 0x140199890, 0x1401998A0, 0x1401998B0, 0x1401998C0, 0x140199930, 0x140199990, 0x1401999D0):
    E.call(init, ())
def f32(v): return struct.unpack('<I', struct.pack('<f', v))[0]
def query(seed, radius, divisions, x, y, z):
    pos = E.alloc(0x10); E.mu.mem_write(pos, struct.pack('<4f', x, y, z, 1.0))
    key, dist, p0, p1, p2 = (E.alloc(0x10) for _ in range(5))
    E.mu.reg_write(X.UC_X86_REG_XMM2, f32(radius))
    E.mu.reg_write(X.UC_X86_REG_XMM3, f32(divisions * 0.5))
    E.call(0x142D76D40, (pos, seed), (key, dist, p0, p1, p2))
    rd = lambda a, f: struct.unpack(f, bytes(E.mu.mem_read(a, struct.calcsize(f))))
    return rd(key, '<Q')[0], rd(dist, '<f')[0], rd(p0, '<4f'), rd(p1, '<4f'), rd(p2, '<4f')
if __name__ == '__main__':
    seed, radius, divisions = int(sys.argv[1]), float(sys.argv[2]), float(sys.argv[3])
    x, y, z = (float(v) for v in sys.argv[4:7])
    k, d, a, b, c = query(seed, radius, divisions, x, y, z)
    print(f'key {k:016X}  float {d!r}')
    for name, p in (('point', a), ('second', b), ('third', c)): print(f'  {name}: {p}')
