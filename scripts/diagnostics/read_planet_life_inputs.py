# READ-ONLY diagnostic (docs/research/PLANET_BUILDING_PLACEMENT.md). Run while NMS is open:
#   python scripts/diagnostics/read_planet_life_inputs.py
# Reads the thresholds the life/sentinel/scrap stage 0x1416956C0 uses (NMS.exe .data globals, relocated by the
# module base) and, for every loaded planet, the stage's inputs and outputs (GcPlanetData offsets): Biome/SubType/
# Class/StarType (+0x32B8..+0x32C8), PlanetIndex +0x353C, Life +0x3538, CreatureLife +0x352C, ResourceLevel
# +0x3540, sentinel level per difficulty (GroundCombatDataPerDifficulty[i] +0x14, i.e. +0x34CC + 0x18 i), HasScrap
# +0x3ACE, BuildingLevel +0x3528. Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, struct, subprocess
k32 = ctypes.WinDLL('kernel32', use_last_error=True); psapi = ctypes.WinDLL('psapi')
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
mods = (ctypes.c_void_p * 1)(); need = W.DWORD()
psapi.EnumProcessModulesEx(h, mods, ctypes.sizeof(mods), ctypes.byref(need), 3)
base = mods[0] - 0x140000000
f = lambda va: struct.unpack('<f', read(va + base, 4))[0]
i32 = lambda va: struct.unpack('<i', read(va + base, 4))[0]
print(f'module base {mods[0]:X}')
for name, va in [('A', 0x145260E18), ('B', 0x145260B20), ('A3', 0x145260E1C), ('B3', 0x145260B24), ('C', 0x145260F40),
                 ('D', 0x1452612FC), ('E', 0x145260C44), ('F', 0x144B288A4), ('Q', 0x144B28E9C)]:
    print(f'{name} {va:X} = {f(va)!r}')
print('flag 14526F4F8', read(0x14526F4F8 + base, 1)[0], 'difficulty 145215270', i32(0x145215270),
      'mode 14688D718?', 'galaxytypes', [i32(0x144A33ED0 + 4 * k) for k in range(20)])
SPACING = struct.pack('<f', 250.0)
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
                        block = start + off + i - 0x3C; d = block - 0x3338
                        ua = struct.unpack('<Q', read(block + 0x30, 8))[0]
                        biome, sub, cls, size_, star = struct.unpack('<5I', read(d + 0x32B8, 20))
                        g = lambda o: struct.unpack('<i', read(d + o, 4))[0]
                        sent = [g(0x34CC + 0x18 * k) for k in range(4)]
                        print(f'{ua:016X} biome {biome} sub {sub} class {cls} size {size_} star {star} index {g(0x353C)}'
                              f' life {g(0x3538)} creature {g(0x352C)} resource {g(0x3540)} sentinels {sent}'
                              f' scrap {read(d + 0x3ACE, 1)[0]} level {g(0x3528)}')
                i = chunk.find(SPACING, i + 1)
    addr = start + size
    if addr >= 0x7FFFFFFFFFFF: break
