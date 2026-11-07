# Fits the terrain layer draw model (docs/research/PLANET_BUILDING_PLACEMENT.md, 0x1416C4F60) to live planets:
# value = second + u * (first - second) per field in the game's draw order, one Active-chance draw per layer;
# layer 0's Height uses word 4 of the stage-4 stream (drawn early in the terrain step) instead of its own draw.
# usage: python voxelfit.py <scratch folder>   (needs obj_*.bin and voxel/voxelgeneratorsettings.mbin)
import sys, struct, pickle, glob, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from buildingseeds import stage_seeds, Mwc
S = sys.argv[1]
ORDER = [(0x7c,'f'),(0x40,'f'),(0x60,'f'),(0x64,'f'),(0x8,'i'),(0x14,'i'),(0x30,'f'),(0x4,'f'),(0x18,'f'),(0x0,'f'),(0x2c,'f'),(0x38,'f'),(0x10,'f'),(0xc,'f'),(0x20,'f'),(0x1c,'f'),(0x28,'f'),(0x24,'f'),(0x3c,'f'),(0x34,'f'),(0x70,'f'),(0x6c,'f'),(0x44,'f'),(0x58,'f'),(0x50,'f'),(0x54,'i')]
def f32(x): return struct.unpack('<f', struct.pack('<f', x))[0]
def unit(w): return f32(w * 2.3283064370807974e-10)
vox = open(f'{S}/voxel/voxelgeneratorsettings.mbin', 'rb').read()
seeds = {'0010FE00011476E2': 0xC11D1E6EE8A8FD60, '0020FE00011476E2': 0xB6B082CA23FE0DDC, '0030FE00011476E2': 0x10430ECD09C556F1,
         '0040FE00011476E2': 0x0D5CBCAACF0B2404, '0050FE00011476E2': 0x447553726422F63C, '0060FE00011476E2': 0x7752D26E93C5A40F}
for f in sorted(glob.glob(f'{S}/obj_*.bin')):
    ua = os.path.basename(f)[4:20]; B = pickle.load(open(f, 'rb')); data = B['obj'][0x60:]
    idx = struct.unpack_from('<i', data, 0x32CC)[0]; el = vox[0x20 + idx * 0x22A0:]; first, second = el[:0x1150], el[0x1150:0x22A0]
    terr = data[0x2030:0x2030 + 0x1150]
    best = None
    for stage in range(26):
        m = Mwc(stage_seeds(seeds[ua])[stage]); words = [m.word() for _ in range(600)]
        for k in range(0, 300):
            pos, ok, tot, miss = k, 0, 0, []
            for L in range(8):
                o = 0xAC0 + L * 0x84
                pos += 1
                if not terr[o + 0x80]: continue
                for off, kind in ORDER:
                    fmt = '<f' if kind == 'f' else '<i'
                    lo = struct.unpack_from(fmt, second, o + off)[0]; hi = struct.unpack_from(fmt, first, o + off)[0]; v = struct.unpack_from(fmt, terr, o + off)[0]
                    w = words[pos]; pos += 1
                    if L == 0 and off == 0x40: w = words[4]   # layer 0 Height: the terrain step's 5th draw (word 4)
                    if lo == hi: continue
                    tot += 1
                    pred = f32(f32(unit(w) * f32(hi - lo)) + lo) if kind == 'f' else ((w * (hi - lo + 1)) >> 32) + lo
                    if pred == v: ok += 1
                    elif len(miss) < 3: miss.append((L, hex(off), pred, v))
            if best is None or ok > best[0]: best = (ok, tot, stage, k, miss)
    print(ua, 'setting', idx, 'varying fields matching %d/%d at stage %d word %d' % best[:4], 'misses', best[4])
