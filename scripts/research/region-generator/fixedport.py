# Step 2: the region MWC state after the 122 fixed stars, ported and diffed against the emulator.
import os, sys, json, random, math
SCR = os.path.dirname(os.path.abspath(__file__))
live = json.loads(open(os.path.join(SCR, 'live.json'), encoding='utf-8-sig').read().splitlines()[1])
regions = [(0, r['words'][0] & 0xFFF, r['words'][2] & 0xFF, r['words'][1] & 0xFFF) for r in live[:40]]
rng = random.Random(27)
regions += [(rng.randrange(256), rng.randrange(4096), rng.randrange(256), rng.randrange(4096)) for _ in range(19)] + [(0, 0, 0, 0)]
sys.argv = [sys.argv[0], '0', '0', '0', '0']
exec(compile(open(os.path.join(SCR, 'segport.py')).read().split('\nif len(sys.argv) > 1')[0], 'segport.py', 'exec'))

state_at = {}
def grab(name):
    def hook(mu, addr, size, _):
        if name not in state_at: state_at[name] = rd(mu.reg_read(X.UC_X86_REG_RSP) + 0x30, '<II')
    return hook
E.mu.hook_add(UC_HOOK_CODE, grab('after_fixed'), begin=0x141368267, end=0x141368267)

def f(x): return f32(x)
def after_fixed_state(g, x, y, z, fixed_total=122, near=5.0, far=10.0):
    lo = (g << 32 | y << 24 | z << 12 | x) & M32
    s = Stream(lo if lo else 1, ((lo >> 16) | (lo << 16)) & M32 ^ g ^ lo)   # hi = galaxy byte
    s.word()                                                                    # setup draw
    _, _, segs = build(g, x, y, z)
    placed = 0
    for a, b in segs:
        d = [f(b[k] - a[k]) for k in range(3)]
        sq = [f(c * c) for c in d]
        length = f(math.sqrt(f(sq[2] + f(sq[1] + sq[0]))))
        if length == 0: continue
        step_min = f(1.0 / f(length / near)); step_max = f(1.0 / f(length / far))
        if 1.0 < step_min: continue
        span = f(step_max - step_min); t = step_min
        while True:
            for _ in range(5): s.word()       # 0x14136A020: always five draws
            placed += 1
            t = f(t + f(f(unit(s.word()) * span) + step_min))
            if not (1.0 >= t): break
    for _ in range(max(0, fixed_total - placed)):
        for _ in range(7): s.word()
    return (s.lo, s.carry), placed

ok = 0
for g, x, y, z in regions:
    state_at.clear()
    region_stars(g, x, y, z)
    mine, placed = after_fixed_state(g, x, y, z)
    same = mine == state_at.get('after_fixed')
    ok += same
    if not same: print('DIFF', g, hex(x), hex(y), hex(z), 'loop1 stars', placed, flush=True)
print(f'after-fixed RNG state: {ok}/{len(regions)} identical', flush=True)
