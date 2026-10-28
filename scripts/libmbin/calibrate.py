"""Learns how NMS.exe field type codes map to libMBIN C# types, from classes whose GUID is unchanged."""
import sys, struct; sys.path.insert(0, '.')
from nmsmeta import *; import libsrc
from collections import defaultdict, Counter
img = Image(); cs = classes(img); lib = libsrc.load()
byva = {c['va']: n for n, c in cs.items()}
combos = defaultdict(Counter); nofields = 0
for n, c in cs.items():
    if n not in lib or lib[n]['guid'] != c['guid']: continue
    fl = fields(img, c)
    if fl is None: nofields += 1; continue
    lf = {f['name'].lower(): f for f in lib[n]['fields']}
    for i, f in enumerate(fl):
        w = struct.unpack('<22I', f['raw'])
        t, inner, esize, count = w[3], w[4], w[5], w[6]
        cls = byva.get(w[8] | (w[9] << 32))
        l = lf.get(f['name'].replace(' ', '').lower()) if f['name'] else None
        if not l: combos[(t, inner)]['<missing>'] += 1; continue
        ltype = l['type'].replace(cls, 'CLS') if cls else l['type']
        combos[(t, inner)][f"{ltype} sz={esize:X}"] += 1
print('unchanged classes without a stored field list:', nofields)
for k in sorted(combos):
    print(f'type {k[0]:2X} inner {k[1]:2X}:', dict(combos[k].most_common(5)))
