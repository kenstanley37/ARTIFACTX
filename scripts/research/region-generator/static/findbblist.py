# READ-ONLY: locates the in-memory list of GcPersistentBBObjectData (0x60 each: At, Position, Up, ObjectID "^...",
# GalacticAddress, RegionSeed, Timestamp, UserData) - runs of entries whose ObjectID starts with '^' - and the
# {ptr, count, capacity} header pointing at it. usage: python findbblist.py
import sys, os, struct, ctypes, ctypes.wintypes as W
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mem import *
class MBI(ctypes.Structure):
    _fields_ = [('BaseAddress', ctypes.c_void_p), ('AllocationBase', ctypes.c_void_p), ('AllocationProtect', W.DWORD),
                ('PartitionId', W.WORD), ('RegionSize', ctypes.c_size_t), ('State', W.DWORD), ('Protect', W.DWORD), ('Type', W.DWORD)]
k32.VirtualQueryEx.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.POINTER(MBI), ctypes.c_size_t]
seed = struct.pack('<Q', 0x7B89DC37B31051CB)
def entry_ok(b, o):
    return len(b) >= o + 0x60 and 65 <= b[o + 0x30] <= 90 and 65 <= b[o + 0x31] <= 95 and b[o + 0x48:o + 0x50] != b'\0' * 8
runs = []
addr, mbi = 0, MBI()
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    if mbi.State == 0x1000 and mbi.Protect == 0x04 and size < (1 << 32):
        for off in range(0, size, 1 << 24):
            c = read(start + off, min(1 << 24, size - off))
            i = c.find(seed)
            while i >= 0:
                e = i - 0x48
                if e >= 0 and entry_ok(c, e):
                    lo = e
                    while lo >= 0x60 and entry_ok(c, lo - 0x60): lo -= 0x60
                    hi = e
                    while entry_ok(c, hi + 0x60): hi += 0x60
                    runs.append((start + off + lo, (hi - lo) // 0x60 + 1, start + off + e))
                i = c.find(seed, i + 1)
    addr = start + size
for base_, n, me in runs:
    names = []
    for k in range(n):
        b = read(base_ + 0x60 * k + 0x30, 16); names.append(b.split(b'\0')[0].decode('latin1'))
    print(f'list at {base_:#x}: {n} entries (beacon entry #{(me - base_) // 0x60}) {names}')
# headers pointing at the run starts
targets = {r[0]: r for r in runs}
addr, mbi = 0, MBI()
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    if mbi.State == 0x1000 and mbi.Protect == 0x04 and size < (1 << 32):
        for off in range(0, size, 1 << 24):
            c = read(start + off, min(1 << 24, size - off)); n8 = len(c) // 8
            if not n8: continue
            q = np.frombuffer(c[:n8 * 8], dtype='<u8')
            for t in targets:
                for i in np.nonzero(q == t)[0]:
                    cnt, cap = struct.unpack_from('<ii', c, 8 * int(i) + 8)
                    print(f'header at {start + off + 8 * int(i):#x} -> {t:#x}: count {cnt}, capacity {cap}')
    addr = start + size
