# Python port of NMS.exe's override placement 0x141635D10 (build 183791; docs/research/PLANET_BUILDING_PLACEMENT.md):
# where a planet's portal, shelter, base, distress signal and start spawns stand. Phase 1 builds the start-area chain
# (anchor + points 500-600 from it, the last one 800-1000), phase 2 assigns the records; every candidate direction
# comes from the planet-seed MWC and must pass the accept test (site query, terrain height, flatness, water/height
# rules). Checked against the game's records read live (read_planet_object_live.py bundles).
# The record list comes from PLANETBUILDINGTABLE (tabledump -> override_table.json), not from the live records.
# usage: python placeport.py <override_table.json> <planet bundle> [...]
import sys, struct, math, pickle, json
import numpy as np
from uberport import f, ONE
from buildingseeds import Mwc
from siteport import nearest_site, feature
import heightport as hp
import flatport as fp

TWO_PI = f(6.2831854820251465); GROWTH = f(0.002)
CRASH_MIN, CRASH_MAX, SHELTER_MIN, SHELTER_MAX = f(500), f(600), f(800), f(1000)   # GcBuildingGlobals StartCrashSite/StartShelter Min/Max
SEA_ABOVE, WATER_ONLY_DEPTH, MIN_BELOW_SEA, MAX_SLOPE = f(5), f(35), f(60), f(1)
CHAIN_CLASSES, CHAIN_CLASSES_WATER = {2, 20, 28}, {28, 32}
UNIT = 2.3283064370807974e-10
SEA_MOON, SEA_GAS_GIANT = f(10), f(35)                      # GcTerrainGlobals: no water-only buildings at these levels
UNDERWATER_ONLY = 3                                         # TkNoiseFlattenOptions.WaterPlacement
# Ring radius of the flatness test on Prime planets for types whose OverrideRadius is -1: the game measures the model
# (GcBuildingSpawnData.Radius = half the AABB's XZ diagonal, BuildingSizeCalculated). Values read live (build 183791):
# Shelter's first type (the chain's accept test) and the Portal; the start spawns' 10 is never used by placement.
MODEL_RADIUS = {2: f(22.641382217407227), 16: f(37.76814651489258), 28: f(10)}

def override_list(table, P, npcs=True):
    """The type loop's override records (0x141635D10 step 1): per class in class order, NumOverridesToGenerate (or the
    Waterworld count when >= 0 on biome 14); none for NPC-only classes without NPCs or UnderwaterOnly without water -
    a sea level of 0 or exactly the moon or gas giant sea level (GcTerrainGlobals SeaLevelMoon / SeaLevelGasGiant)."""
    water_world = P.biome == 14
    out = []
    for row in table:
        n = row['overridesWater'] if water_world and row['overridesWater'] >= 0 else row['overrides']
        if not row['noNpc'] and not npcs: n = 0
        if row['water'] == UNDERWATER_ONLY and (not P.sea > 0 or P.sea in (SEA_MOON, SEA_GAS_GIANT)): n = 0
        ring = f(row['radius']) if row['radius'] > 0 else MODEL_RADIUS.get(row['cls'], f(0))
        out += [dict(cls=row['cls'], water=row['water'], minh=f(row['minh']), maxh=f(row['maxh']), ring=ring)] * max(n, 0)
    return out

class Planet:
    def __init__(self, bundle):
        B = pickle.load(open(bundle, 'rb')); o = B['obj']; self.obj = o
        self.data = o[0x60:]
        self.terrain = self.data[0x2030:0x2030 + 0x1150]
        self.seed = struct.unpack_from('<Q', o, 0x3B60)[0]
        blk = o[0x3398:0x3398 + 0x60]
        self.radius = struct.unpack_from('<f', blk, 0x38)[0]; self.divisions = int(struct.unpack_from('<f', blk, 0x40)[0])
        self.sector = struct.unpack_from('<I', blk, 0x48)[0]; self.bd4d, self.bd4e = blk[0x4D], blk[0x4E]
        self.position = struct.unpack_from('<3f', o, 0xD74D0)
        self.sea = struct.unpack_from('<f', self.terrain, 0x1148)[0]
        self.biome = struct.unpack_from('<i', self.data, 0x32B8)[0]
        tb, tn = B['types'][1], B['types'][2]
        self.types = [dict(cls=tb[i * 0xE0 + 0xD3], water=struct.unpack_from('<i', tb, i * 0xE0 + 0x9C)[0],
                           maxh=struct.unpack_from('<f', tb, i * 0xE0 + 0xB4)[0], minh=struct.unpack_from('<f', tb, i * 0xE0 + 0xBC)[0],
                           ring=struct.unpack_from('<f', tb, i * 0xE0 + 0xC0)[0]) for i in range(tn)]
        ob, on = B['overrides'][1], B['overrides'][2]
        self.live = [dict(pos=struct.unpack_from('<3f', ob, 0x30 * i), key=struct.unpack_from('<Q', ob, 0x30 * i + 0x10)[0],
                          type=struct.unpack_from('<I', ob, 0x30 * i + 0x20)[0]) for i in range(on)]

def accept(P, d, t):
    """0x141631160 for a candidate point d (planet-relative, on the sphere): (ok, key, ground position)."""
    key, face, cell, _ = nearest_site(d, P.sector, P.radius, P.divisions)
    fe = [f(c) for c in feature(face, *cell)]
    ln = f(np.sqrt(f(f(f(fe[0] * fe[0]) + f(fe[1] * fe[1])) + f(fe[2] * fe[2]))))
    A = [f(f(c / ln) * f(P.radius)) for c in fe]                       # site point (sub-mm from the game's own sequence)
    h = hp.height(P.terrain, A)
    slope, lo, hi = fp.flatness(P.terrain, A + [ONE], f(P.radius), t['ring'], P.bd4d != 0)
    sea = f(P.sea)
    under = f(sea + SEA_ABOVE) > h and sea > 0
    if P.bd4e: ok = True
    elif t['water'] == 3: ok = under and not h > f(sea - WATER_ONLY_DEPTH)
    else:
        ok = not (under and t['water'] not in (1, 2)) and not f(sea - MIN_BELOW_SEA) > h
    if t['minh'] > h or h > t['maxh'] or not ok or slope < 0 or MAX_SLOPE < slope: return False, key, None
    n = f(np.sqrt(f(f(f(A[0] * A[0]) + f(A[1] * A[1])) + f(A[2] * A[2]))))
    pos = [f(f(A[i] + f(f(A[i] / n) * h)) + f(P.position[i])) for i in range(3)]
    return True, key, pos

def direction(rng):
    u1 = f(rng.word() * UNIT); u2 = f(rng.word() * UNIT)
    y = f(ONE - f(u1 + u1)); r = f(np.sqrt(max(f(0), f(ONE - f(y * y)))))
    phi = f(u2 * TWO_PI)
    return [f(fp.game_cos(phi) * r), y, f(fp.game_sin(phi) * r)]

def place(P, recs):
    rng = Mwc(P.seed)
    water = P.biome == 14
    chain_cls = CHAIN_CLASSES_WATER if water else CHAIN_CLASSES
    chain_len = sum(1 for r in recs if r['cls'] in chain_cls)
    need = chain_len - 1
    chain, last = [], None
    attempts = 0; round_attempts = 0
    while not (len(chain) >= need and last is not None):
        s = f(f(f(attempts) * GROWTH) * f(f(attempts) * GROWTH))
        dvec = direction(rng)
        n_now = len(chain)
        if n_now:
            anchor = chain[0][1]; ar = [f(anchor[i] - f(P.position[i])) for i in range(3)]
            dist = f(s + (SHELTER_MAX if n_now == need else CRASH_MAX))
            q = [f(f(dvec[i] * dist) + ar[i]) for i in range(3)]
            m = f(np.sqrt(f(f(f(q[0] * q[0]) + f(q[1] * q[1])) + f(q[2] * q[2]))))
            dvec = [f(c / m) for c in q]
        d = [f(c * f(P.radius)) for c in dvec]
        ok, key, pos = accept(P, d, recs[0])
        if ok and all(k != key for k, _ in chain):
            good = True
            if n_now:
                lo_ = max(f(0), f((SHELTER_MIN if n_now == need else CRASH_MIN) - s))
                hi_ = f(f(s + (SHELTER_MAX if n_now == need else CRASH_MAX)))
                anchor = chain[0][1]; dd = sum(f(f(pos[i] - anchor[i]) ** 2) for i in range(3))
                good = not (f(lo_ * lo_) > dd or dd > f(hi_ * hi_))
            if good:
                if n_now == need: last = (key, pos)
                else: chain.append((key, pos))
        attempts += 1; round_attempts += 1
        if attempts > 20000: return None
        if round_attempts > 100:
            chain, last, round_attempts = [], None, 0
    out = []
    for r in recs:
        # 0x14163661C: DistressSignal (and WaterDistressSignal on a waterworld) take the chain's head, start spawns its
        # back, Shelter (and 31/24 on a waterworld) the last point; once the chain is used up, a record gets a random site.
        c = r['cls']
        if (c == 20 or (water and c == 32)) and chain: key, pos = chain.pop(0)
        elif c == 28 and chain: key, pos = chain.pop()
        elif (c == 2 or (water and c in (31, 24))) and last is not None: key, pos = last
        else:
            key = pos = None
            for _ in range(5000):
                d = [f(c_ * f(P.radius)) for c_ in direction(rng)]
                ok, k, p = accept(P, d, r)
                if ok: key, pos = k, p; break
        out.append((key, pos))
    return out

if __name__ == '__main__':
    table = json.load(open(sys.argv[1]))
    for b in sys.argv[2:]:
        P = Planet(b); recs = override_list(table, P)
        if [r['cls'] for r in recs] != [P.types[r['type']]['cls'] for r in P.live]: print(b, 'record list differs'); continue
        res = place(P, recs)
        if res is None: print(b, 'no chain'); continue
        ok = sum(1 for (k, p), r in zip(res, P.live) if k == r['key'])
        worst = max(math.dist(p, r['pos']) for (k, p), r in zip(res, P.live) if p)
        print(b.split('obj_')[-1][:16], f'keys {ok}/{len(P.live)}, worst position {worst:.3f} m')
