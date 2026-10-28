# READ-ONLY diagnostic (docs/research/PLANET_BUILDING_PLACEMENT.md). Run while NMS is open:
#   python scripts/diagnostics/find_planet_values.py <start hex> <length hex> <u64 hex> [...]
# Searches one memory range (e.g. a planet's building records) for 64-bit values such as marker building seeds,
# printing each hit's offset from the start. Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, struct, subprocess, sys
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
pid = int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip())
h = k32.OpenProcess(0x410, False, pid)
start, length = int(sys.argv[1], 16), int(sys.argv[2], 16)
buf = ctypes.create_string_buffer(length); got = ctypes.c_size_t()
k32.ReadProcessMemory(h, ctypes.c_void_p(start), buf, length, ctypes.byref(got))
data = buf.raw[:got.value]
for text in sys.argv[3:]:
    needle = struct.pack('<Q', int(text, 16))
    hits = []
    i = data.find(needle)
    while i >= 0:
        hits.append(i); i = data.find(needle, i + 1)
    print(f'{text}: ' + (', '.join(f'+{o:X}' for o in hits) if hits else 'not found'))
