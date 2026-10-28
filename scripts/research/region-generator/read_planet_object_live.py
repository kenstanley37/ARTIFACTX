# READ-ONLY live read for emulating the override placement (docs/research/PLANET_BUILDING_PLACEMENT.md): writes a
# loaded planet's runtime object (planet = building block - 0x3398, 0xD7500 bytes) plus its building types, slots and
# override records to a binary bundle in the scratch folder - game data, never committed. Access 0x410 only.
# usage: python read_planet_object_live.py <block hex> <out.bin>
import ctypes, ctypes.wintypes as W, struct, subprocess, sys, pickle
k32 = ctypes.WinDLL('kernel32'); k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
h = k32.OpenProcess(0x410, False, int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip()))
def read(a, n):
    b = ctypes.create_string_buffer(n); g = ctypes.c_size_t(); k32.ReadProcessMemory(h, ctypes.c_void_p(a), b, n, ctypes.byref(g)); return b.raw[:g.value]
block = int(sys.argv[1], 16); planet = block - 0x3398
obj = read(planet, 0xD7500)
bptr, bn = struct.unpack_from('<QI', obj, 0x3398); sptr, sn = struct.unpack_from('<QI', obj, 0x33A8); optr, on = struct.unpack_from('<QI', obj, 0x33B8)
bundle = dict(planet=planet, obj=obj, types=(bptr, read(bptr, 0xE0 * bn), bn), slots=(sptr, read(sptr, 12 * sn), sn),
              overrides=(optr, read(optr, 0x30 * on), on))
pickle.dump(bundle, open(sys.argv[2], 'wb'))
print('object', hex(len(obj)), 'types', bn, 'slots', sn, 'overrides', on)
