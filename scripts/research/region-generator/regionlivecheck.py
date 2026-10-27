# Compares the emulated region star count N with the running game's loaded region slots (live.json from liveregions.py).
# Slot words are (x, z, y) as signed values; galaxy given on the command line.
import os, sys, json
LIVE = os.path.abspath(sys.argv[1]); GALAXY = int(sys.argv[2])
sys.argv = [sys.argv[0], 'unused.hg.json']  # the shared header only needs a path
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'regionbatch.py')).read().split('\nroot = json.loads')[0]
exec(compile(src, 'regionbatch.py', 'exec'))
rows = json.loads(open(LIVE, encoding='utf-8-sig').read().splitlines()[1])
exact = diff = 0
for r in rows:
    x, z, y = r['words']
    emitted = region_stars(GALAXY, x & 0xFFF, y & 0xFF, z & 0xFFF)
    n = sum(1 for i in emitted if i < 1000)
    ok = n == r['n'] and len(emitted) == r['total']
    exact += ok; diff += not ok
    if not ok: print(f'DIFF region x={x} y={y} z={z}: game N={r["n"]} total={r["total"]}  emulator N={n} total={len(emitted)}', flush=True)
print(f'{len(rows)} live regions: {exact} exact, {diff} different', flush=True)
