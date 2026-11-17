# Maps installed-build (185034) code addresses back to build 184271: masked bytes from the new NMS.exe searched in the
# 184271 export (static/ExportBlocks.java output). usage: python backmap.py <old export dir> 0xNEW [...]
import sys, os, re, struct, pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
old_dir = sys.argv[1]
blocks = [(int(a, 16), int(n), f) for a, n, f in (l.split() for l in open(os.path.join(old_dir, 'blocks.txt')))]
s, size, f = next(b for b in blocks if b[2].startswith('_text'))
old_text = open(os.path.join(old_dir, f), 'rb').read()
EXE = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
pe = pefile.PE(EXE, fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
md = Cs(CS_ARCH_X86, CS_MODE_64); md.detail = True
def rip_disp(ins):
    if ins.disp_offset: return range(ins.disp_offset, ins.disp_offset + 4)
    k = bytes(ins.bytes).rfind(struct.pack('<i', ins.disp)); return range(k, k + 4) if k >= 0 else range(0)
def masked(va, n):
    parts, length = [], 0
    for ins in md.disasm(pe.get_data(va - base, n + 32), va):
        b = bytearray(ins.bytes); mask = [False] * len(b)
        if ins.bytes[0] in (0xE8, 0xE9) and ins.size == 5: mask[1:] = [True] * 4
        elif ins.bytes[0] == 0x0F and ins.size == 6 and 0x80 <= ins.bytes[1] <= 0x8F: mask[2:] = [True] * 4
        elif 'rip' in ins.op_str:
            for i in rip_disp(ins): mask[i] = True
        parts.append(b''.join(b'.' if m else re.escape(bytes([c])) for c, m in zip(b, mask)))
        length += ins.size
        if length >= n: break
    return b''.join(parts)
for arg in sys.argv[2:]:
    va = int(arg, 16); res = None
    for back in (0x20, 0x40, 0x60):
        for n in (48, 96, 160):
            hits = [m.start() for m in re.finditer(masked(va - back, n), old_text, re.S)]
            if len(hits) == 1: res = s + hits[0] + back; break
        if res: break
    print(arg, '->', hex(res) if res else 'not found')
