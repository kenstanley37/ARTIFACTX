# READ-ONLY diagnostic (docs/research/REGION_STAR_COUNT.md). Run while NMS is open:
#   python scripts/diagnostics/read_region_star_positions.py <x> <y> <z> > positions.json
# Dumps every star the running game holds for one loaded region (signed region words, e.g.
# 12 92 1756 for Noturbo Spur): its system index (from the stored packed address) and stored
# position. Same access rules as read_region_star_counts.py: query/read access only (0x410),
# code bytes verified before following pointers, no writes, no game code executed.
import ctypes, ctypes.wintypes as W, struct, subprocess, json, sys
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
want = tuple(int(v) for v in sys.argv[1:4])  # x, y, z
pid = int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip())
h = k32.OpenProcess(0x410, False, pid)
if not h: sys.exit('cannot open NMS for reading')
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
expect = {0x32E2E2: bytes.fromhex('488b3d1ff4b506'), 0x32E339: bytes.fromhex('48895f10'),
          0x1365478: bytes.fromhex('498b4f58418b4754'),
          # lookup reads positions at [slot+0x218] (stride 12) and addresses at [slot+0x258]
          0x1365586: bytes.fromhex('488b8358020000'), 0x13655A9: bytes.fromhex('488b8318020000')}
for rva, b in expect.items():
    if read(base + rva, len(b)) != b: sys.exit(f'unsupported build: code at {rva:X} differs')
gmap = u64(u64(base + 0x6E8D708) + 0x95EFA8 + 0x10)
pool = gmap + 0x50
count, arr = u32(pool + 0x54), u64(pool + 0x58)
for i in range(min(count, 512)):
    slot = u64(arr + 8 * i)
    x, z, y = struct.unpack('<hhh', read(slot + 0x260, 6))
    if (x, y, z) != want: continue
    n, total = u32(slot), u32(slot + 0x214)
    pos = read(u64(slot + 0x218), 12 * total)
    addr = read(u64(slot + 0x258), 8 * total)
    stars = []
    for k in range(total):
        a = struct.unpack_from('<Q', addr, 8 * k)[0]
        stars.append(dict(entry=k + 1, system=(a >> 40) & 0xFFF, address=hex(a), pos=struct.unpack_from('<3f', pos, 12 * k)))
    print(json.dumps(dict(region=[x, y, z], n=n, total=total, stars=stars)))
    break
else:
    sys.exit(f'region {want} not loaded')
