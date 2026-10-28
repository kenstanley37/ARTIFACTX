# READ-ONLY live read for the terrain height port (docs/research/PLANET_BUILDING_PLACEMENT.md): for a loaded planet
# (building block address from scripts/diagnostics/read_planet_level_inputs.py) writes its TkVoxelGeneratorData
# (GcPlanetData + 0x2030, 0x1150 bytes; GcPlanetData = block - 0x3338), the planet position (runtime planet
# + 0xD74D0, planet = block - 0x3398) and its OverrideBuildings records to a JSON file in the scratch folder - game
# data, never committed. Query/read access only (0x410).
# usage: python read_terrain_live.py <block hex> <out.json>
import ctypes, ctypes.wintypes as W, struct, subprocess, sys, json
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
pid = int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip())
h = k32.OpenProcess(0x410, False, pid)
def read(a, n):
    b = ctypes.create_string_buffer(n); g = ctypes.c_size_t(); k32.ReadProcessMemory(h, ctypes.c_void_p(a), b, n, ctypes.byref(g)); return b.raw[:g.value]
block = int(sys.argv[1], 16)
planet, data = block - 0x3398, block - 0x3338
terrain = read(data + 0x2030, 0x1150)
pos = struct.unpack('<4f', read(planet + 0xD74D0, 16))
optr, on = struct.unpack_from('<QI', read(block + 0x20, 12))
ov = read(optr, 0x30 * on)
recs = [dict(pos=struct.unpack_from('<3f', ov, 0x30 * i), w=struct.unpack_from('<f', ov, 0x30 * i + 12)[0],
             key=f'{struct.unpack_from("<Q", ov, 0x30 * i + 0x10)[0]:016X}', type=struct.unpack_from('<I', ov, 0x30 * i + 0x20)[0])
        for i in range(on)]
radius = struct.unpack('<f', read(block + 0x38, 4))[0]
json.dump(dict(terrain=terrain.hex(), position=pos, radius=radius, overrides=recs), open(sys.argv[2], 'w'), indent=1)
print('position', pos, 'radius', radius, 'overrides', len(recs), 'sea level', struct.unpack_from('<f', terrain, 0x1148)[0],
      'no-sea base', struct.unpack_from('<f', terrain, 0x1140)[0])
