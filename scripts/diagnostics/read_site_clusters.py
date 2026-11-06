# READ-ONLY diagnostic (docs/research/PLANETARY_MULTITOOL_LOCATIONS.md). Run while NMS is open:
#   python scripts/diagnostics/read_site_clusters.py <site key hex> [...]
# Finds the runtime site records (stride 0x530, key at +0) and prints their points (+0x10: point A, then the cluster
# members BEFORE the re-centring shift) and directions (+0x2A0: main forward, then each member's direction from the
# main building). Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, struct, subprocess, sys
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
class MBI(ctypes.Structure):
    _fields_ = [('BaseAddress', ctypes.c_void_p), ('AllocationBase', ctypes.c_void_p), ('AllocationProtect', W.DWORD),
                ('PartitionId', W.WORD), ('RegionSize', ctypes.c_size_t), ('State', W.DWORD), ('Protect', W.DWORD), ('Type', W.DWORD)]
k32.VirtualQueryEx.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.POINTER(MBI), ctypes.c_size_t]
pid = int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip())
h = k32.OpenProcess(0x410, False, pid)
def read(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    ok = k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got))
    return buf.raw[:got.value] if ok or got.value else b''
keys = {struct.pack('<Q', int(k, 16)): k for k in sys.argv[1:]}
addr, mbi = 0, MBI()
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    if mbi.State == 0x1000 and mbi.Protect == 0x04:
        for off in range(0, size, 1 << 24):
            base = start + off
            chunk = read(base, min(1 << 24, size - off))
            for kb, name in keys.items():
                i = chunk.find(kb)
                while i >= 0:
                    rec = read(base + i, 0x530)
                    pts = [struct.unpack_from('<4f', rec, 0x10 + 16 * j) for j in range(41)]
                    if abs(pts[0][0]) > 1000 and pts[0][3] == 1.0:
                        print(f'{name} at {base + i:#x} word {rec[8:16].hex()}')
                        for j, p in enumerate(pts):
                            if any(c != 0 for c in p[:3]): print(f'   pt {j:2} {p[0]:12.3f} {p[1]:12.3f} {p[2]:12.3f} {p[3]:6.3f}')
                        for j in range(0, 0x530 - 0x2A0, 16):
                            v = struct.unpack_from('<4f', rec, 0x2A0 + j)
                            if any(c != 0 for c in v[:3]): print(f'   +{0x2A0 + j:#05x} {v[0]:10.4f} {v[1]:10.4f} {v[2]:10.4f} {v[3]:10.4f}')
                    i = chunk.find(kb, i + 8)
    addr = start + size
