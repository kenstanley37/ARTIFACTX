import os
"""usage: python importof.py 0xTHUNK [...] - names the imported function a `jmp qword ptr [rip+X]` thunk in NMS.exe calls"""
import sys, pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
pe = pefile.PE(os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe"))
base = pe.OPTIONAL_HEADER.ImageBase
slots = {imp.address: f"{entry.dll.decode()}!{(imp.name or b'#%d' % imp.ordinal).decode()}"
         for entry in pe.DIRECTORY_ENTRY_IMPORT for imp in entry.imports}
md = Cs(CS_ARCH_X86, CS_MODE_64)
for a in (int(x, 16) for x in sys.argv[1:]):
    insn = next(md.disasm(pe.get_data(a - base, 15), a))
    if insn.mnemonic != 'jmp' or 'rip' not in insn.op_str:
        print(f'{a:X}: not a thunk ({insn.mnemonic} {insn.op_str})'); continue
    slot = a + insn.size + int(insn.op_str.split('rip + ')[1].rstrip(']'), 16)
    print(f'{a:X}: {slots.get(slot, f"slot {slot:X} (not an import)")}')
