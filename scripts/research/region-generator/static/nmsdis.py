import os
"""usage: python dis.py start end [start end ...]  (hex VAs) - static disassembly of NMS.exe"""
import sys, pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
p = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
pe = pefile.PE(p, fast_load=True)
md = Cs(CS_ARCH_X86, CS_MODE_64)
a = [int(x, 16) for x in sys.argv[1:]]
for start, end in zip(a[::2], a[1::2]):
    print(f'--- {start:X}..{end:X}')
    code = pe.get_data(start - pe.OPTIONAL_HEADER.ImageBase, end - start)
    for i in md.disasm(code, start):
        print(f'{i.address:X}: {i.mnemonic} {i.op_str}')
