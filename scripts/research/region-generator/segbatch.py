# Diffs the ported segment builder against the emulator for many regions.
import os, sys, json, random
SCR = os.path.dirname(os.path.abspath(__file__))
live = json.loads(open(os.path.join(SCR, 'live.json'), encoding='utf-8-sig').read().splitlines()[1])
regions = [(0, r['words'][0] & 0xFFF, r['words'][2] & 0xFF, r['words'][1] & 0xFFF) for r in live[:40]]
rng = random.Random(27)
regions += [(rng.randrange(256), rng.randrange(4096), rng.randrange(256), rng.randrange(4096)) for _ in range(19)] + [(0, 0, 0, 0)]
sys.argv = [sys.argv[0], '0', '0', '0', '0']
src = open(os.path.join(SCR, 'segport.py')).read().split('\nif len(sys.argv) > 1')[0]
exec(compile(src, 'segport.py', 'exec'))
ok = 0
for g, x, y, z in regions:
    galaxy, X_, Y_, Z_ = g, x, y, z
    _, _, port = build(g, x, y, z)
    emu = [(tuple(a[:3]), tuple(b[:3])) for a, b in emulated_segments()]
    ok += emu == port
    if emu != port: print('DIFF', g, hex(x), hex(y), hex(z), len(emu), len(port), flush=True)
print(f'segment lists: {ok}/{len(regions)} identical', flush=True)
