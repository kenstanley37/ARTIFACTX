# READ-ONLY check of the building-choice rule against the running game (docs/research/PLANET_BUILDING_PLACEMENT.md).
# usage: python buildingcheck_live.py <building data block hex> <radius> <depot key hex> [more keys...]
# Reads the planet's slot table (1000 x 12 bytes) and building types (stride 0xE0, classification byte +0xD3), then
# for every live site record (key +0x00, points from +0x10) predicts building / no building with: MWC seeded from the
# key and stepped once; slot = (int)(Next() * 999); built when slot.HasBuilding and slot.Probability > Next() and slot.Index != -1.
# Compares with whether the record holds building points after point A. Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, struct, subprocess, sys
import numpy as np
from collections import Counter
from siteport import feature, key as site_key
FACE = {(2, -1): 0, (2, 1): 1, (0, 1): 2, (0, -1): 3, (1, -1): 4, (1, 1): 5}
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
block, radius = int(sys.argv[1], 16), float(sys.argv[2])
half = None
head = read(block, 0x30)
bptr, bn = struct.unpack_from('<QI', head, 0); sptr, sn = struct.unpack_from('<QI', head, 0x10)
slots = [struct.unpack_from('<if?', read(sptr, 12 * sn), 12 * i) for i in range(sn)]
types = read(bptr, 0xE0 * bn)
cls = [types[0xE0 * i + 0xD3] for i in range(bn)]
M = 0xFFFFFFFF
class Mwc:
    def __init__(self, seed):
        lo, hi = seed & M, seed >> 32
        self.lo = lo + (lo == 0); self.hi = (((lo >> 16) | (lo << 16)) & M) ^ hi ^ lo
    def word(self):
        t = self.lo * 0x5A76F899 + self.hi; self.lo, self.hi = t & M, t >> 32; return self.lo
def unit(w): return struct.unpack('<f', struct.pack('<f', w * 2.3283064370807974e-10))[0]
def f32(x): return struct.unpack('<f', struct.pack('<f', x))[0]
def choose(key):
    r = Mwc(key)
    r.word()   # the caller (0x14128EB5B) steps the key-seeded MWC once before storing it
    idx = int(f32(f32(unit(r.word()) + 0.0) * 999.0))
    index, prob, has = slots[idx]
    if not has: return None
    if not (prob > f32(unit(r.word()) + 0.0)): return None
    return None if index == -1 else index
for text in sys.argv[3:]:
    i = choose(int(text, 16)); print(f'key {text}: building {i} class {cls[i] if i is not None else None}')
records = {}
addr, mbi = 0, MBI()
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    if mbi.State == 0x1000 and mbi.Protect == 0x04:
        for off in range(0, size, 1 << 24):
            chunk = read(start + off, min(1 << 24, size - off))
            n = len(chunk) // 16
            if n < 3: continue
            v = np.frombuffer(chunk[:n * 16], dtype='<f4').reshape(n, 4)
            with np.errstate(all='ignore'):
                r = np.sqrt((v[:, :3].astype(np.float64) ** 2).sum(axis=1))
            for i in np.nonzero((v[:, 3] == 1.0) & (np.abs(r - radius) < 1.0))[0]:
                if i == 0 or i + 1 >= n: continue
                k = struct.unpack_from('<Q', chunk, 16 * (int(i) - 1))[0]
                if k < (1 << 48): continue
                second = v[i + 1]
                built = second[3] == 1.0 and not (second[0] == 0 and second[1] == 0 and second[2] == 0)
                packed = struct.unpack_from('<I', chunk, 16 * (int(i) - 1) + 12)[0]
                records[k] = (built, tuple(float(c) for c in v[i, :3]), packed)
    addr = start + size
    if addr >= 0x7FFFFFFFFFFF: break
head2 = read(block + 0x38, 0x14)
divisions = int(struct.unpack_from('<f', head2, 8)[0]); half = divisions // 2
sector = struct.unpack_from('<I', head2, 0x10)[0]
agree, genuine = Counter(), 0
samples = Counter()
for k, (built, p, packed) in records.items():
    ax = max(range(3), key=lambda i: abs(p[i])); sign = 1 if p[ax] > 0 else -1
    q = [c / abs(p[ax]) * half for c in p]; cell = [round(c) for c in q]; cell[ax] = sign * half
    if site_key(feature(FACE[(ax, sign)], *cell), sector) != k: continue   # not a genuine site record
    genuine += 1
    pred = choose(k)
    agree[(bool(built), pred is not None)] += 1
    samples[(FACE[(ax, sign)], tuple(cell))] = 1
# Cells with no record inside the area where records exist (per face, between the min and max cell of the records,
# only where the 3x3 neighbourhood has a record): a correct rule should call them empty.
present = set(samples)
empty_ok = empty_bad = 0
for (face, cell) in list(present):
    ax = next(i for i in range(3) if abs(cell[i]) == half)
    others = [i for i in range(3) if i != ax]
    for da in (-1, 0, 1):
        for db in (-1, 0, 1):
            c = list(cell); c[others[0]] += da; c[others[1]] += db
            if (face, tuple(c)) in present or abs(c[others[0]]) > half or abs(c[others[1]]) > half: continue
            pred = choose(site_key(feature(face, *c), sector))
            if pred is None: empty_ok += 1
            else: empty_bad += 1
import random
rnd = random.Random(1); base = 0
for _ in range(20000):
    face = rnd.randrange(6); a, b = rnd.randrange(-half, half), rnd.randrange(-half, half)
    axis = {0: 2, 1: 2, 2: 0, 3: 0, 4: 1, 5: 1}[face]; sign = -1 if face in (0, 3, 4) else 1
    c = [a, b]; c.insert(axis, sign * half)
    if choose(site_key(feature(face, *c), sector)) is not None: base += 1
print(f'random cells predicted built: {base / 200:.1f}%')
print(f'neighbouring cells with no record: predicted empty {empty_ok}, predicted built {empty_bad}')
print(f'{genuine} genuine site records: (has parts, predicted building) -> count: {dict(agree)}')
