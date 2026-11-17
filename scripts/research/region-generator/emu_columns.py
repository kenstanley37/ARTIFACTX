# Runs NMS.exe's (build 184271) voxel column pass in Unicorn on a planet object read live: 0x1412B5F70 sets up the
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


def on_unmapped(mu, access, addr, size, value, _):
    if len(unmapped) < 30:
        unmapped.append((addr, mu.reg_read(X.UC_X86_REG_RIP)))
    try:
        mu.mem_map(addr & ~0xFFF, 0x1000, UC_PROT_ALL); return True
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


# --- planet object --------------------------------------------------------------------------------------------------
B = pickle.load(open(sys.argv[1], 'rb'))
obj = bytearray(B['obj']); PL = alloc(len(obj) + 0x100, 0x100)
for off, key in ((0x3398, 'types'), (0x33A8, 'slots'), (0x33B8, 'overrides')):
    blob = bytearray(B[key][1])
    if key == 'types' and os.environ.get('RADIUS'):   # experiment: every record radius (+0xC0) forced to this value
        for i in range(B['types'][2]):
            struct.pack_into('<f', blob, 0xE0 * i + 0xC0, float(os.environ['RADIUS']))
    a = alloc(len(blob) + 0x100); mu.mem_write(a, bytes(blob)); struct.pack_into('<Q', obj, off, a)
mu.mem_write(PL, bytes(obj))
VOXEL, BLOCK = PL + 0x2090, PL + 0x3398

cols = json.load(open(sys.argv[3]))
n = len(cols)
xs, ys, zs, rs = (alloc(4 * n + 64) for _ in range(4))
mu.mem_write(xs, struct.pack(f'<{n}f', *(c[0] for c in cols)))
mu.mem_write(ys, struct.pack(f'<{n}f', *(c[1] for c in cols)))
mu.mem_write(zs, struct.pack(f'<{n}f', *(c[2] for c in cols)))
table = alloc(0x600, 0x40)
desc = alloc(0x20); mu.mem_write(desc, struct.pack('<QQQ', ALLOCATOR, 0, 0))
call(A(0x1412B5F70), (table, n, desc))
# the driver writes |p| per column into the table's +0x3C0 array before the pass
rptr = struct.unpack('<Q', bytes(mu.mem_read(table + 0x3C0, 8)))[0]
if rptr:
    mu.mem_write(rptr, struct.pack(f'<{n}f', *(math.sqrt(c[0] ** 2 + c[1] ** 2 + c[2] ** 2) for c in cols)))
lod = int(os.environ.get('LOD', '0'))
region = alloc(0x1000)
call(A(0x14128DB30), (table, xs, ys, zs), (VOXEL, BLOCK, region, lod, 0))
pads = struct.unpack('<Q', bytes(mu.mem_read(table + 0x390, 8)))[0]
out = [list(struct.unpack('<4f', bytes(mu.mem_read(pads + 16 * i, 16)))) for i in range(n)] if pads else []
arrays = {}
for off in (0x8, 0x20, 0xC8, 0xE0, 0xE8, 0x100, 0x350, 0x360, 0x370, 0x378, 0x380, 0x388, 0x398, 0x3C0):
    p = struct.unpack('<Q', bytes(mu.mem_read(table + off, 8)))[0]
    try:
        arrays[hex(off)] = list(struct.unpack(f'<{n}f', bytes(mu.mem_read(p, 4 * n))))
    except UcError:
        arrays[hex(off)] = f'unreadable {p:#x}'
json.dump({'pads': out, 'arrays': arrays, 'unmapped': [(hex(a), hex(r)) for a, r in unmapped]}, open(sys.argv[4], 'w'))
print(f'{n} columns; pads at {pads:#x}; unmapped {len(unmapped)}')
