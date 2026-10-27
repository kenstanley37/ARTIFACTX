import os
import struct, pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
pe = pefile.PE(os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe"), fast_load=True)
B = pe.OPTIONAL_HEADER.ImageBase
def data(a, n): return pe.get_data(a - B, n)
md = Cs(CS_ARCH_X86, CS_MODE_64)
code = data(0x14136A930, 0x14136B2C4 - 0x14136A930)
import re
for i in md.disasm(code, 0x14136A930):
    m = re.search(r'\[rip ([+-]) (0x[0-9a-f]+)\]', i.op_str)
    if m:
        t = i.address + i.size + (1 if m.group(1) == '+' else -1) * int(m.group(2), 16)
        raw = data(t, 16)
        print(f'{i.address:X} {i.mnemonic} {i.op_str.split(",")[0]} <- {t:X}: f={struct.unpack("<4f", raw)} q={struct.unpack("<d", raw[:8])[0]}')
for table in (0x14136B2C4, 0x14136B2DC):
    offs = struct.unpack('<6i', data(table, 24))
    print(f'jump table {table:X}:', [hex(B + o) for o in offs])
