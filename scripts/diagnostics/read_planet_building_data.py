# READ-ONLY diagnostic (docs/research/PLANET_BUILDING_PLACEMENT.md). Run while NMS is open:
#   python scripts/diagnostics/read_planet_building_data.py
# Finds every planet's runtime GcPlanetBuildingData block in the game's memory by its signature
# (PlanetRadius, Spacing = 250.0, VoronoiPointDivisions = an even whole number, then the point and sector seeds
# in 1..10000) and prints it with the planet's universe address and the building/slot list sizes.
# Query/read access only (0x410), no writes, no game code executed.
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
if not h: sys.exit('cannot open NMS for reading')
def read(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    ok = k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got))
    return buf.raw[:got.value] if ok or got.value else b''
SPACING = struct.pack('<f', 250.0)
found = []
addr, mbi = 0, MBI()
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    # committed, readable, writable private/mapped data (PAGE_READWRITE), skip huge regions in chunks
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
                        block = start + off + i - 0x3C  # GcPlanetBuildingData base (Spacing at +0x3C)
                        found.append((block, radius, div, point, sector))
                i = chunk.find(SPACING, i + 1)
    addr = start + size
    if addr >= 0x7FFFFFFFFFFF: break
for block, radius, div, point, sector in found:
    head = read(block, 0x50)
    ua = struct.unpack_from('<Q', head, 0x30)[0] if len(head) >= 0x38 else 0
    lists = [struct.unpack_from('<QI', head, o) if len(head) >= o + 12 else (0, 0) for o in (0x00, 0x10, 0x20)]
    print(f'block {block:X}: UA {ua:016X} radius {radius:.1f} divisions {div:.0f} point seed {point} sector seed {sector}; '
          f'buildings ptr {lists[0][0]:X} n {lists[0][1]}, slots ptr {lists[1][0]:X} n {lists[1][1]}, overrides n {lists[2][1]}')
print(len(found), 'candidate blocks')
