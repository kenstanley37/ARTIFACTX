import os
"""usage: python riploads.py 0xSTART 0xEND - every rip-relative/image-relative data reference in a range, with section and whether the file holds data there"""
import sys, re, pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
pe = pefile.PE(os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe"), fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
start, end = int(sys.argv[1], 16), int(sys.argv[2], 16)
md = Cs(CS_ARCH_X86, CS_MODE_64)
seen = set()
for i in md.disasm(pe.get_data(start - base, end - start), start):
    m = re.search(r'\[rip ([+-]) (0x[0-9a-f]+)\]', i.op_str)
    targets = []
    if m: targets.append(i.address + i.size + (1 if m.group(1) == '+' else -1) * int(m.group(2), 16))
    m2 = re.search(r'\[r\w+ \+ r\w+\*\d \+ (0x[0-9a-f]{6,})\]|\[r\w+ \+ (0x5[0-9a-f]{6})\]', i.op_str)
    if m2: targets.append(base + int(m2.group(1) or m2.group(2), 16))
    for t in targets:
        if t in seen: continue
        seen.add(t)
        rva = t - base; sec = pe.get_section_by_rva(rva)
        if sec is None: print(f'{i.address:X} -> {t:X} (no section)'); continue
        inraw = rva - sec.VirtualAddress < sec.SizeOfRawData
        data = pe.get_data(rva, 8) if inraw else None
        flag = 'BSS' if not inraw else ('ZERO' if data == bytes(8) else '')
        print(f'{i.address:X} -> {t:X} {sec.Name.rstrip(chr(0).encode()).decode():7} {data.hex() if data else "-":16} {flag}')
