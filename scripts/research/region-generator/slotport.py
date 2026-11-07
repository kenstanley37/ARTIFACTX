# Port of the building slot table (docs/research/PLANET_BUILDING_PLACEMENT.md), checked against slot tables dumped
# from the running game (scripts/diagnostics/dump_planet_buildings.py output).
# usage: python slotport.py <planet_buildings.json> <PLANETBUILDINGTABLE densities json> <density level per UA ...>
#   density json: {"2": [d0..d7], ...} per class from RegionCheck buildtable (see make_density_json below)
# Model: class c owns slots 10c..10c+9; with k = density[c][level], slots j < floor(k) are (first type of c, 1.0, true);
# a fractional remainder gives one more slot (first type, frac, true); the rest are (first type, 1.0, false). A class
# with no type in the planet's list points at the next class's first type.
import json, sys, struct, math
def f32(x): return struct.unpack('<f', struct.pack('<f', x))[0]
def build(types_by_class, ntypes, density, level):
    slots = []
    for c in range(100):
        first = types_by_class.get(c)
        if first is None:
            later = [types_by_class[k] for k in types_by_class if k > c]
            first = min(later) if later else ntypes
        k = density.get(c, [0] * 8)[level] if c in types_by_class else 0.0
        whole = int(math.floor(k)); frac = f32(k - whole)
        for j in range(10):
            if j < whole: slots.append([first, 1.0, True])
            elif j == whole and frac > 0: slots.append([first, frac, True])
            else: slots.append([first, 1.0, False])
    return slots
if __name__ == '__main__':
    planets = json.load(open(sys.argv[1]))
    density = {int(k): v for k, v in json.load(open(sys.argv[2])).items()}
    levels = dict(a.split('=') for a in sys.argv[3:])
    for p in planets:
        if p['ua'] not in levels or not p['types']: continue
        first = {}
        for i, t in enumerate(p['types']): first.setdefault(t['classification'], i)
        ours = build(first, len(p['types']), density, int(levels[p['ua']]))
        live = p['slots']
        same = sum(1 for a, b in zip(ours, live) if a[0] == b[0] and a[2] == b[2] and abs(a[1] - b[1]) < 1e-6)
        diff = [(i, ours[i], live[i]) for i in range(len(live)) if not (ours[i][0] == live[i][0] and ours[i][2] == live[i][2] and abs(ours[i][1] - live[i][1]) < 1e-6)]
        print(f"{p['ua']}: {same}/{len(live)} slots identical")
        for d in diff[:12]: print('   slot', d)
