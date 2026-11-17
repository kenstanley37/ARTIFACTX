# READ-ONLY (access 0x410): finds every copy of a 64-bit value in the running game's writable memory and prints the
# surrounding 0x100 bytes as qwords, marking pointers into NMS.exe (vtables) as image VAs (base 0x140000000).
# usage: python findseed.py 0xVALUE [out.json]
import sys, os, struct, json, ctypes, ctypes.wintypes as W
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mem import *
class MBI(ctypes.Structure):
    _fields_ = [('BaseAddress', ctypes.c_void_p), ('AllocationBase', ctypes.c_void_p), ('AllocationProtect', W.DWORD),
                ('PartitionId', W.WORD), ('RegionSize', ctypes.c_size_t), ('State', W.DWORD), ('Protect', W.DWORD), ('Type', W.DWORD)]
k32.VirtualQueryEx.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.POINTER(MBI), ctypes.c_size_t]
val = int(sys.argv[1], 16); pat = struct.pack('<Q', val)
image_end = base + 0x8000000
hits = []
addr, mbi = 0, MBI()
while k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    start, size = mbi.BaseAddress or 0, mbi.RegionSize
    if mbi.State == 0x1000 and mbi.Protect in (0x04, 0x40, 0x08) and size < (1 << 32):
        for off in range(0, size, 1 << 24):
            c = read(start + off, min(1 << 24, size - off))
            i = c.find(pat)
            while i >= 0:
                hits.append(start + off + i); i = c.find(pat, i + 1)
    addr = start + size
print(len(hits), 'hits')
out = []
for a in hits:
    lo = a - 0x80
    blob = read(lo, 0x100)
    q = struct.unpack(f'<{len(blob) // 8}Q', blob[:len(blob) // 8 * 8])
    marks = []
    for k, v in enumerate(q):
        if base <= v < image_end: marks.append(f'{lo + 8 * k - a:+#x}: image {v - base + 0x140000000:#x}')
    print(f'{a:#x} (in module: {base <= a < image_end})'); print('   ', '; '.join(marks[:8]))
    print('    qwords around:', ' '.join(f'{x:016x}' for x in q[12:20]))
    out.append({'addr': a, 'qwords': list(q), 'lo': lo})
if len(sys.argv) > 2: json.dump(out, open(sys.argv[2], 'w'))
