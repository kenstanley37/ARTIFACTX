# Predicts a planet's building-stage Voronoi seeds from its planet seed, as traced in
# docs/research/PLANET_BUILDING_PLACEMENT.md: 26 stage seeds from the planet seed, entry 3 reseeds the
# generator's MWC, then VoronoiPointSeed = Next(10000) + 1 and VoronoiSectorSeed = Next(10000) + 1.
# usage: python buildingseeds.py <planet seed hex> [...]
import sys
M = 0xFFFFFFFF
def mix64(r):
    r = ((r >> 33) ^ r) * 0x64DD81482CBD31D7 & 0xFFFFFFFFFFFFFFFF
    r = ((r >> 33) ^ r) * 0xE36AA5C613612997 & 0xFFFFFFFFFFFFFFFF
    return (r >> 33) ^ r
class Mwc:
    def __init__(self, seed):
        lo, hi = seed & M, seed >> 32
        self.lo = lo + (lo == 0)
        self.hi = (((lo >> 16) | (lo << 16)) & M) ^ hi ^ lo
    def word(self):
        t = self.lo * 0x5A76F899 + self.hi
        self.lo, self.hi = t & M, t >> 32
        return self.lo
    def next(self, n): return ((self.word() * n) >> 32)
def stage_seeds(planet_seed):
    rng, out = Mwc(planet_seed), []
    for _ in range(26):
        first = rng.word(); second = rng.word()
        out.append(mix64((second << 32) | first))
    return out
if __name__ == '__main__':
  for text in sys.argv[1:]:
    seeds = stage_seeds(int(text, 16))
    rng = Mwc(seeds[3])
    point, sector = rng.next(10000) + 1, rng.next(10000) + 1
    print(f'planet {text}: stage seed 3 {seeds[3]:016X}; VoronoiPointSeed {point}, VoronoiSectorSeed {sector}')
