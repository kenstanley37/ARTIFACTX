# (preamble shared with emu_columns.py) Runs NMS.exe's (build 184271) voxel column pass in Unicorn on a planet object read live: 0x1412B5F70 sets up the
# column table, 0x14128DB30 fills it (noise layers, then 0x14128F0A0: building pads +0x390, 16 bytes per column:
# height, distance, cluster extent, column radius). docs/research/PLANETARY_MULTITOOL_LOCATIONS.md.
# usage: python emu_columns.py <planet bundle> <data snapshot> <columns.json> <out.json>
#   bundle   = read_planet_object_live.py output; snapshot = NMS.exe .data copied from the running game (pickle
#   {va: bytes}, read-only), for tables filled at start-up; columns.json = [[x, y, z], ...] planet-relative points.
#   Both pickles are local files written by our own read-only readers (as for emu_overrides.py), never downloaded.
import struct, sys, json, math, os, pickle
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE, UC_HOOK_MEM_UNMAPPED, UcError, UC_PROT_ALL
from unicorn import x86_const as X
from nmsbuild import A, BUILD   # build-184271 addresses moved to the installed build

EXE = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
data = open(EXE, 'rb').read()
pe = pefile.PE(data=data, fast_load=True)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"]])
BASE = pe.OPTIONAL_HEADER.ImageBase
mu = Uc(UC_ARCH_X86, UC_MODE_64)
mu.mem_map(BASE, (pe.OPTIONAL_HEADER.SizeOfImage + 0xFFF) & ~0xFFF)
for s in pe.sections:
    mu.mem_write(BASE + s.VirtualAddress, data[s.PointerToRawData:s.PointerToRawData + min(s.SizeOfRawData, s.Misc_VirtualSize)])
for va, blob in pickle.load(open(sys.argv[2], 'rb')).items():
    mu.mem_write(va, blob)
STUBS, STACK, HEAP, SENTINEL = 0x10000000, 0x20000000, 0x30000000, 0x40000000
for a, n in ((STUBS, 0x10000), (STACK, 0x800000), (HEAP, 0x10000000), (SENTINEL, 0x1000)):
    mu.mem_map(a, n)
heap = [HEAP]


def alloc(n, align=16):
    if n > 0x4000000:
        raise RuntimeError(f'alloc of {n:#x} bytes at {mu.reg_read(X.UC_X86_REG_RIP):#x}')
    heap[0] = (heap[0] + align - 1) & ~(align - 1)
    a = heap[0]; heap[0] += max(n, 1); mu.mem_write(a, b'\0' * max(n, 1))
    return a


imports = {}
for k, imp in enumerate(i for d in pe.DIRECTORY_ENTRY_IMPORT for i in d.imports):
    stub = STUBS + 0x10 * k; mu.mem_write(stub, b"\xc3"); mu.mem_write(imp.address, struct.pack("<Q", stub))
    imports[stub] = (imp.name or b"#").decode()


def f32reg(r):
    return struct.unpack('<f', mu.reg_read(r).to_bytes(16, 'little')[:4])[0]


def on_stub(mu, addr, size, _):
    fn = imports.get(addr)
    if fn is None:
        return
    x, y = f32reg(X.UC_X86_REG_XMM0), f32reg(X.UC_X86_REG_XMM1)
    f = {'sinf': math.sin, 'cosf': math.cos, 'sqrtf': math.sqrt, 'floorf': math.floor, 'ceilf': math.ceil,
         'expf': math.exp, 'logf': math.log, 'truncf': math.trunc, 'acosf': math.acos, 'asinf': math.asin,
         'tanf': math.tan, 'roundf': lambda v: math.floor(abs(v) + 0.5) * (1 if v >= 0 else -1)}
    if fn in f or fn in ('atan2f', 'powf', 'fmodf'):
        v = f[fn](x) if fn in f else math.atan2(x, y) if fn == 'atan2f' else x ** y if fn == 'powf' else math.fmod(x, y)
        mu.reg_write(X.UC_X86_REG_XMM0, int.from_bytes(struct.pack('<f', v), 'little'))
    elif fn in ('memset', 'memcpy', 'memmove'):
        rcx, rdx, r8 = (mu.reg_read(r) for r in (X.UC_X86_REG_RCX, X.UC_X86_REG_RDX, X.UC_X86_REG_R8))
        if r8:
            mu.mem_write(rcx, bytes([rdx & 0xFF]) * r8 if fn == 'memset' else bytes(mu.mem_read(rdx, r8)))
        mu.reg_write(X.UC_X86_REG_RAX, rcx)
    elif fn == '_fdclass':
        v = f32reg(X.UC_X86_REG_XMM0)
        mu.reg_write(X.UC_X86_REG_RAX, 2 if v != v else 1 if v in (float('inf'), float('-inf')) else 0 if v == 0 else 0xFFFF)
    elif fn in ('_aligned_malloc', 'malloc', '??2@YAPEAX_K@Z'):
        rcx, rdx = mu.reg_read(X.UC_X86_REG_RCX), mu.reg_read(X.UC_X86_REG_RDX)
        mu.reg_write(X.UC_X86_REG_RAX, alloc(rcx, max(16, rdx if fn == '_aligned_malloc' else 16)))
    else:
        mu.reg_write(X.UC_X86_REG_RAX, 0)


mu.hook_add(UC_HOOK_CODE, on_stub, begin=STUBS, end=STUBS + 0x10000)
unmapped = []


LIVE = None
if os.environ.get('LIVE') == '1':   # fetch missing pages from the running game (read-only, PROCESS_VM_READ only)
    import ctypes, ctypes.wintypes as W, subprocess
    _k32 = ctypes.WinDLL('kernel32'); _k32.OpenProcess.restype = W.HANDLE
    _k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
    _h = _k32.OpenProcess(0x410, False, int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip()))
    def LIVE(a, n):
        b = ctypes.create_string_buffer(n); g = ctypes.c_size_t()
        _k32.ReadProcessMemory(_h, ctypes.c_void_p(a), b, n, ctypes.byref(g)); return b.raw[:g.value]


def on_unmapped(mu, access, addr, size, value, _):
    if len(unmapped) < 30:
        unmapped.append((addr, mu.reg_read(X.UC_X86_REG_RIP)))
    page = addr & ~0xFFF
    if page >= 0x7FF000000000:
        return False
    try:
        mu.mem_map(page, 0x1000, UC_PROT_ALL)
        if LIVE and page >= 0x10000:
            blob = LIVE(page, 0x1000)
            if blob:
                mu.mem_write(page, blob)
        return True
    except UcError:
        return False


mu.hook_add(UC_HOOK_MEM_UNMAPPED, on_unmapped)


def ret_with(rax):
    rsp = mu.reg_read(X.UC_X86_REG_RSP); ret = struct.unpack('<Q', bytes(mu.mem_read(rsp, 8)))[0]
    mu.reg_write(X.UC_X86_REG_RAX, rax); mu.reg_write(X.UC_X86_REG_RSP, rsp + 8); mu.reg_write(X.UC_X86_REG_RIP, ret)


# The game's frame allocator (build 184271): 0x142C91990(0) -> allocator, 0x142C92910(alloc, size, align, flag) ->
# block, 0x142C93ED0(alloc, mark, mark) -> rewind. Served from the emulator heap.
ALLOCATOR = alloc(0x100)
HOOKS = {A(0x142C91990): lambda: ret_with(ALLOCATOR),
         A(0x142C92910): lambda: ret_with(alloc(mu.reg_read(X.UC_X86_REG_RDX), max(16, min(4096, mu.reg_read(X.UC_X86_REG_R8) or 16)))),
         A(0x142C93ED0): lambda: ret_with(0)}


def on_hook(mu, addr, size, _):
    HOOKS[addr]()


for a in HOOKS:
    mu.hook_add(UC_HOOK_CODE, on_hook, begin=a, end=a)


def call(fn, args, stack_args=()):
    rsp = (STACK + 0x7F0000) & ~0xF; rsp -= 0x400
    mu.mem_write(rsp, struct.pack('<Q', SENTINEL)); mu.reg_write(X.UC_X86_REG_RSP, rsp)
    for i, v in enumerate(stack_args):
        mu.mem_write(rsp + 0x28 + 8 * i, struct.pack('<Q', v))
    for r, v in zip((X.UC_X86_REG_RCX, X.UC_X86_REG_RDX, X.UC_X86_REG_R8, X.UC_X86_REG_R9), args):
        mu.reg_write(r, v)
    mu.emu_start(fn, SENTINEL, count=int(os.environ.get("EMU_COUNT", "4000000000")))



# --- one live terrain region regenerated: 0x141294A00(region) = 0x1412B3770(region, voxel, building block, 0x80) ---
# usage: python emu_far.py <planet bundle> <data snapshot> <region dump> <out.json>
#   region dump = {'region': address, 'mem': {address: bytes}} (live, read-only); the planet object and its building
#   type/slot arrays are mapped at their live addresses. The region's height grid (+0x1D0) is zeroed before the run.
B = pickle.load(open(sys.argv[1], 'rb'))
R = pickle.load(open(sys.argv[3], 'rb'))
blocks = dict(R['mem'])
blocks[B['planet']] = B['obj']
for key in ('types', 'slots', 'overrides'):
    blocks[B[key][0]] = B[key][1]
spans = sorted(((a & ~0xFFFF), (a + len(b) + 0xFFFF) & ~0xFFFF) for a, b in blocks.items() if a)
merged = []
for lo, hi in spans:
    if merged and lo <= merged[-1][1]:
        merged[-1][1] = max(merged[-1][1], hi)
    else:
        merged.append([lo, hi])
for lo, hi in merged:
    mu.mem_map(lo, hi - lo, UC_PROT_ALL)
for a, blob in blocks.items():
    if a:
        mu.mem_write(a, blob)
region = R['region']
grid_ptr = struct.unpack('<Q', bytes(mu.mem_read(region + 0x1D0, 8)))[0]
stride = R['mem'][region][0x86]
mu.mem_write(grid_ptr, b'\0' * 4 * stride * stride)
captured = {}
def on_density(mu, addr, size, _):
    rsp = mu.reg_read(X.UC_X86_REG_RSP)
    tbl = struct.unpack('<Q', bytes(mu.mem_read(rsp + 0x40, 8)))[0]   # 8th argument: the column table
    n = stride * stride
    def arr(off, k=1):
        p = struct.unpack('<Q', bytes(mu.mem_read(tbl + off, 8)))[0]
        return list(struct.unpack(f'<{n * k}f', bytes(mu.mem_read(p, 4 * n * k)))) if p else None
    captured.update(pads=arr(0x390, 4), l0=arr(0x8), l6=arr(0xC8), l7=arr(0xE8), r=arr(0x3C0))
mu.hook_add(UC_HOOK_CODE, on_density, begin=A(0x1412B1D60), end=A(0x1412B1D60))
# optional, emulator copy only: ZERO=lo-hi;... wipes and PATCH=off:fmt:value;... sets fields of the planet's
# TkVoxelGeneratorData (planet + 0x2090) before the run, to see what each part adds (2026-11-10).
VOX = B['planet'] + 0x2090
for spec in [v for v in os.environ.get('ZERO', '').split(';') if v]:
    lo, hi = (int(x, 16) for x in spec.split('-')); mu.mem_write(VOX + lo, bytes(hi - lo))
for spec in [v for v in os.environ.get('PATCH', '').split(';') if v]:
    off, fmt, val = spec.split(':'); mu.mem_write(VOX + int(off, 16), struct.pack(fmt, float(val) if 'f' in fmt else int(val)))
call(A(0x141294A00), (region,))
json.dump(captured, open(sys.argv[4] + '.table.json', 'w'))
grid = struct.unpack(f'<{stride * stride}f', bytes(mu.mem_read(grid_ptr, 4 * stride * stride)))
json.dump({'grid': grid, 'unmapped': [(hex(a), hex(r)) for a, r in unmapped]}, open(sys.argv[4], 'w'))
import numpy as np
g = np.array(grid).reshape(stride, stride); live = np.array(R['grid'])
print('emulated vs live grid: max |diff|', float(np.abs(g - live).max()), ' unmapped', len(unmapped))
print(np.round(g[:4, :6], 3)); print(np.round(live[:4, :6], 3))
if os.environ.get('DUMPCOL'):
    # density columns: voxel words at grid header +0 (u32 each, density in the high u16), strides +8 (row) / +0x10 (layer)
    hdr = struct.unpack('<Q', bytes(mu.mem_read(region + 0x60, 8)))[0]
    vptr, row, layers, layer = struct.unpack('<QiiI', bytes(mu.mem_read(hdr, 20)))
    cols = {}
    for ij in os.environ['DUMPCOL'].split(';'):
        i, j = map(int, ij.split(','))
        q = [struct.unpack('<H', bytes(mu.mem_read(vptr + 2 + 4 * (k * layer + i * row + j), 2)))[0] for k in range(layers)]
        cols[ij] = q
    json.dump(cols, open(sys.argv[4] + '.cols.json', 'w'))
    print('row', row, 'layer', layer, 'layers', layers)
if os.environ.get('SAMPLE'):
    # 0x1412B7F70's spawn sampling done with the game's transforms on the regenerated region (float32 in between):
    # g = local / (cell, cell height, cell); corners (i,j), (i,j+1), (i+1,j); point lerp(lerp(P00,P01,fz),P10,fx) with
    # fx, fz = frac(g / cell); world = 0x141291310(point); height = |world| - R.
    import numpy as np
    f32 = np.float32
    b = R['mem'][region]
    cx = f32(b[0x84]) / f32(b[0x86] - b[0x88]); cy = f32(b[0x85]) / f32(b[0x87] - (b[0x88] >> 1))
    row = struct.unpack('<i', bytes(mu.mem_read(struct.unpack('<Q', bytes(mu.mem_read(region + 0x60, 8)))[0] + 8, 4)))[0]
    S = alloc(0x100)
    for p in json.load(open(os.environ['SAMPLE'])):
        mu.mem_write(S, struct.pack('<4f', *p, 1.0))
        call(A(0x141291DF0), (region, S + 0x40, S))
        L = np.array(struct.unpack('<4f', bytes(mu.mem_read(S + 0x40, 16))), dtype=f32)
        g = L / np.array([cx, cy, cx, 1], dtype=f32)
        i, j = int(np.floor(g[0])), int(np.floor(g[2]))
        H = lambda a, c: f32(grid[a * row + c]) / cy
        P00 = np.array([f32(i) * cx, H(i, j) * cy, f32(j) * cx, 1], dtype=f32)
        P01 = np.array([f32(i) * cx, H(i, j + 1) * cy, f32(j + 1) * cx, 1], dtype=f32)
        P10 = np.array([f32(i + 1) * cx, H(i + 1, j) * cy, f32(j) * cx, 1], dtype=f32)
        gz, gx = g[2] / cx, g[0] / cx
        fz, fx = gz - f32(np.floor(gz)), gx - f32(np.floor(gx))
        a = (P01 - P00) * fz + P00
        pt = (P10 - a) * fx + a
        mu.mem_write(S + 0x80, pt.astype('<f4').tobytes()); mu.mem_write(S + 0xC0, struct.pack('<4f', 0, 1, 0, 0))
        call(A(0x141291310), (region, S + 0x40, S + 0x80, S + 0xC0))
        w = np.array(struct.unpack('<4f', bytes(mu.mem_read(S + 0x40, 16))), dtype=f32)
        print('   L', np.round(L, 3), 'g', np.round(g, 3), 'ij', i, j, 'f', round(float(fx), 3), round(float(fz), 3), 'pt', np.round(pt, 3), 'w', np.round(w, 2))
        print(f'point {np.round(p, 2)}: game sampling height {float(np.sqrt(np.sum(w[:3] * w[:3], dtype=f32))) - float(os.environ.get("PLANET_R", "130048")):.4f}')
