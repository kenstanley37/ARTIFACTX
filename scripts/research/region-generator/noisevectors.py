# Game noise routines called directly in the emulator, as JSON test vectors for the C# port:
# 2D simplex value (0x142D74860), 3D simplex value (0x142D74E60), 3D simplex with derivatives (0x142D75250).
# usage: python noisevectors.py <out.json>
import os, sys, json, struct, random
OUT = os.path.abspath(sys.argv[1])
EMU = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu')
DATA = os.environ.get('NMS_EMU_DATA') or sys.exit('set NMS_EMU_DATA (see README.md)')
os.chdir(DATA); sys.path.insert(0, EMU)
import emu_sys as E
from unicorn import x86_const as X
for init in (0x1401997A0, 0x140199890, 0x1401998A0, 0x1401998B0, 0x1401998C0, 0x140199930, 0x140199990, 0x1401999D0):
    E.call(init, ())
def f2x(v): return int.from_bytes(struct.pack('<f', v), 'little')
def x2f(reg): return struct.unpack('<f', E.mu.reg_read(reg).to_bytes(16, 'little')[:4])[0]
def f32(v): return struct.unpack('<f', struct.pack('<f', v))[0]
rng = random.Random(4242)
points = [tuple(f32(rng.uniform(-60, 60)) for _ in range(3)) for _ in range(1500)]
points += [tuple(f32(rng.uniform(-2, 2)) for _ in range(3)) for _ in range(500)] + [(0.0, 0.0, 0.0), (1.0, 1.0, 1.0), (-1.0, 0.5, 2.0)]
d = E.alloc(16)
rows = []
for x, y, z in points:
    for reg, v in zip((X.UC_X86_REG_XMM0, X.UC_X86_REG_XMM1, X.UC_X86_REG_XMM2), (x, y, z)): E.mu.reg_write(reg, f2x(v))
    E.call(0x142D74860, ()); n2 = x2f(X.UC_X86_REG_XMM0)
    for reg, v in zip((X.UC_X86_REG_XMM0, X.UC_X86_REG_XMM1, X.UC_X86_REG_XMM2), (x, y, z)): E.mu.reg_write(reg, f2x(v))
    E.call(0x142D74E60, ()); n3 = x2f(X.UC_X86_REG_XMM0)
    for reg, v in zip((X.UC_X86_REG_XMM0, X.UC_X86_REG_XMM1, X.UC_X86_REG_XMM2), (x, y, z)): E.mu.reg_write(reg, f2x(v))
    E.call(0x142D75250, (0, 0, 0, d), (d + 4, d + 8)); nd = x2f(X.UC_X86_REG_XMM0)
    rows.append([x, y, z, n2, n3, nd, *struct.unpack('<3f', bytes(E.mu.mem_read(d, 12)))])
json.dump(rows, open(OUT, 'w'))
print('noise vectors', len(rows))
