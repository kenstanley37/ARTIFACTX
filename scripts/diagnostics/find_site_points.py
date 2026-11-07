# READ-ONLY diagnostic (docs/research/PLANET_BUILDING_PLACEMENT.md). Run while NMS is open:
#   python scripts/diagnostics/find_site_points.py <radius> [divisions]
# Finds every 16-byte (x, y, z, 1.0) float vector in the game's writable memory whose length is the planet's
# building radius (planet-relative building site points, e.g. 130048 for Peak XV), dedupes them and prints each
# with its cube-face grid position for the given divisions. Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, struct, subprocess, sys, math
import numpy as np
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
class MBI(ctypes.Structure):
    _fields_ = [('BaseAddress', ctypes.c_void_p), ('AllocationBase', ctypes.c_void_p), ('AllocationProtect', W.DWORD),
                ('PartitionId', W.WORD), ('RegionSize', ctypes.c_size_t), ('State', W.DWORD), ('Protect', W.DWORD), ('Type', W.DWORD)]
k32.VirtualQueryEx.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.POINTER(MBI), ctypes.c_size_t]
pid = int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip())
h = k32.OpenProcess(0x410, False, pid)
radius = float(sys.argv[1]); divisions = int(sys.argv[2]) if len(sys.argv) > 2 else 0
def read(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    ok = k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got))
    return buf.raw[:got.value] if ok or got.value else b''
points = {}
addr, mbi = 0, MBI()
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    if mbi.State == 0x1000 and mbi.Protect == 0x04:
        for off in range(0, size, 1 << 24):
            chunk = read(start + off, min(1 << 24, size - off))
            n = len(chunk) // 16
            if n == 0: continue
            v = np.frombuffer(chunk[:n * 16], dtype='<f4').reshape(n, 4)
            with np.errstate(all='ignore'):
                r = np.sqrt((v[:, :3].astype(np.float64) ** 2).sum(axis=1))
            for i in np.nonzero((v[:, 3] == 1.0) & (np.abs(r - radius) < 1.0))[0]:
                key = tuple(round(float(c), 1) for c in v[i, :3])
                points.setdefault(key, start + off + 16 * int(i))
    addr = start + size
    if addr >= 0x7FFFFFFFFFFF: break
def grid(p):
    r = math.sqrt(sum(c * c for c in p)); d = [c / r for c in p]
    ax = max(range(3), key=lambda i: abs(d[i])); m = abs(d[ax])
    face = ('+' if d[ax] > 0 else '-') + 'XYZ'[ax]
    return face, [(d[i] / m + 1) / 2 * divisions for i in range(3) if i != ax]
for p, a in sorted(points.items(), key=lambda kv: kv[1]):
    line = f'{a:X}: ({p[0]:.1f}, {p[1]:.1f}, {p[2]:.1f})'
    if divisions:
        face, g = grid(p)
        line += f'  {face} grid ({g[0]:.3f}, {g[1]:.3f})  off ({g[0] - round(g[0]):+.3f}, {g[1] - round(g[1]):+.3f})'
    print(line)
print(len(points), 'distinct points')
