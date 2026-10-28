# Runs NMS.exe's override placement 0x141635D10 (build 183791; docs/research/PLANET_BUILDING_PLACEMENT.md) in Unicorn
# on a planet object read live (read_planet_object_live.py) and compares the records with the game's.
# usage: python emu_overrides.py <planet bundle> <planetbuildingtable.mbin>
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
for a, n in ((STUBS, 0x10000), (STACK, 0x400000), (HEAP, 0x8000000), (SENTINEL, 0x1000)): mu.mem_map(a, n)
heap = [HEAP]
def alloc(n, align=16):
    if n > 0x2000000: raise RuntimeError(f'alloc of {n:#x} bytes at {mu.reg_read(X.UC_X86_REG_RIP):#x}')
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
    elif fn in ('memset', 'memcpy', 'memmove'):
        rcx, rdx, r8 = (mu.reg_read(r) for r in (X.UC_X86_REG_RCX, X.UC_X86_REG_RDX, X.UC_X86_REG_R8))
        if r8: mu.mem_write(rcx, bytes([rdx & 0xFF]) * r8 if fn == 'memset' else bytes(mu.mem_read(rdx, r8)))
        mu.reg_write(X.UC_X86_REG_RAX, rcx)
    elif fn == '_fdclass':   # MSVC: FP_NAN 2, FP_INFINITE 1, FP_ZERO 0, FP_NORMAL -1
        v = f32reg(X.UC_X86_REG_XMM0)
        mu.reg_write(X.UC_X86_REG_RAX, 2 if v != v else 1 if v in (float('inf'), float('-inf')) else 0 if v == 0 else 0xFFFF)
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
    if len(zero) < 25: print(f'  unmapped {"write" if access in (20, 22) else "read"} {addr:#x} at {mu.reg_read(X.UC_X86_REG_RIP):#x}')
    try: mu.mem_map(page, 0x1000, UC_PROT_ALL); zero.append(addr); return True
    except UcError: return False
mu.hook_add(UC_HOOK_MEM_UNMAPPED, on_unmapped)
def call(fn, args, stack_args=()):
    rsp = (STACK + 0x3F0000) & ~0xF; rsp -= 0x200
    mu.mem_write(rsp, struct.pack('<Q', SENTINEL)); mu.reg_write(X.UC_X86_REG_RSP, rsp)
    for i, v in enumerate(stack_args): mu.mem_write(rsp + 0x28 + 8 * i, struct.pack('<Q', v))
    for r, v in zip((X.UC_X86_REG_RCX, X.UC_X86_REG_RDX, X.UC_X86_REG_R8, X.UC_X86_REG_R9), args): mu.reg_write(r, v)
    mu.emu_start(fn, SENTINEL, count=int(os.environ.get("EMU_COUNT", "3000000000")))
    return f32reg(X.UC_X86_REG_XMM0)

# --- override placement: 0x141635D10 on a planet object read live (read_planet_object_live.py) ---------------------
import pickle
# Post-placement registration (takes a building record from the planet's pool, notifies a global manager through a
# vtable): positions are already decided, so skip it and return 0.
SKIP = {0x141630100}
def on_skip(mu, addr, size, _):
    if addr in SKIP:
        rsp = mu.reg_read(X.UC_X86_REG_RSP); ret = struct.unpack('<Q', bytes(mu.mem_read(rsp, 8)))[0]
        mu.reg_write(X.UC_X86_REG_RSP, rsp + 8); mu.reg_write(X.UC_X86_REG_RAX, 0); mu.reg_write(X.UC_X86_REG_RIP, ret)
for a in SKIP: mu.hook_add(UC_HOOK_CODE, on_skip, begin=a, end=a)
# The game's pool allocator (global state is empty in the file): allocate (0x142C8E290 / 0x142C8E190: rdx = size,
# r8 = alignment) or free (0x142C8C960: no-op) on the emulator heap instead.
POOL = {0x142C8E290: 'alloc', 0x142C8E190: 'alloc', 0x142C8C960: 'free'}
def on_pool(mu, addr, size, _):
    rsp = mu.reg_read(X.UC_X86_REG_RSP); ret = struct.unpack('<Q', bytes(mu.mem_read(rsp, 8)))[0]
    if POOL[addr] == 'alloc':
        n, al = mu.reg_read(X.UC_X86_REG_RDX), mu.reg_read(X.UC_X86_REG_R8)
        mu.reg_write(X.UC_X86_REG_RAX, alloc(n, max(16, al if 0 < al <= 4096 else 16)))
    else: mu.reg_write(X.UC_X86_REG_RAX, 0)
    mu.reg_write(X.UC_X86_REG_RSP, rsp + 8); mu.reg_write(X.UC_X86_REG_RIP, ret)
for a in POOL: mu.hook_add(UC_HOOK_CODE, on_pool, begin=a, end=a)
WATCH = {0x141631160: 'accept', 0x1416362A5: 'accept-call-1', 0x1416360D3: 'loop-top', 0x1416365C6: 'loop-exit', 0x14163610A: 'draw', 0x14163120E: 'ret:sitequery', 0x141631348: 'ret:heightrule', 0x141631216: 'site-valid?', 0x1416365C6: 'phase2-entry', 0x141636600: 'p2-record', 0x141636740: 'p2-draw', 0x141636840: 'ret:p2-accept', 0x141636A45: 'p2-write', 0x141636D87: 'p2-done'}
hits = {}
def on_watch(mu, addr, size, _):
    k = WATCH[addr]
    if k.startswith('ret:'): k += '=' + str(mu.reg_read(X.UC_X86_REG_RAX) & 0xFF)
    hits[k] = hits.get(k, 0) + 1
for a in WATCH: mu.hook_add(UC_HOOK_CODE, on_watch, begin=a, end=a)
# Run-time global the accept test 0x141631160 requires (0 in the file, set by the game at start-up).
mu.mem_write(0x145340EC0, b'')
# Placement values the game sets at start-up (0 in the file; read from the running game 2026-10-28, build 183791):
# 0x145006F00 = 10, 0x145006EF8 = 35 (override function), 0x14535178C/790/794/798 = 600/500/1000/800 (chain distance
# band). Their source globals MBIN is not identified yet.
for va, v in ((0x145006F00, 10.0), (0x145006EF8, 35.0), (0x14535178C, 600.0), (0x145351790, 500.0), (0x145351794, 1000.0), (0x145351798, 800.0)):
    mu.mem_write(va, struct.pack('<f', v))
B = pickle.load(open(sys.argv[1], 'rb'))
def load_mbin(path):
    b = bytearray(open(path, 'rb').read()); hh = alloc(len(b) + 0x100, 0x100)
    for i in range(0, len(b) - 15, 4):
        m = struct.unpack_from('<I', b, i + 12)[0]
        if m in (0xAAAAAA01, 0xEEEEEE01) and i % 8 == 0:
            rel = struct.unpack_from('<q', b, i)[0]; cnt = struct.unpack_from('<I', b, i + 8)[0]
            struct.pack_into('<Q', b, i, hh + i + rel if (m == 0xEEEEEE01 or cnt) else 0)
    mu.mem_write(hh, bytes(b)); return hh + 0x20
table = load_mbin(sys.argv[2])
tableptr = alloc(16); mu.mem_write(tableptr, struct.pack('<Q', table))
obj = bytearray(B['obj']); PL = alloc(len(obj) + 0x100, 0x100)
def copy_array(off, blob):
    a = alloc(len(blob) + 0x100); mu.mem_write(a, blob); struct.pack_into('<Q', obj, off, a); return a
copy_array(0x3398, B['types'][1]); copy_array(0x33A8, B['slots'][1])
live_overrides = B['overrides'][1]
ov = copy_array(0x33B8, bytes(1) * len(live_overrides))
mu.mem_write(PL, bytes(obj))
seed = struct.unpack_from('<Q', obj, 0x3B60)[0]
lo, hi = seed & 0xFFFFFFFF, seed >> 32
mwc = alloc(16); mu.mem_write(mwc, struct.pack('<II', lo if lo else 1, (((lo >> 16) | (lo << 16)) & 0xFFFFFFFF) ^ hi ^ lo))
race = struct.unpack_from('<I', obj, 0x3594)[0]; level = struct.unpack_from('<I', obj, 0x3588)[0]
flag = 1 if level != 0 and not (4 <= level <= 7) and not obj[0x3B2F] and not obj[0x3B30] else 0
from unicorn import UC_HOOK_MEM_WRITE
writes = []
def on_write(mu, access, addr, size, value, _):
    if len(writes) < 40: writes.append((mu.reg_read(X.UC_X86_REG_RIP), addr - ov, size))
mu.hook_add(UC_HOOK_MEM_WRITE, on_write, begin=ov, end=ov + len(live_overrides) - 1)
print(f'planet seed {seed:016X} race {race} level {level} flag {flag} overrides {B["overrides"][2]}')
try:
    call(0x141635D10, (tableptr, race, level, PL + 0x3BA0), (PL, mwc, flag))
except UcError as e:
    print('emu error', e, hex(mu.reg_read(X.UC_X86_REG_RIP)))
out = bytes(mu.mem_read(ov, len(live_overrides)))
for i in range(B['overrides'][2]):
    a = struct.unpack_from('<3f', out, 0x30 * i); b = struct.unpack_from('<3f', live_overrides, 0x30 * i)
    ka = struct.unpack_from('<Q', out, 0x30 * i + 0x10)[0]; kb = struct.unpack_from('<Q', live_overrides, 0x30 * i + 0x10)[0]
    ta = struct.unpack_from('<I', out, 0x30 * i + 0x20)[0]; tb = struct.unpack_from('<I', live_overrides, 0x30 * i + 0x20)[0]
    print(f'rec {i}: type {ta}/{tb}  key {ka:016X} {"==" if ka == kb else "!="} {kb:016X}  dist {math.dist(a, b):10.3f}')
print('instructions run', mu.query(1) if False else '?', 'stopped at', hex(mu.reg_read(X.UC_X86_REG_RIP))); print('writes into overrides', [(hex(r), hex(o), n) for r, o, n in writes]); print('hits', hits); print('imports', used, 'zero-filled pages', len(set(x & ~0xFFF for x in zero)))
