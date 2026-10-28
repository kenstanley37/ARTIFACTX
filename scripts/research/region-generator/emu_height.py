# Runs NMS.exe's terrain height function 0x14128AA20 (build 183791; docs/research/PLANET_BUILDING_PLACEMENT.md) in
# Unicorn on a planet's real TkVoxelGeneratorData (read_terrain_live.py output) and compares it with the height of
# each override record (|record - planet position| - radius). Ground truth for the uber-noise port.
# usage: python emu_height.py <terrain.json> [live]   ("live": first copy NMS.exe's .data from the running game,
#        read-only, for tables only filled at start-up)
import struct, sys, json, math, os
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE, UC_HOOK_MEM_UNMAPPED, UcError, UC_PROT_ALL
from unicorn import x86_const as X
EXE = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
data = open(EXE, 'rb').read()
pe = pefile.PE(data=data, fast_load=True)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"]])
BASE = pe.OPTIONAL_HEADER.ImageBase
mu = Uc(UC_ARCH_X86, UC_MODE_64)
mu.mem_map(BASE, (pe.OPTIONAL_HEADER.SizeOfImage + 0xFFF) & ~0xFFF)
for s in pe.sections:
    mu.mem_write(BASE + s.VirtualAddress, data[s.PointerToRawData:s.PointerToRawData + min(s.SizeOfRawData, s.Misc_VirtualSize)])
STUBS, STACK, HEAP, SENTINEL = 0x10000000, 0x20000000, 0x30000000, 0x40000000
for a, n in ((STUBS, 0x10000), (STACK, 0x400000), (HEAP, 0x1000000), (SENTINEL, 0x1000)): mu.mem_map(a, n)
heap = [HEAP]
def alloc(n, align=16):
    heap[0] = (heap[0] + align - 1) & ~(align - 1); a = heap[0]; heap[0] += max(n, 1); mu.mem_write(a, b'\0' * max(n, 1)); return a
imports = {}
for k, imp in enumerate(i for d in pe.DIRECTORY_ENTRY_IMPORT for i in d.imports):
    stub = STUBS + 0x10 * k; mu.mem_write(stub, b"\xc3"); mu.mem_write(imp.address, struct.pack("<Q", stub))
    imports[stub] = (imp.name or b"#").decode()
used = {}
def f32reg(r): return struct.unpack('<f', mu.reg_read(r).to_bytes(16, 'little')[:4])[0]
def on_stub(mu, addr, size, _):
    fn = imports.get(addr)
    if fn is None: return
    used[fn] = used.get(fn, 0) + 1
    x, y = f32reg(X.UC_X86_REG_XMM0), f32reg(X.UC_X86_REG_XMM1)
    f = {'sinf': math.sin, 'cosf': math.cos, 'sqrtf': math.sqrt, 'floorf': math.floor, 'ceilf': math.ceil, 'expf': math.exp,
         'logf': math.log, 'roundf': lambda v: math.floor(abs(v) + 0.5) * (1 if v >= 0 else -1), 'truncf': math.trunc, 'acosf': math.acos, 'asinf': math.asin, 'tanf': math.tan}
    if fn in f or fn in ('atan2f', 'powf', 'fmodf'):
        v = f[fn](x) if fn in f else math.atan2(x, y) if fn == 'atan2f' else x ** y if fn == 'powf' else math.fmod(x, y)
        mu.reg_write(X.UC_X86_REG_XMM0, int.from_bytes(struct.pack('<f', v), 'little'))
    elif fn in ('_aligned_malloc', 'malloc', '??2@YAPEAX_K@Z'):
        rcx, rdx = mu.reg_read(X.UC_X86_REG_RCX), mu.reg_read(X.UC_X86_REG_RDX)
        mu.reg_write(X.UC_X86_REG_RAX, alloc(rcx, max(16, rdx if fn == '_aligned_malloc' else 16)))
    elif fn in ('EnterCriticalSection', 'LeaveCriticalSection', 'free', '_aligned_free'):
        mu.reg_write(X.UC_X86_REG_RAX, 0)
    else:
        print('UNHANDLED import', fn); mu.reg_write(X.UC_X86_REG_RAX, 0)
mu.hook_add(UC_HOOK_CODE, on_stub, begin=STUBS, end=STUBS + 0x10000)
zero = []
def on_unmapped(mu, access, addr, size, value, _):
    page = addr & ~0xFFF
    try: mu.mem_map(page, 0x1000, UC_PROT_ALL); zero.append(addr); return True
    except UcError: return False
mu.hook_add(UC_HOOK_MEM_UNMAPPED, on_unmapped)
def call(fn, args):
    rsp = (STACK + 0x3F0000) & ~0xF; rsp -= 0x100
    mu.mem_write(rsp, struct.pack('<Q', SENTINEL)); mu.reg_write(X.UC_X86_REG_RSP, rsp)
    for r, v in zip((X.UC_X86_REG_RCX, X.UC_X86_REG_RDX, X.UC_X86_REG_R8, X.UC_X86_REG_R9), args): mu.reg_write(r, v)
    mu.emu_start(fn, SENTINEL, count=50_000_000)
    return f32reg(X.UC_X86_REG_XMM0)
if len(sys.argv) > 2 and sys.argv[2] == 'live':
    import ctypes, ctypes.wintypes as W, subprocess
    k32 = ctypes.WinDLL('kernel32'); k32.OpenProcess.restype = W.HANDLE
    k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
    h = k32.OpenProcess(0x410, False, int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip()))
    psapi = ctypes.WinDLL('psapi'); mods = (ctypes.c_void_p * 1)(); need = W.DWORD()
    psapi.EnumProcessModulesEx(h, mods, ctypes.sizeof(mods), ctypes.byref(need), 3); delta = mods[0] - BASE
    for s in pe.sections:
        if s.Name.rstrip(b'\0') != b'.data': continue
        n = s.Misc_VirtualSize; buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
        k32.ReadProcessMemory(h, ctypes.c_void_p(BASE + s.VirtualAddress + delta), buf, n, ctypes.byref(got))
        raw = bytearray(buf.raw[:got.value])
        for o in range(0, len(raw) - 7, 8):
            q = struct.unpack_from('<Q', raw, o)[0]
            if BASE + delta <= q < BASE + delta + pe.OPTIONAL_HEADER.SizeOfImage: struct.pack_into('<Q', raw, o, q - delta)
        mu.mem_write(BASE + s.VirtualAddress, bytes(raw)); print('copied live .data', hex(len(raw)))
J = json.load(open(sys.argv[1]))
terrain = alloc(0x1200, 0x40); mu.mem_write(terrain, bytes.fromhex(J['terrain']))
P, R = J['position'][:3], J['radius']
pt = alloc(16)
for rec in J['overrides']:
    v = [rec['pos'][i] - P[i] for i in range(3)]; d = math.sqrt(sum(c * c for c in v))
    site = [c / d * R for c in v]
    mu.mem_write(pt, struct.pack('<4f', *site, 1.0))
    try: h = call(0x14128AA20, (pt, terrain))
    except UcError as e: print('emu error', e, hex(mu.reg_read(X.UC_X86_REG_RIP))); break
    print(f"type {rec['type']:3}  record height {d - R:9.3f}   emulated height {h:9.3f}")
print('imports', used, 'zero-filled pages', len(set(a & ~0xFFF for a in zero)))
