# usage: python static/vtremap.py static <old export dir> 0xVTABLE [...]
# Remaps 184271 vtables: old entries -> new functions (remap.py match), then finds that pointer list in the new .rdata.
import sys, os, struct, re
here = sys.argv[1]; old_dir = sys.argv[2]; vts = [int(x, 16) for x in sys.argv[3:]]
sys.argv = ['remap.py', old_dir]
exec(open(os.path.join(here, 'remap.py')).read().split('for arg in sys.argv[2:]')[0])
rsec = next(x for x in pe.sections if x.Name.startswith(b'.rdata'))
rdata = data[rsec.PointerToRawData:rsec.PointerToRawData + rsec.SizeOfRawData]; rva = pe.OPTIONAL_HEADER.ImageBase + rsec.VirtualAddress
def find_new(va):
    for n in (32, 64, 128, 256):
        pat, _ = masked(va, n); hits = [m.start() for m in re.finditer(pat, text, re.S)]
        if len(hits) == 1: return text_va + hits[0]
        if not hits: return None
    return None
for vt in vts:
    ents = struct.unpack('<6Q', old_bytes(vt, 48))
    new = [find_new(e) for e in ents]
    print(hex(vt), 'entries', [hex(e) for e in ents], '->', [hex(e) if e else None for e in new])
    if any(e is None for e in new[:4]): print('  unmatched entries'); continue
    k = len([e for e in new if e]); seq = b''.join(struct.pack('<Q', e) for e in new[:k])
    hits = [m.start() for m in re.finditer(re.escape(seq), rdata)]
    print('  new vtable', [hex(rva + h) for h in hits])
