# READ-ONLY diagnostic. Run while NMS is open:
#   python scripts/diagnostics/dump_memory.py <address hex> <before hex> <after hex>
# Prints memory around an address as 16-byte rows (hex words and floats). Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, struct, subprocess, sys
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
pid = int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip())
h = k32.OpenProcess(0x410, False, pid)
a, before, after = (int(x, 16) for x in sys.argv[1:4])
buf = ctypes.create_string_buffer(before + after); got = ctypes.c_size_t()
k32.ReadProcessMemory(h, ctypes.c_void_p(a - before), buf, before + after, ctypes.byref(got))
raw = buf.raw[:got.value]
for o in range(0, len(raw) - 15, 16):
    row = raw[o:o + 16]
    print(f'{o - before:+06X}: {row.hex(" ", 4)}   ' + ' '.join(f'{x:.6g}' for x in struct.unpack('<4f', row)))
