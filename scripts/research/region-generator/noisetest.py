# Calls NMS.exe's gradient noise 0x142D75250(x, y, z in xmm0..2; r9/[rsp+0x20]/[rsp+0x28] = derivative outputs) directly.
import os, sys, struct
EMU = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu')  # emulator code
DATA = os.environ.get('NMS_EMU_DATA') or sys.exit('set NMS_EMU_DATA to the folder with the extracted .mbin files (see README.md)')
os.chdir(DATA); sys.path.insert(0, EMU)
import emu_sys as E
from unicorn import x86_const as X
def f2x(v): return int.from_bytes(struct.pack('<f', v), 'little')
def x2f(reg): return struct.unpack('<f', E.mu.reg_read(reg).to_bytes(16, 'little')[:4])[0]
d = E.alloc(16)
for p in [(0.18, 1.38, 26.34), (0.5, 0.25, 0.75), (1.3, 7.7, 2.2), (10.1, 3.3, 4.4)]:
    for reg, v in zip((X.UC_X86_REG_XMM0, X.UC_X86_REG_XMM1, X.UC_X86_REG_XMM2), p): E.mu.reg_write(reg, f2x(v))
    E.call(0x142D75250, (0, 0, 0, d), (d + 4, d + 8))
    print(p, '->', round(x2f(X.UC_X86_REG_XMM0), 6), struct.unpack('<3f', bytes(E.mu.mem_read(d, 12))))
