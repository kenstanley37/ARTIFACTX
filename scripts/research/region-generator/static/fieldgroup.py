import os
"""usage: python fieldgroup.py 0xOFF1 0xOFF2 ... - functions in NMS.exe (.pdata bounds) that read every given field offset
from one base register, e.g. a struct's fields used together. Slow: disassembles all of .text. Prints function, register
and the matching instructions."""
import sys, re, pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
pe = pefile.PE(os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe"))
base = pe.OPTIONAL_HEADER.ImageBase
want = [int(x, 16) for x in sys.argv[1:]]
text = next(s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text')
data = text.get_data()
tbase = base + text.VirtualAddress
md = Cs(CS_ARCH_X86, CS_MODE_64)
pat = re.compile(r'\[(r\w+) \+ (0x[0-9a-f]+)\]')
funcs = sorted({(base + e.struct.BeginAddress, base + e.struct.EndAddress) for e in pe.DIRECTORY_ENTRY_EXCEPTION})
hits = 0
for begin, end in funcs:
    if not (tbase <= begin < tbase + len(data)): continue
    seen = {}
    for insn in md.disasm(data[begin - tbase:end - tbase], begin):
        for reg, off in pat.findall(insn.op_str):
            off = int(off, 16)
            if off in want and reg not in ('rsp', 'rbp', 'rip'):
                seen.setdefault(reg, {}).setdefault(off, []).append(f'{insn.address:X}: {insn.mnemonic} {insn.op_str}')
    for reg, offs in seen.items():
        if all(o in offs for o in want):
            hits += 1
            print(f'== {begin:X}-{end:X} via {reg}')
            for o in want: print('   ', offs[o][0])
            sys.stdout.flush()
print(hits, 'functions')
