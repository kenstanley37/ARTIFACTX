# READ-ONLY diagnostic (docs/research/PLANET_BUILDING_PLACEMENT.md). Run while NMS is open:
#   python scripts/diagnostics/read_planet_building_slots.py <block hex> [slots to show]
# Dumps a planet's runtime GcPlanetBuildingData (block address from read_planet_building_data.py): slot entries
# (12 bytes: building index, probability, has-building) and building records (stride from the list's spacing),
# showing each record's seed and classification bytes. Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, struct, subprocess, sys
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
pid = int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip())
h = k32.OpenProcess(0x410, False, pid)
def read(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got))
    return buf.raw[:got.value]
block = int(sys.argv[1], 16)
show = int(sys.argv[2]) if len(sys.argv) > 2 else 40
head = read(block, 0x50)
(bptr, bn), (sptr, sn) = struct.unpack_from('<QI', head, 0), struct.unpack_from('<QI', head, 0x10)
print(f'buildings {bn} at {bptr:X}; slots {sn} at {sptr:X}')
slots = read(sptr, 12 * sn)
filled = [(i, *struct.unpack_from('<if?', slots, 12 * i)) for i in range(sn)]
used = [s for s in filled if s[3]]
print(f'{len(used)} slots with a building; index range {min(s[1] for s in filled)}..{max(s[1] for s in filled)}')
for i, idx, prob, has in filled[:show]:
    print(f'  slot {i}: building {idx} probability {prob:.3f} has {has}')
raw = read(bptr, 0x400)
print('first building record bytes:', raw[:0x100].hex())
