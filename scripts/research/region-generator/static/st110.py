# Lists 64-bit stores to [reg+0x110] in a range of the installed NMS.exe, with the previous writes of the source register.
# usage: python st110.py 0xSTART 0xEND
import sys, os, pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone import x86_const as CX
EXE = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
pe = pefile.PE(EXE, fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
lo, hi = int(sys.argv[1], 16), int(sys.argv[2], 16)
md = Cs(CS_ARCH_X86, CS_MODE_64); md.detail = True
code = pe.get_data(lo - base, hi - lo)
window = []
off = 0
while off < len(code):
    got = False
    for ins in md.disasm(code[off:], lo + off):
        got = True
        window.append(ins); window = window[-24:]
        if ins.mnemonic == 'mov' and len(ins.operands) == 2 and ins.operands[0].type == CX.X86_OP_MEM and ins.operands[0].mem.disp == 0x110 \
           and ins.operands[0].size == 8 and ins.operands[1].type == CX.X86_OP_REG:
            src = ins.reg_name(ins.operands[1].reg)
            prev = [w for w in window[:-1] if w.op_str.split(',')[0].strip() == src or w.mnemonic == 'call'][-3:]
            print(f'{ins.address:#x}: {ins.mnemonic} {ins.op_str}   <- ' + '; '.join(f'{w.address:#x} {w.mnemonic} {w.op_str}' for w in prev))
        off = ins.address + ins.size - lo
    if not got: off += 1
