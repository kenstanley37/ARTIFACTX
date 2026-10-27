# READ-ONLY diagnostic (docs/research/REGION_STAR_COUNT.md). Run with Python on Windows while NMS is open:
#   python scripts/diagnostics/read_region_star_counts.py > live.json
# Lists the galaxy-map region slots loaded in the running NMS (build 180836) with each
# slot's ordinary star count. Opens the process with query/read access only (0x410), never writes,
# injects or calls game code. Verifies the code bytes it relies on before following pointers.
import ctypes, ctypes.wintypes as W, struct, subprocess, json, sys
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
pid = int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip())
h = k32.OpenProcess(0x410, False, pid)
if not h: sys.exit('cannot open NMS for reading')
# Main module base via psapi.
psapi = ctypes.WinDLL('psapi')
mods = (W.HMODULE * 1)(); need = W.DWORD()
psapi.EnumProcessModulesEx(h, mods, ctypes.sizeof(mods), ctypes.byref(need), 3)
base = mods[0]
def read(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    if not k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got)) or got.value != n:
        raise IOError(f'read failed at {addr:X}')
    return buf.raw
def u64(a): return struct.unpack('<Q', read(a, 8))[0]
def u32(a): return struct.unpack('<I', read(a, 4))[0]
# Code checks: the galaxy-map store and the lookup's list fields, as disassembled from the file.
expect = {0x32E2E2: bytes.fromhex('488b3d1ff4b506'), 0x32E339: bytes.fromhex('48895f10'),
          0x1365478: bytes.fromhex('498b4f58418b4754')}
for rva, b in expect.items():
    if read(base + rva, len(b)) != b: sys.exit(f'unsupported build: code at {rva:X} differs')
manager = u64(base + 0x6E8D708)
gmap = u64(manager + 0x95EFA8 + 0x10)
pool = gmap + 0x50
count, arr = u32(pool + 0x54), u64(pool + 0x58)
print(f'manager {manager:X} galaxymap {gmap:X} active slots {count}')
rows = []
for i in range(min(count, 512)):
    slot = u64(arr + 8 * i)
    n, total = u32(slot), u32(slot + 0x214)
    w0, w1, w2 = struct.unpack('<hhh', read(slot + 0x260, 6))
    rows.append(dict(slot=hex(slot), n=n, total=total, words=[w0, w1, w2], tail=read(slot + 0x266, 0x10).hex()))
print(json.dumps(rows))
