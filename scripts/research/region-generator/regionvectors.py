# Emulator reference counts for varied regions (random galaxies and coordinates, negative
# values, and regions around the galactic-centre goal gap), as JSON for the C# check.
# usage: python regionvectors.py <out.json>
import os, sys, json, random
SCR = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(sys.argv[1])
sys.argv = [sys.argv[0], 'unused.hg.json']
exec(compile(open(os.path.join(SCR, 'regionbatch.py')).read().split('\nroot = json.loads')[0], 'regionbatch.py', 'exec'))
rng = random.Random(1027)
regions = [(rng.randrange(256), rng.randrange(4096), rng.randrange(256), rng.randrange(4096)) for _ in range(34)]
# Gap edge: integer distance around 8 from the centre, both signs.
for x, y, z in [(7, 0, 0), (8, 0, 0), (0xFF8, 0, 0), (0, 0xF9, 0), (0, 0, 0xFF8), (5, 5, 5), (4, 4, 4), (6, 3, 2),
                (0xFFB, 0xFC, 0xFFB), (3, 0, 0), (0, 8, 0), (0, 0, 9), (0xFFF, 0xFF, 0xFFF), (0x7FF, 0x7F, 0x7FF), (0x800, 0x80, 0x800), (1, 2, 3)]:
    regions.append((rng.randrange(256), x, y, z))
rows = []
for g, x, y, z in regions:
    emitted = region_stars(g, x, y, z)
    rows.append(dict(galaxy=g, x=x, y=y, z=z, n=sum(1 for i in emitted if i < 1000), total=len(emitted)))
    print(rows[-1], flush=True)
json.dump(rows, open(OUT, 'w'))
