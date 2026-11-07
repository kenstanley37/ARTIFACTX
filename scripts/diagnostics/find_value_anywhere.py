# READ-ONLY diagnostic (docs/research/PLANET_BUILDING_PLACEMENT.md). Run while NMS is open:
#   python scripts/diagnostics/find_value_anywhere.py <u64 hex> [context bytes]
# Searches all of the game's writable memory for a 64-bit value (e.g. a marker's building seed) and prints the
# bytes around every hit, as 32-bit words and floats. Query/read access only (0x410), no writes.
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
def read(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    ok = k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got))
    return buf.raw[:got.value] if ok or got.value else b''
needle = struct.pack('<Q', int(sys.argv[1], 16))
ctx = int(sys.argv[2], 0) if len(sys.argv) > 2 else 0x60
addr, mbi, hits = 0, MBI(), []
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    if mbi.State == 0x1000 and mbi.Protect == 0x04:
        for off in range(0, size, 1 << 24):
            chunk = read(start + off, min(1 << 24, size - off))
            i = chunk.find(needle)
            while i >= 0:
                hits.append(start + off + i); i = chunk.find(needle, i + 1)
    addr = start + size
    if addr >= 0x7FFFFFFFFFFF: break
for a in hits:
    print(f'== hit at {a:X}')
    raw = read(a - ctx, 2 * ctx)
    for o in range(0, len(raw) - 3, 16):
        words = raw[o:o + 16]
        f = struct.unpack_from(f'<{len(words) // 4}f', words)
        print(f'  {o - ctx:+05X}: {words.hex(" ", 4)}   ' + ' '.join(f'{x:.6g}' for x in f))
print(len(hits), 'hits')
