import os
"""usage: python callers.py 0xTARGET [...] - direct E8/E9 rel32 call/jmp sites in NMS.exe .text, with the containing function start (.pdata)"""
import sys, bisect, struct, pefile, numpy as np
p = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
d = open(p, 'rb').read()
pe = pefile.PE(data=d, fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
s = next(x for x in pe.sections if x.Name.startswith(b'.text'))
t = d[s.PointerToRawData:s.PointerToRawData + s.SizeOfRawData]
va = base + s.VirtualAddress
pd = next(x for x in pe.sections if x.Name.startswith(b'.pdata'))
pdata = d[pd.PointerToRawData:pd.PointerToRawData + pd.SizeOfRawData]
starts = sorted(base + struct.unpack_from('<I', pdata, i)[0] for i in range(0, len(pdata) - 11, 12) if struct.unpack_from('<I', pdata, i)[0])
b = np.frombuffer(t, dtype=np.uint8).astype(np.int64)
disp = b[1:-3] | (b[2:-2] << 8) | (b[3:-1] << 16) | (b[4:] << 24)
disp = np.where(disp >= 1 << 31, disp - (1 << 32), disp)
op = b[:-4]
idx = np.arange(len(op), dtype=np.int64)
for target in [int(a, 16) for a in sys.argv[1:]]:
    hits = np.nonzero(((op == 0xE8) | (op == 0xE9)) & (va + idx + 5 + disp == target))[0]
    print(f'== {target:X}: {len(hits)} sites')
    for h in hits:
        site = va + int(h)
        f = starts[bisect.bisect_right(starts, site) - 1]
        print(f'  {site:X} {"call" if t[h] == 0xE8 else "jmp "} in function {f:X}')
