import os
"""usage: python dispscan.py 0xDISP [mnemonic-substring] - every instruction in NMS.exe .text whose memory operand uses
[reg + DISP] (32-bit displacement), e.g. a struct field offset. Finds candidates by bytes, confirms with capstone."""
import sys, pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
p = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
pe = pefile.PE(p, fast_load=True)
text = next(s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text')
base = pe.OPTIONAL_HEADER.ImageBase + text.VirtualAddress
data = text.get_data()
disp = int(sys.argv[1], 16)
want = sys.argv[2] if len(sys.argv) > 2 else ''
needle = disp.to_bytes(4, 'little')
md = Cs(CS_ARCH_X86, CS_MODE_64)
md.detail = True
seen = set()
i = data.find(needle)
while i >= 0:
    # The displacement sits 2..8 bytes into an instruction: try decoding from each possible start.
    for back in range(2, 10):
        start = i - back
        if start < 0: continue
        insn = next(md.disasm(data[start:start + 15], base + start), None)
        if insn is None or insn.size < back + 4: continue
        if hex(disp) in insn.op_str and '[' in insn.op_str and 'rip' not in insn.op_str and want in insn.mnemonic:
            if insn.address not in seen:
                seen.add(insn.address)
                print(f'{insn.address:X}: {insn.mnemonic} {insn.op_str}')
            break
    i = data.find(needle, i + 1)
