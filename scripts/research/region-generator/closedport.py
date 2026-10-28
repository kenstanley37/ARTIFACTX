# Override placement with the game closed: planet inputs from Core (RegionCheck "places" -> JSON), terrain data from
# terrainport, the record list from the table; checked against the live records in the planet bundles.
# usage: python closedport.py <places.json> <override_table.json> <voxelgeneratorsettings.mbin> <bundle folder>
import sys, json, math, struct, os
from uberport import f
import terrainport as tp
from placeport import Planet, override_list, place

class Inputs:
    """The fields placeport reads from a planet, built from Core's inputs instead of a live bundle."""
    def __init__(self, p, vox):
        self.seed = int(p['seed'], 16)
        setting = vox[0x20 + p['setting'] * 0x22A0:0x20 + (p['setting'] + 1) * 0x22A0]
        self.terrain = tp.terrain(self.seed, setting, p['noiseChances'], p['rock'], p['substance'], p['sea'], p['seaStandard'],
                                  p['size'] == 3, p['deadToWeird'], p['drawsBeforeLayers'])
        self.radius = p['radius']; self.divisions = p['divisions']; self.sector = p['sectorSeed']
        self.bd4d, self.bd4e = int(p['prime']), int(p['biome'] == 14)
        self.sea = p['sea']; self.biome = p['biome']
        self.position = tuple(float(math.floor(c / 1000) * 1000) for c in p['centre']) if p['centre'] else None

if __name__ == '__main__':
    places, table = json.load(open(sys.argv[1])), json.load(open(sys.argv[2]))
    vox = open(sys.argv[3], 'rb').read()
    for p in places:
        P = Inputs(p, vox)
        live = Planet(os.path.join(sys.argv[4], f"obj_{p['ua']}.bin"))
        note = ''
        if P.position is None: P.position, note = live.position, ' (centre from live data: prime body)'
        recs = override_list(table, P)
        res = place(P, recs)
        ok = sum(1 for (k, _), r in zip(res, live.live) if k == r['key'])
        worst = max(math.dist(pos, r['pos']) for (_, pos), r in zip(res, live.live) if pos)
        print(p['ua'], f'keys {ok}/{len(live.live)}, worst position {worst:.3f} m{note}')
