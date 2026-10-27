import os
"""usage: python rangerefs.py 0xLO 0xHI - rip-relative instructions in NMS.exe .text whose target falls in [LO, HI)"""
import sys, pefile, numpy as np
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
p = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
d = open(p, 'rb').read()
pe = pefile.PE(data=d, fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
s = next(x for x in pe.sections if x.Name.startswith(b'.text'))
t = d[s.PointerToRawData:s.PointerToRawData + s.SizeOfRawData]
va = base + s.VirtualAddress
lo, hi = int(sys.argv[1], 16), int(sys.argv[2], 16)
b = np.frombuffer(t, dtype=np.uint8).astype(np.int64)
disp = (b[:-3] | (b[1:-2] << 8) | (b[2:-1] << 16) | (b[3:] << 24))
disp = np.where(disp >= 1 << 31, disp - (1 << 32), disp)
idx = np.arange(len(disp), dtype=np.int64)
md = Cs(CS_ARCH_X86, CS_MODE_64)
seen = set()
for imm in (0, 1, 2, 4):
    tgt = va + idx + 4 + imm + disp
    for i in np.nonzero((tgt >= lo) & (tgt < hi))[0]:
        for back in range(2, 8):
            start = int(i) - back
            ins = next(md.disasm(t[start:start + 16], va + start), None)
            if ins and ins.size == back + 4 + imm and 'rip' in ins.op_str and ins.address not in seen:
                seen.add(ins.address)
                print(f'{ins.address:X}: {ins.mnemonic} {ins.op_str}   -> {int(tgt[i]):X}')
                break
