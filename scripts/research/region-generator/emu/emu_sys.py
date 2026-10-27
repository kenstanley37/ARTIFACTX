import os
# Runs NMS.exe's own solar-system generator (build 180836) in Unicorn: attributes
# (0x14132d5e0), setup (0x14164c580), layout (0x14164da40) and planet types (0x14164c770),
# with the real biome tables, and prints each planet's biome. One-off, 2026-10-22.
import struct, sys, pefile
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
for a, n in ((STUBS, 0x10000), (STACK, 0x400000), (HEAP, 0x10000000), (SENTINEL, 0x1000)): mu.mem_map(a, n)
heap = [HEAP]
def alloc(n, align=16):
    heap[0] = (heap[0] + align - 1) & ~(align - 1); a = heap[0]; heap[0] += max(n, 1); mu.mem_write(a, b'\0' * max(n,1)); return a
imports = {}; k = 0
for d in pe.DIRECTORY_ENTRY_IMPORT:
    for imp in d.imports:
        stub = STUBS + 0x10 * k; k += 1
        mu.mem_write(stub, b"\xc3"); mu.mem_write(imp.address, struct.pack("<Q", stub))
        imports[stub] = (imp.name or b"#").decode()
def cstr(a, n=512):
    b = bytes(mu.mem_read(a, n)); return b[:b.index(0)] if 0 in b else b
used = {}
import math
def on_stub(mu, addr, size, _):
    fn = imports.get(addr)
    if fn is None: return
    used[fn] = used.get(fn, 0) + 1
    rcx, rdx, r8 = (mu.reg_read(r) for r in (X.UC_X86_REG_RCX, X.UC_X86_REG_RDX, X.UC_X86_REG_R8))
    ret = 0
    if fn in ('memcpy', 'memmove'):
        if r8: mu.mem_write(rcx, bytes(mu.mem_read(rdx, r8)))
        ret = rcx
    elif fn == 'memset':
        if r8: mu.mem_write(rcx, bytes([rdx & 0xFF]) * r8)
        ret = rcx
    elif fn == 'strlen': ret = len(cstr(rcx))
    elif fn in ('toupper', 'tolower', '_stricmp', '_strnicmp', 'strcmp', 'strncmp', 'strstr', 'strchr', 'strrchr', '_Mtx_lock', '_Mtx_unlock', '__stdio_common_vsprintf', '_stat64i32'):
        ret = 0
    elif fn in ('malloc', '??2@YAPEAX_K@Z', '??_U@YAPEAX_K@Z'): ret = alloc(rcx)
    elif fn == '_aligned_malloc': ret = alloc(rcx, max(16, rdx))
    elif fn in ('free', '_aligned_free', '??3@YAXPEAX@Z', '??_V@YAXPEAX@Z'): ret = 0
    elif fn in ('sinf','cosf','sqrtf','atan2f','floorf','powf','expf','logf','fmodf','acosf','asinf','tanf','ceilf'):
        x = struct.unpack('<f', bytes(mu.reg_read(X.UC_X86_REG_XMM0).to_bytes(16,'little')[:4]))[0]
        y = struct.unpack('<f', bytes(mu.reg_read(X.UC_X86_REG_XMM1).to_bytes(16,'little')[:4]))[0]
        f = {'sinf':math.sin,'cosf':math.cos,'sqrtf':math.sqrt,'floorf':math.floor,'expf':math.exp,'logf':math.log,'acosf':math.acos,'asinf':math.asin,'tanf':math.tan,'ceilf':math.ceil}
        if fn in f: v = f[fn](x)
        elif fn == 'atan2f': v = math.atan2(x, y)
        elif fn == 'powf': v = x ** y
        elif fn == 'fmodf': v = math.fmod(x, y)
        mu.reg_write(X.UC_X86_REG_XMM0, int.from_bytes(struct.pack('<f', v), 'little'))
        return
    elif fn == 'frexp':
        x = struct.unpack('<d', bytes(mu.reg_read(X.UC_X86_REG_XMM0).to_bytes(16,'little')[:8]))[0]
        m, e = math.frexp(x); mu.mem_write(rdx, struct.pack('<i', e))
        mu.reg_write(X.UC_X86_REG_XMM0, int.from_bytes(struct.pack('<d', m), 'little')); return
    else:
        print('UNHANDLED import', fn)
    mu.reg_write(X.UC_X86_REG_RAX, ret & (1 << 64) - 1)
mu.hook_add(UC_HOOK_CODE, on_stub, begin=STUBS, end=STUBS + 0x10000)
zeromapped = []
def on_unmapped(mu, access, addr, size, value, _):
    page = addr & ~0xFFF
    if page < 0x10000: print('NULL-ish access', hex(addr), 'at', hex(mu.reg_read(X.UC_X86_REG_RIP)))
    try: mu.mem_map(page, 0x1000, UC_PROT_ALL); zeromapped.append(page); return True
    except UcError: return False
mu.hook_add(UC_HOOK_MEM_UNMAPPED, on_unmapped)
def load_mbin(path):
    b = bytearray(open(path, 'rb').read())
    h = alloc(len(b) + 0x100, 0x100)
    for i in range(0, len(b) - 15, 4):
        m = struct.unpack_from('<I', b, i + 12)[0]
        if m in (0xAAAAAA01, 0xEEEEEE01) and i % 8 == 0:
            rel = struct.unpack_from('<q', b, i)[0]; cnt = struct.unpack_from('<I', b, i + 8)[0]
            if m == 0xEEEEEE01 or cnt: struct.pack_into('<Q', b, i, h + i + rel)
            else: struct.pack_into('<Q', b, i, 0)
    mu.mem_write(h, bytes(b)); return h + 0x20
biomelist = load_mbin('biomelist.mbin'); biomefiles = load_mbin('biomefiles.mbin')
G = 0x146e8d708
O = alloc(0x800000); P = alloc(0x600000)
mu.mem_write(G, struct.pack('<Q', O)); mu.mem_write(O + 0x72afb0, struct.pack('<Q', P))
mu.mem_write(P + 0x5205e0, struct.pack('<Q', biomefiles)); mu.mem_write(P + 0x5205e8, struct.pack('<Q', biomefiles)); mu.mem_write(P + 0x5205f0, struct.pack('<Q', biomelist))
mu.mem_write(0x146e8d748, struct.pack('<I', 1))
# GcSolarGenerationGlobals lives at 0x145215050 at runtime (zero in the file): moons on
# (+0x28C), extra prime planet chance 33% (+0x244), ... - load the real file there.
_sg = load_mbin('solargen.mbin'); _len = len(open('solargen.mbin','rb').read()) - 0x20
mu.mem_write(0x145215050, bytes(mu.mem_read(_sg, min(_len, 0x2a0))))
GEN = P + 0x521180
def call(fn, args, stack_args=()):
    rsp = (STACK + 0x3F0000) & ~0xF
    rsp -= 0x100
    mu.mem_write(rsp, struct.pack('<Q', SENTINEL))
    for i, v in enumerate(stack_args): mu.mem_write(rsp + 0x28 + 8 * i, struct.pack('<Q', v))
    mu.reg_write(X.UC_X86_REG_RSP, rsp)
    for r, v in zip((X.UC_X86_REG_RCX, X.UC_X86_REG_RDX, X.UC_X86_REG_R8, X.UC_X86_REG_R9), args): mu.reg_write(r, v)
    mu.emu_start(fn, SENTINEL, count=200_000_000)
    return mu.reg_read(X.UC_X86_REG_RAX)
def system(address):
    attrs = alloc(0x1000)
    call(0x14132d5e0, (attrs, address))
    sysobj = alloc(0x4000); holder = alloc(0x20); mu.mem_write(holder, struct.pack('<QQ', 0, sysobj))
    seed = alloc(0x10); mu.mem_write(seed, struct.pack('<QQ', address, 1))
    lo = address & 0xFFFFFFFF; hi = address >> 32
    s1 = ((((lo << 16) | (lo >> 16)) & 0xFFFFFFFF) ^ hi ^ lo) & 0xFFFFFFFF
    mu.mem_write(GEN + 0x510, struct.pack('<II', lo if lo else 1, s1))
    mu.mem_write(sysobj + 0x2480, struct.pack('<QQ', address, 1))
    out30 = alloc(0x100)
    call(0x14164c580, (GEN, seed, attrs, out30), (holder,))
    call(0x14164da40, (GEN, attrs, holder, 0))
    call(0x14164c770, (GEN, attrs, holder, out30))
    n = struct.unpack('<i', bytes(mu.mem_read(sysobj + 0x2544, 4)))[0]
    star = struct.unpack('<i', bytes(mu.mem_read(attrs + 0x750, 4)))[0]
    pc, prim = struct.unpack('<ii', bytes(mu.mem_read(attrs + 0x740, 8)))
    seeds = [struct.unpack('<Q', bytes(mu.mem_read(attrs + 0x4b0 + 0x10 * i, 8)))[0] for i in range(max(n, 0))]
    biomes = [struct.unpack('<i', bytes(mu.mem_read(sysobj + 0x21b0 + 0x58 * i, 4)))[0] for i in range(8)]
    subs = [struct.unpack('<i', bytes(mu.mem_read(sysobj + 0x21b4 + 0x58 * i, 4)))[0] for i in range(8)]
    return dict(n=n, star=star, pc=pc, prime=prim, seeds=seeds, biomes=biomes, subs=subs)
if __name__ == '__main__':
    for a in sys.argv[1:]:
        try: print(a, system(int(a, 16)))
        except UcError as e: print('emu error', e, hex(mu.reg_read(X.UC_X86_REG_RIP)))
    print('imports', used, 'zero pages', len(zeromapped))
