# READ-ONLY check of siteport.py against the running game (docs/research/PLANET_BUILDING_PLACEMENT.md).
# usage: python sitecheck_live.py <sector seed> <radius> <divisions>   (e.g. 2243 130048 546 for Peak XV)
# Finds every live site record (key at +0x00, point A at +0x10 at the building radius), derives the face and cell
# from point A, recomputes the key and feature point with the port, and counts exact matches.
# Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, struct, subprocess, sys, math
import numpy as np
from siteport import feature, key
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
class MBI(ctypes.Structure):
    _fields_ = [('BaseAddress', ctypes.c_void_p), ('AllocationBase', ctypes.c_void_p), ('AllocationProtect', W.DWORD),
                ('PartitionId', W.WORD), ('RegionSize', ctypes.c_size_t), ('State', W.DWORD), ('Protect', W.DWORD), ('Type', W.DWORD)]
k32.VirtualQueryEx.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.POINTER(MBI), ctypes.c_size_t]
pid = int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip())
h = k32.OpenProcess(0x410, False, pid)
seed, radius, divisions = int(sys.argv[1]), float(sys.argv[2]), int(sys.argv[3])
half = divisions // 2
def read(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    ok = k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got))
    return buf.raw[:got.value] if ok or got.value else b''
# face index (the worker's r9) by outward normal: table 0x1450AF190
FACE = {(2, -1): 0, (2, 1): 1, (0, 1): 2, (0, -1): 3, (1, -1): 4, (1, 1): 5}
records = {}
addr, mbi = 0, MBI()
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    if mbi.State == 0x1000 and mbi.Protect == 0x04:
        for off in range(0, size, 1 << 24):
            chunk = read(start + off, min(1 << 24, size - off))
            n = len(chunk) // 16
            if n < 2: continue
            v = np.frombuffer(chunk[:n * 16], dtype='<f4').reshape(n, 4)
            with np.errstate(all='ignore'):
                r = np.sqrt((v[:, :3].astype(np.float64) ** 2).sum(axis=1))
            for i in np.nonzero((v[:, 3] == 1.0) & (np.abs(r - radius) < 1.0))[0]:
                if i == 0: continue
                k = struct.unpack_from('<Q', chunk, 16 * (int(i) - 1))[0]
                p = tuple(float(c) for c in v[i, :3])
                records[k] = p
    addr = start + size
    if addr >= 0x7FFFFFFFFFFF: break
exact = tried = 0
bad = []
for k, p in records.items():
    ax = max(range(3), key=lambda i: abs(p[i]))
    sign = 1 if p[ax] > 0 else -1
    face = FACE[(ax, sign)]
    q = [c / abs(p[ax]) * half for c in p]
    cell = [round(c) for c in q]
    cell[ax] = sign * half
    if any(abs(q[i] - cell[i]) > 0.05 for i in range(3)): continue   # not a site's point A (a building part)
    if k < (1 << 48): continue   # a heap pointer, not a site key: another structure holding a point
    tried += 1
    if key(feature(face, *cell), seed) == k: exact += 1
    elif len(bad) < 5: bad.append((hex(k), p, face, cell))
print(f'{len(records)} candidate records; {tried} site points; {exact} keys exact')
for b in bad: print('  mismatch', b)
