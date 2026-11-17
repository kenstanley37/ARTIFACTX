# Finds build-184271 code addresses in the installed NMS.exe: takes the old bytes at each address (from the Ghidra
# export), masks everything that moves between builds (rel32 call/jmp targets, RIP-relative displacements), and
# searches the new .text for a unique match, growing the pattern until it is unique.
# usage: python remap.py <old export dir> 0xADDR [...]
import sys, re, os, pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone import x86_const as CX

old_dir = sys.argv[1]
blocks = [(int(a, 16), int(n), f) for a, n, f in (l.split() for l in open(os.path.join(old_dir, 'blocks.txt')))]
def old_bytes(va, n):
    for s, size, f in blocks:
        if s <= va < s + size:
            with open(os.path.join(old_dir, f), 'rb') as fh: fh.seek(va - s); return fh.read(n)
    raise KeyError(hex(va))

EXE = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
data = open(EXE, 'rb').read()
pe = pefile.PE(data=data, fast_load=True)
sec = next(x for x in pe.sections if x.Name.startswith(b'.text'))
text = data[sec.PointerToRawData:sec.PointerToRawData + sec.SizeOfRawData]
text_va = pe.OPTIONAL_HEADER.ImageBase + sec.VirtualAddress

md = Cs(CS_ARCH_X86, CS_MODE_64); md.detail = True
import struct
def rip_disp(ins):
    """Byte positions of a RIP-relative instruction's 32-bit displacement."""
    if ins.disp_offset: return range(ins.disp_offset, ins.disp_offset + 4)
    k = bytes(ins.bytes).rfind(struct.pack('<i', ins.disp))
    return range(k, k + 4) if k >= 0 else range(0)
def masked(va, n):
    """Regex for n+ bytes of old code at va, with moving fields as wildcards; returns (regex bytes, length)."""
    code = old_bytes(va, n + 32)
    parts, length = [], 0
    for ins in md.disasm(code, va):
        b = bytearray(ins.bytes); mask = [False] * len(b)
        rel = ins.group(CX.X86_GRP_CALL) or ins.group(CX.X86_GRP_JUMP)
        if rel and ins.size >= 5 and ins.operands and ins.operands[0].type == CX.X86_OP_IMM and ins.bytes[0] in (0xE8, 0xE9, 0x0F):
            for i in range(len(b) - 4, len(b)): mask[i] = True     # rel32 target
        elif 'rip' in ins.op_str:                                    # RIP-relative operand (incl. SSE loads)
            for i in rip_disp(ins): mask[i] = True
        parts.append(b''.join(b'.' if m else re.escape(bytes([c])) for c, m in zip(b, mask)))
        length += ins.size
        if length >= n: break
    return b''.join(parts), length

for arg in sys.argv[2:]:
    va = int(arg, 16); found = None
    for n in (32, 64, 128, 256, 512, 1024):
        pat, length = masked(va, n)
        hits = [m.start() for m in re.finditer(pat, text, re.S)]
        if len(hits) == 1: found = (text_va + hits[0], length); break
        if not hits: found = ('NO MATCH', length); break
    if found is None: found = (f'{len(hits)} matches', length)
    print(arg, '->', hex(found[0]) if isinstance(found[0], int) else found[0], f'(pattern {found[1]} bytes)')

# --- verify: whole function body (Ghidra size) masked-equal at the new address
if os.environ.get('VERIFY'):
    sizes = {int(a, 16): int(n) for a, n in (l.split() for l in open(os.path.join(old_dir, 'functions.txt')) if not l.startswith('ff'))}
    for pair in os.environ['VERIFY'].split(','):
        o, n_ = (int(x, 16) for x in pair.split(':'))
        size = sizes.get(o, 0)
        pat, length = masked(o, size)
        off = n_ - text_va
        ok = re.match(pat, text[off:off + length + 64], re.S) is not None
        print(f'{o:#x} -> {n_:#x}: body {length} bytes', 'IDENTICAL (masked)' if ok else 'DIFFERS')
