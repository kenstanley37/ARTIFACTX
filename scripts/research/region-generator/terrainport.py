# Python port of the parts of NMS.exe's terrain data generation 0x1416C6BF0 (build 183791;
# docs/research/PLANET_BUILDING_PLACEMENT.md) that the terrain height reads: the planet's TkVoxelGeneratorData noise
# layers, BaseSeed and sea level, from the terrain setting (voxelgeneratorsettings element: min set, max set at +0x1150)
# and the planet-seed stage-4 stream. The draw sequence is Core's PlanetProfileGenerator.Terrain (808/808 planets).
# usage: python terrainport.py <voxelgeneratorsettings.mbin> <planet bundle> [...]
import sys, struct, pickle
from buildingseeds import stage_seeds, Mwc

LAYERS, LAYER_BASE, LAYER_SIZE = 8, 0xAC0, 0x84
# A layer's drawn fields in the game's order (0x1416C4F60); value = second + u * (first - second), ints Next(range + 1).
ORDER = [(0x7c, 'f'), (0x40, 'f'), (0x60, 'f'), (0x64, 'f'), (0x8, 'i'), (0x14, 'i'), (0x30, 'f'), (0x4, 'f'), (0x18, 'f'),
         (0x0, 'f'), (0x2c, 'f'), (0x38, 'f'), (0x10, 'f'), (0xc, 'f'), (0x20, 'f'), (0x1c, 'f'), (0x28, 'f'), (0x24, 'f'),
         (0x3c, 'f'), (0x34, 'f'), (0x70, 'f'), (0x6c, 'f'), (0x44, 'f'), (0x58, 'f'), (0x50, 'f'), (0x54, 'i')]
SEA_BELOW_LAYER7 = 30.0          # 0x1416C7067: layer 7's Height = sea level - 30 on a planet with a sea above 1
RANDOM_ROCK = (8, 9)             # TkNoiseVoxelType RandomRock, RandomRockOrSubstance: extra draws (0x1416924c0)

def f32(x): return struct.unpack('<f', struct.pack('<f', x))[0]
def unit(w): return f32(w * 2.3283064370807974e-10)

def terrain(seed, setting, chances, rock, substance, sea, sea_standard, moon, dead_to_weird, pre_draws):
    """TkVoxelGeneratorData bytes as far as the height reads them. setting: the 0x22A0-byte element; chances: the biome
    file's 8 NoiseLayers chances; pre_draws: the terrain step's draws before the layers (index, sea; Core's Terrain)."""
    first, second = setting[:0x1150], setting[0x1150:0x22A0]
    out = bytearray(second)
    rng = Mwc(stage_seeds(seed)[4])
    words = [rng.word() for _ in range(400)]
    at = 0
    def draw():
        nonlocal at
        at += 1; return words[at - 1]
    for _ in range(pre_draws): draw()
    for i in range(LAYERS):
        o = LAYER_BASE + i * LAYER_SIZE
        on = bool(second[o + 0x80]) and chances[i] > unit(draw()) if second[o + 0x80] else False
        if i == 7:
            if sea > sea_standard: on = False
            elif dead_to_weird or sea > 1: on = True
        out[o + 0x80] = int(on)
        if not on: continue
        voxel = struct.unpack_from('<i', second, o + 0x74)[0]
        if voxel in RANDOM_ROCK:                                  # 0x1416C4E30
            if rock > unit(draw()): voxel = 1 if draw() >> 31 == 0 else 2
            elif voxel == 9 and substance > unit(draw()): voxel = 5 + ((draw() * 3) >> 32)
            else: voxel = 0
        struct.pack_into('<i', out, o + 0x74, voxel)
        struct.pack_into('<f', out, o + 0x5C, 1.0)                # RegionGain: the builder's default, never drawn
        for off, kind in ORDER:
            w = draw()
            if kind == 'f':
                lo, hi = struct.unpack_from('<f', second, o + off)[0], struct.unpack_from('<f', first, o + off)[0]
                struct.pack_into('<f', out, o + off, f32(f32(unit(w) * f32(hi - lo)) + lo))
            else:
                lo, hi = struct.unpack_from('<i', second, o + off)[0], struct.unpack_from('<i', first, o + off)[0]
                struct.pack_into('<i', out, o + off, ((w * (hi - lo + 1)) >> 32) + lo)
        if i == 7 and sea > 1:
            struct.pack_into('<f', out, o + 0x50, 0.0); struct.pack_into('<i', out, o + 0x54, 4)
            struct.pack_into('<f', out, o + 0x40, f32(sea - SEA_BELOW_LAYER7))
    if not sea > 0: out[LAYER_BASE + 4 * LAYER_SIZE + 0x80] = 0                    # 0x1416C75DF
    if moon: out[LAYER_BASE + 6 * LAYER_SIZE + 0x80] = 0                             # 0x1416C75EF
    struct.pack_into('<Q', out, 0xAB0, seed)
    struct.pack_into('<f', out, 0x1148, sea)
    return bytes(out)

def used_bytes_equal(a, b):
    """The fields the height reads: every active layer (Active, Subtract and the 0x80 data bytes), BaseSeed, NoSeaBaseLevel."""
    bad = []
    for i in range(LAYERS):
        o = LAYER_BASE + i * LAYER_SIZE
        if a[o + 0x80] != b[o + 0x80]: bad.append(f'layer {i} active'); continue
        if a[o + 0x80] and (a[o:o + 0x80] != b[o:o + 0x80] or a[o + 0x81] != b[o + 0x81]):
            bad.append(f'layer {i} fields ' + ' '.join(hex(k) for k in range(0, 0x80, 4) if a[o + k:o + k + 4] != b[o + k:o + k + 4]))
    for off in (0xAB0, 0x1140, 0x1148):
        if a[off:off + 4] != b[off:off + 4]: bad.append(hex(off))
    return bad

if __name__ == '__main__':
    vox = open(sys.argv[1], 'rb').read()
    for b in sys.argv[2:]:
        B = pickle.load(open(b, 'rb')); o = B['obj']; d = o[0x60:]; live = d[0x2030:0x2030 + 0x1150]
        seed = struct.unpack_from('<Q', o, 0x3B60)[0]
        idx = struct.unpack_from('<i', d, 0x32CC)[0]
        chances = struct.unpack_from('<8f', d, 0x3264)
        rock, substance = struct.unpack_from('<2f', d, 0x32A8)
        sea = struct.unpack_from('<f', live, 0x1148)[0]
        biome, size = struct.unpack_from('<i', d, 0x32B8)[0], struct.unpack_from('<i', d, 0x32C4)[0]
        results = {}
        for pre in range(0, 6):
            t = terrain(seed, vox[0x20 + idx * 0x22A0:0x20 + (idx + 1) * 0x22A0], chances, rock, substance, sea, 120.0, size == 3,
                        biome == 7, pre)
            results[pre] = used_bytes_equal(t, live)
        good = [p for p, r in results.items() if not r]
        print(b[-20:-4], 'biome', biome, 'sea', sea, 'exact with pre-draws', good or 'none', '' if good else results[3])
