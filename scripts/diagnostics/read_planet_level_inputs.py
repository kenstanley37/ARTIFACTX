# READ-ONLY diagnostic (docs/research/PLANET_BUILDING_PLACEMENT.md). Run while NMS is open:
#   python scripts/diagnostics/read_planet_level_inputs.py
# For every loaded planet's GcPlanetData (found through its building data block, GcPlanetData + 0x3338), prints the
# inputs of the BuildingLevel rule at 0x141695B22 (GenerationData Biome +0x32B8, BiomeSubType +0x32BC, Class +0x32C0)
# and the results BuildingLevel +0x3528 and InhabitingRace +0x3534. Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, struct, subprocess
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
BIOMES = 'Lush Toxic Scorched Radioactive Frozen Barren Dead Weird Red Green Blue Test Swamp Lava Waterworld GasGiant All'.split()
LEVELS = 'Dead Low Mid Full Weird HalfWeird Waterworld GasGiant'.split()
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
                        block = start + off + i - 0x3C
                        data = block - 0x3338          # GcPlanetData (libMBIN offsets)
                        ua = struct.unpack('<Q', read(block + 0x30, 8))[0]
                        biome, sub, cls = struct.unpack('<3I', read(data + 0x32B8, 12))
                        level, = struct.unpack('<I', read(data + 0x3528, 4))
                        race, = struct.unpack('<I', read(data + 0x3534, 4))
                        name = read(data + 0x3A4E, 0x80).split(b'\0')[0].decode('utf-8', 'replace')
                        print(f'{block:X} {ua:016X} {name:24} biome {BIOMES[biome] if biome < 17 else biome:11} sub {sub:2} class {cls}'
                              f' level {LEVELS[level] if level < 8 else level} race {race}')
                i = chunk.find(SPACING, i + 1)
    addr = start + size
    if addr >= 0x7FFFFFFFFFFF: break
