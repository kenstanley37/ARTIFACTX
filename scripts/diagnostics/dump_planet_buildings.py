# READ-ONLY diagnostic (docs/research/PLANET_BUILDING_PLACEMENT.md). Run while NMS is open:
#   python scripts/diagnostics/dump_planet_buildings.py > planet_buildings.json
# Finds every loaded planet's runtime building data (same signature as read_planet_building_data.py) and dumps it as
# JSON: header fields, all slots (index, probability, has-building) and every building type record as hex
# (0xE0 bytes each) plus decoded seed (+0x68) and classification (+0xD3). Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, struct, subprocess, sys, json
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
SPACING = struct.pack('<f', 250.0)
blocks = []
addr, mbi = 0, MBI()
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    if mbi.State == 0x1000 and mbi.Protect == 0x04:
        for off in range(0, size, 1 << 24):
            chunk = read(start + off, min(1 << 24, size - off))
            i = chunk.find(SPACING)
            while i >= 0:
                if 4 <= i and i + 16 <= len(chunk):
                    radius, spacing, div = struct.unpack_from('<3f', chunk, i - 4)
                    point, sector = struct.unpack_from('<II', chunk, i + 8)
                    if 1000 < radius < 1e7 and div == int(div) and 2 <= div <= 4000 and int(div) % 2 == 0 \
                            and 1 <= point <= 10000 and 1 <= sector <= 10000:
                        blocks.append(start + off + i - 0x3C)
                i = chunk.find(SPACING, i + 1)
    addr = start + size
    if addr >= 0x7FFFFFFFFFFF: break
out = []
for block in blocks:
    head = read(block, 0x50)
    if len(head) < 0x50: continue
    bptr, bn = struct.unpack_from('<QI', head, 0); sptr, sn = struct.unpack_from('<QI', head, 0x10)
    optr, on = struct.unpack_from('<QI', head, 0x20)
    if bn > 4096 or sn > 4096: continue
    ua, radius, spacing, div, point, sector = struct.unpack_from('<QfffII', head, 0x30)
    flags = list(head[0x4C:0x4F])
    slots = read(sptr, 12 * sn)
    types = read(bptr, 0xE0 * bn)
    overrides = read(optr, 0x30 * on) if on < 256 else b''
    out.append(dict(
        ua=f'{ua:016X}', radius=radius, spacing=spacing, divisions=div, point_seed=point, sector_seed=sector,
        flags=flags,
        slots=[list(struct.unpack_from('<if?', slots, 12 * i)) for i in range(sn)],
        types=[dict(seed=f'{struct.unpack_from("<Q", types, 0xE0 * i + 0x68)[0]:016X}',
                    classification=types[0xE0 * i + 0xD3], raw=types[0xE0 * i:0xE0 * (i + 1)].hex()) for i in range(bn)],
        overrides=overrides.hex()))
json.dump(out, sys.stdout, indent=1)
