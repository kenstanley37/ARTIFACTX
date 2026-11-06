# Samples the surface height of live terrain regions (cGcRegionTerrainNear/Mid/Far/VeryFar, build 184271) at points,
# by running the game's region transforms in Unicorn: 0x141291DF0 (planet point -> region grid coordinates) and
# 0x141291310 (region local point -> planet point), with the region's height grid (+0x1D0) interpolated in between
# as 0x141299710 does. docs/research/PLANETARY_MULTITOOL_LOCATIONS.md.
# usage: python emu_region.py <regiondump.pkl> <data snapshot> <points.json> <planet bundle>
#   regiondump = {'regions': [(address, name)], 'mem': {address: bytes}} read live (read-only); snapshot as for
#   emu_columns.py; both are our own local pickles, never downloaded. points.json = [[x, y, z], ...] planet-relative.
import struct, sys, json, math, os, pickle
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE, UC_HOOK_MEM_UNMAPPED, UcError, UC_PROT_ALL
from unicorn import x86_const as X

EXE = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
data = open(EXE, 'rb').read()
pe = pefile.PE(data=data, fast_load=True)
BASE = pe.OPTIONAL_HEADER.ImageBase
mu = Uc(UC_ARCH_X86, UC_MODE_64)
mu.mem_map(BASE, (pe.OPTIONAL_HEADER.SizeOfImage + 0xFFF) & ~0xFFF)
for s in pe.sections:
    mu.mem_write(BASE + s.VirtualAddress, data[s.PointerToRawData:s.PointerToRawData + min(s.SizeOfRawData, s.Misc_VirtualSize)])
for va, blob in pickle.load(open(sys.argv[2], 'rb')).items():
    mu.mem_write(va, blob)
STACK, SCR, SENTINEL = 0x20000000, 0x30000000, 0x40000000
for a, n in ((STACK, 0x100000), (SCR, 0x10000), (SENTINEL, 0x1000)):
    mu.mem_map(a, n)
D = pickle.load(open(sys.argv[1], 'rb'))
if len(sys.argv) > 4:   # the planet object bundle (read_planet_object_live.py): region +0x10 -> planet; voxel data at planet+0x2090
    PB = pickle.load(open(sys.argv[4], 'rb'))
    D['mem'][PB['planet']] = PB['obj']
spans = sorted(((a & ~0xFFFF), (a + len(b) + 0xFFFF) & ~0xFFFF) for a, b in D['mem'].items())
merged = []
for lo, hi in spans:
    if merged and lo <= merged[-1][1]:
        merged[-1][1] = max(merged[-1][1], hi)
    else:
        merged.append([lo, hi])
for lo, hi in merged:
    mu.mem_map(lo, hi - lo, UC_PROT_ALL)
for a, blob in D['mem'].items():
    mu.mem_write(a, blob)


misses = []


def on_unmapped(mu, access, addr, size, value, _):
    misses.append((addr, mu.reg_read(X.UC_X86_REG_RIP)))
    if addr < 0x10000 or addr >= 0x7FF000000000:
        print(f'  refused map {addr:#x} at {mu.reg_read(X.UC_X86_REG_RIP):#x}'); return False
    try:
        mu.mem_map(addr & ~0xFFF, 0x1000, UC_PROT_ALL); return True
    except UcError:
        return False


mu.hook_add(UC_HOOK_MEM_UNMAPPED, on_unmapped)
# the game's frame allocator, served from a scratch heap (as in emu_columns.py)
HEAP = 0x50000000
mu.mem_map(HEAP, 0x4000000)
heap = [HEAP + 0x1000]


def ret_with(rax):
    rsp = mu.reg_read(X.UC_X86_REG_RSP); ret = struct.unpack('<Q', bytes(mu.mem_read(rsp, 8)))[0]
    mu.reg_write(X.UC_X86_REG_RAX, rax); mu.reg_write(X.UC_X86_REG_RSP, rsp + 8); mu.reg_write(X.UC_X86_REG_RIP, ret)


def alloc_hook():
    n, al = mu.reg_read(X.UC_X86_REG_RDX), max(16, min(4096, mu.reg_read(X.UC_X86_REG_R8) or 16))
    heap[0] = (heap[0] + al - 1) & ~(al - 1); a = heap[0]; heap[0] += max(n, 1); ret_with(a)


HOOKS = {0x142C91990: lambda: ret_with(HEAP), 0x142C92910: alloc_hook, 0x142C93ED0: lambda: ret_with(0)}
for a in HOOKS:
    mu.hook_add(UC_HOOK_CODE, lambda mu, addr, size, _: HOOKS[addr](), begin=a, end=a)


def call(fn, args):
    rsp = (STACK + 0xF0000) & ~0xF; rsp -= 0x100
    mu.mem_write(rsp, struct.pack('<Q', SENTINEL)); mu.reg_write(X.UC_X86_REG_RSP, rsp)
    for r, v in zip((X.UC_X86_REG_RCX, X.UC_X86_REG_RDX, X.UC_X86_REG_R8, X.UC_X86_REG_R9), args):
        mu.reg_write(r, v)
    mu.emu_start(fn, SENTINEL, count=50_000_000)


def vec(a):
    return struct.unpack('<4f', bytes(mu.mem_read(a, 16)))


def u8(o, k):
    return D['mem'][o][k]


IN, OUT, NRM = SCR, SCR + 0x40, SCR + 0x80
for o, name in D['regions']:
    raw = D['mem'][o]
    cx = u8(o, 0x84) / (u8(o, 0x86) - u8(o, 0x88)); cy = u8(o, 0x85) / (u8(o, 0x87) - (u8(o, 0x88) >> 1))
    g = struct.unpack_from('<Q', raw, 0x60)[0]
    stride = struct.unpack_from('<i', D['mem'][g], 8)[0]
    hgrid = struct.unpack_from('<Q', raw, 0x1D0)[0]
    for p in json.load(open(sys.argv[3])):
        mu.mem_write(IN, struct.pack('<4f', *p, 1.0))
        call(0x141291DF0, (o, OUT, IN))
        lx, ly, lz, lw = vec(OUT)
        gx, gz = lx / cx, lz / cx
        i, j = math.floor(gx), math.floor(gz)
        hb = bytes(mu.mem_read(hgrid, 4 * stride * stride))
        H = lambda a, b: struct.unpack_from('<f', hb, 4 * (a * stride + b))[0] / cy
        fx, fz = gx - i, gz - j
        h = (H(i, j) * (1 - fx) * (1 - fz) + H(i + 1, j) * fx * (1 - fz) + H(i, j + 1) * (1 - fx) * fz + H(i + 1, j + 1) * fx * fz)
        mu.mem_write(IN, struct.pack('<4f', lx, h * cy, lz, 1.0)); mu.mem_write(NRM, struct.pack('<4f', 0, 1, 0, 0))
        call(0x141291310, (o, OUT, IN, NRM))
        w = vec(OUT)
        r = math.sqrt(w[0] ** 2 + w[1] ** 2 + w[2] ** 2)
        print(f'{name:8} point {[round(v, 1) for v in p]}: local ({lx:.2f}, {ly:.2f}, {lz:.2f}) grid ({gx:.2f}, {gz:.2f}) '
              f'cell h {h:.3f} -> world height {r - 130048:.3f}')
