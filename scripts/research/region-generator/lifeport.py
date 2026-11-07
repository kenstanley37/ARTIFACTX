# Port of the planet life/sentinel/scrap stage 0x1416956C0 (docs/research/PLANET_BUILDING_PLACEMENT.md), checked
# against values read from the running game (scripts/diagnostics/read_planet_life_inputs.py).
# usage: python lifeport.py galaxy difficulty seed:biome:sub:class:star:index [...]
# The stage reseeds the generator MWC from stage seed entry 0, then: Life (1 draw unless Class Initial or biome
# Dead/GasGiant/Waterworld), CreatureLife (same rule, Waterworld fixed 3), ResourceLevel (1 draw), BuildingLevel,
# sentinel level per difficulty 0-3 (each from a copy of the MWC, so no state change), HasScrap (1 draw).
# Thresholds are NMS.exe globals read live: A .55 / A3 .25, B .13, C .5, D .25, E .25, scrap F .2.
import sys
from buildingseeds import stage_seeds, Mwc
A, A3, B, C, D, E, F = 0.55, 0.25, 0.13, 0.5, 0.25, 0.25, 0.2
GALAXY_TYPES = [4, 4, 2, 4, 4, 4, 1, 4, 4, 3, 4, 1, 4, 4, 2, 4, 4, 4, 3, 4]
DEAD, WEIRD, WATER, GAS, LUSH, SWAMP = 6, 7, 14, 15, 0, 0x19
def unit(w): return w * 2.0 ** -32
class Copy:
    def __init__(self, r): self.lo, self.hi = r.lo, r.hi
    def u(self):
        t = self.lo * 0x5A76F899 + self.hi; self.lo, self.hi = t & 0xFFFFFFFF, t >> 32; return unit(self.lo)
def sentinel(r, i, biome, sub, cls, star, index, gtype):
    if i == 0: return 0
    a, b = (A3, B) if i >= 3 else (A, 0.0 if i <= 1 else B)
    c = Copy(r)
    if cls == 1: lv = 1 if c.u() >= a else 0
    elif star == 4 and index == 1: lv = 3
    elif i >= 3 and biome == LUSH and sub != SWAMP: lv = 1 + (c.u() >= C)
    elif cls == 2: lv = 0
    elif b > c.u(): lv = 2
    else: lv = 1 if c.u() >= a else 0
    if gtype == 2 and cls != 1 and D > c.u(): lv = 2
    if biome == GAS and lv >= 2: lv = 0
    if lv == 2 and E > c.u(): lv = 3
    return lv
def stage(seed, galaxy, difficulty, biome, sub, cls, star, index, scrap_density=1.0):
    r = Mwc(stage_seeds(seed)[0]); gtype = GALAXY_TYPES[galaxy % 20]
    fixed = cls == 1 or biome in (DEAD, GAS, WATER)
    if not fixed: r.word()                       # Life
    if not (cls == 1 or biome in (DEAD, GAS, WATER)): r.word()   # CreatureLife
    resource = int(unit(r.word()) > 0.75)         # ResourceLevel threshold (0x144B29640)
    cur = sentinel(r, difficulty, biome, sub, cls, star, index, gtype)
    levels = []
    for i in range(4):
        lv = sentinel(r, i, biome, sub, cls, star, index, gtype)
        if lv == 3 and cur != 3: lv = 2
        levels.append(lv)
    scrap = biome != WEIRD and F >= unit(r.word()) and scrap_density != 0
    return resource, levels, int(scrap)
if __name__ == '__main__':
    galaxy, difficulty = int(sys.argv[1]), int(sys.argv[2])
    for arg in sys.argv[3:]:
        s, *v = arg.split(':')
        print(s, stage(int(s, 16), galaxy, difficulty, *map(int, v)))
