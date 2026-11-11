# READ-ONLY (access 0x410): finds the live Far terrain regions of the planet whose radius is R (Far vtable given as an
# NMS.exe VA for the installed build: 0x144BBEE20 on 184271, 0x144BC0690 on 185034, from static/remap.py on the vtable's
# entries), picks the pick-th nearest to a planet-relative point, and writes {'region', 'mem', 'grid'} for emu_far.py:
# the region object, the 16 KB behind each heap pointer in it, and its live height grid. Game data: keep the output local.
# usage: python read_far_region_live.py <vtable va> <R> <x> <y> <z> <out.pkl> [pick]
import ctypes, ctypes.wintypes as W, struct, subprocess
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
_o = subprocess.check_output(['powershell', '-NoProfile', '-Command', '$p = Get-Process NMS; "$($p.Id) $($p.MainModule.BaseAddress.ToInt64())"']).split()
pid, base = int(_o[0]), int(_o[1])
h = k32.OpenProcess(0x410, False, pid)
def read(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got)); return buf.raw[:got.value]
VA = lambda va: va - 0x140000000 + base
u32 = lambda a: struct.unpack('<I', read(a, 4))[0]
i32 = lambda a: struct.unpack('<i', read(a, 4))[0]
u64 = lambda a: struct.unpack('<Q', read(a, 8))[0]
f32 = lambda a, n=1: struct.unpack(f'<{n}f', read(a, 4 * n))
def cstr(a, n=128):
    b = read(a, n); return b.split(b'\0')[0].decode('latin1')
def hexdump(a, n):
    b = read(a, n)
    for i in range(0, len(b), 16):
        row = b[i:i + 16]
        print(f'+{i:04x} {row.hex(" ")}  {"".join(chr(c) if 32 <= c < 127 else "." for c in row)}  {struct.unpack_from("<4f", row) if len(row) == 16 else ""}')
import sys, os, struct, pickle, ctypes, ctypes.wintypes as W, numpy as np
class MBI(ctypes.Structure):
    _fields_ = [('BaseAddress', ctypes.c_void_p), ('AllocationBase', ctypes.c_void_p), ('AllocationProtect', W.DWORD),
                ('PartitionId', W.WORD), ('RegionSize', ctypes.c_size_t), ('State', W.DWORD), ('Protect', W.DWORD), ('Type', W.DWORD)]
k32.VirtualQueryEx.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.POINTER(MBI), ctypes.c_size_t]
vt = VA(int(sys.argv[1], 16)); R = float(sys.argv[2]); P = np.array([float(v) for v in sys.argv[3:6]]); out = sys.argv[6]; pick = int(sys.argv[7]) if len(sys.argv) > 7 else 0
cands = []
addr, mbi = 0, MBI()
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    if mbi.State == 0x1000 and mbi.Protect == 0x04 and size < (1 << 31):
        for off in range(0, size, 1 << 24):
            c = read(start + off, min(1 << 24, size - off)); q = np.frombuffer(c[:len(c) // 8 * 8], dtype='<u8')
            for i in np.nonzero(q == vt)[0]:
                obj = start + off + int(i) * 8
                raw = read(obj, 0x200)
                if len(raw) < 0x200: continue
                p10 = struct.unpack_from('<Q', raw, 0x10)[0]
                r30 = struct.unpack('<f', read(p10 + 0x30, 4))[0] if p10 > 0x10000 else 0
                if abs(r30 - R) > 1: continue
                t = np.frombuffer(raw[0x50:0x5C], dtype='<f4').astype(np.float64)
                cands.append((float(np.linalg.norm(t / np.linalg.norm(t) * np.linalg.norm(P) - P)), obj, raw))
    addr = start + size
cands.sort(key=lambda c: c[0])
print(len(cands), 'Far regions on this planet; nearest:')
for d, obj, raw in cands[:5]:
    print(f'  {obj:#x} {d:9.1f} m  b80 {raw[0x80:0x8C].hex()}')
d, region, raw = cands[pick]
full = read(region, 0x1A00)
mem = {region: full}
for k in range(0, 0x1A00, 8):            # pages behind every heap pointer in the region object
    p = struct.unpack_from('<Q', full, k)[0]
    if 0x10000000000 < p < 0x7FF000000000 and p not in mem:
        b = read(p, 0x4000)
        if b: mem[p] = b
stride = full[0x86]
grid_ptr = struct.unpack_from('<Q', full, 0x1D0)[0]
grid = np.frombuffer(read(grid_ptr, 4 * stride * stride), dtype='<f4').copy().reshape(stride, stride)
pickle.dump({'region': region, 'mem': mem, 'grid': grid}, open(out, 'wb'))
print('region', hex(region), 'pages', len(mem), 'stride', stride, 'grid', hex(grid_ptr))


