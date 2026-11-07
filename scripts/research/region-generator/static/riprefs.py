import os
"""Find every rip-relative instruction in NMS.exe .text that references given addresses.
usage: python riprefs.py 0x146E8D714 [more targets...]"""
import sys, pefile, numpy as np
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
p = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
d = open(p, 'rb').read()
pe = pefile.PE(data=d, fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
s = next(x for x in pe.sections if x.Name.startswith(b'.text'))
t = d[s.PointerToRawData:s.PointerToRawData + s.SizeOfRawData]
va = base + s.VirtualAddress
b = np.frombuffer(t, dtype=np.uint8).astype(np.int64)
disp = (b[:-3] | (b[1:-2] << 8) | (b[2:-1] << 16) | (b[3:] << 24))
disp = np.where(disp >= 1 << 31, disp - (1 << 32), disp)
idx = np.arange(len(disp), dtype=np.int64)
md = Cs(CS_ARCH_X86, CS_MODE_64); md.detail = False
for target in [int(a, 16) for a in sys.argv[1:]]:
    print(f'== {target:X}')
    seen = set()
    for imm in (0, 1, 2, 4):
        for i in np.nonzero(va + idx + 4 + imm + disp == target)[0]:
            for back in range(2, 8):
                start = int(i) - back
                ins = next(md.disasm(t[start:start + 16], va + start), None)
                if ins and ins.size == back + 4 + imm and 'rip' in ins.op_str and ins.address not in seen:
                    seen.add(ins.address)
                    print(f'{ins.address:X}: {ins.mnemonic} {ins.op_str}')
                    break
