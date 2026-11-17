# Walks old and new functions instruction by instruction and prints every pair that differs once moving fields
# (rel32 targets, RIP displacements) are ignored. usage: python insdiff.py <old export dir> OLD:NEW[,OLD:NEW...] [maxbytes]
import sys, os
sys.argv = [sys.argv[0], sys.argv[1]] + sys.argv[2:]
pairs, limit = sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 6000
sys.argv = sys.argv[:2]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'remap.py')).read().split('for arg in sys.argv[2:]')[0])
def norm(ins):
    b = bytearray(ins.bytes)
    if ins.bytes[0] in (0xE8, 0xE9) and ins.size == 5: b[1:] = b'\0\0\0\0'
    elif ins.bytes[0] == 0x0F and ins.size == 6 and 0x80 <= ins.bytes[1] <= 0x8F: b[2:] = b'\0\0\0\0'
    elif 'rip' in ins.op_str:
        for k in rip_disp(ins): b[k] = 0
    return bytes(b)
for pair in pairs.split(','):
    o, n = (int(x, 16) for x in pair.split(':'))
    oi = list(md.disasm(old_bytes(o, limit), o)); off = n - text_va
    ni = list(md.disasm(text[off:off + limit], n))
    print(f'== {o:#x} -> {n:#x}')
    shown = 0
    for a, b in zip(oi, ni):
        if norm(a) != norm(b) or a.size != b.size:
            print(f'  {a.address:#x}: {a.mnemonic} {a.op_str:40s} | {b.address:#x}: {b.mnemonic} {b.op_str}')
            shown += 1
            if a.size != b.size or a.mnemonic != b.mnemonic: print('  (instruction stream diverges here)'); break
            if shown > 25: break
        if a.mnemonic in ('int3',) and b.mnemonic in ('int3',): break
