# Python transcription of NMS.exe's scalar uber noise 0x1419BCA30 (build 183791; docs/research/PLANET_BUILDING_PLACEMENT.md),
# float32 lane for lane (the SSE adds' order kept), checked against emulator vectors (emu_uber.py).
# usage: python uberport.py <vectors.json>
import json, sys, struct, os
import numpy as np
import pefile

f = np.float32
EXE = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
_pe = pefile.PE(EXE, fast_load=True); _ib = _pe.OPTIONAL_HEADER.ImageBase
def _rd(va, n):
    for s in _pe.sections:
        if s.VirtualAddress <= va - _ib < s.VirtualAddress + s.SizeOfRawData:
            o = va - _ib - s.VirtualAddress; return s.get_data()[o:o + n]
PERM = list(_rd(0x144AFAEA0, 256))                                                   # Perlin permutation
GRAD = [np.array(struct.unpack_from('<4f', _rd(0x144AFADA0, 256), 16 * i), dtype=f) for i in range(16)]
F3, G3 = f(1 / 3), f(np.float32(0.1666666716337204))
F3 = np.frombuffer(_rd(0x144BEC174, 4), dtype=f)[0]; G3 = np.frombuffer(_rd(0x144BEB900, 4), dtype=f)[0]
V1 = np.frombuffer(_rd(0x144AFAFA0, 16), dtype=f); V2 = np.frombuffer(_rd(0x144AFAFC0, 16), dtype=f)
V3 = np.frombuffer(_rd(0x144AFAD80, 16), dtype=f); VR = np.frombuffer(_rd(0x144AFAFB0, 16), dtype=f)
VD = np.frombuffer(_rd(0x144AFAFD0, 16), dtype=f); VS = np.frombuffer(_rd(0x144AFAD90, 16), dtype=f)
ONE, TWO_OCT, K28, K08, K06, K3, HALF, TWO = (f(1), f(0.9), f(28), f(0.8), f(0.6), f(3), f(0.5), f(2))
ONE, TWO_OCT, K28, K08, K06, K3, HALF, TWO = (np.frombuffer(_rd(a, 4), dtype=f)[0] for a in
    (0x144BECAD8, 0x144BECA00, 0x144BECF8C, 0x144BEC944, 0x144BEC6DC, 0x144BECDB8, 0x144BEC588, 0x144BECCB0))
Z = f(0)

def trunc_floor(v):
    i = int(np.trunc(v))
    return i - (1 if f(i) > v else 0)
def unpacklo(a, b): return np.array([a[0], b[0], a[1], b[1]], dtype=f)
def unpackhi(a, b): return np.array([a[2], b[2], a[3], b[3]], dtype=f)
def hsum4(A, B, C, D):
    """The transpose-add network: lanes of the result = per-input (l0 + l2) + (l1 + l3), in order [A, C, B, D]."""
    x3 = unpacklo(A, B) + unpackhi(A, B)
    x0 = unpacklo(C, D) + unpackhi(C, D)
    return unpacklo(x3, x0) + unpackhi(x3, x0)

def simplex(x, y, z):
    """Inlined simplex with derivatives (0x1419BCC50..0x1419BD083). Returns (noise, [_, dx, dy, dz])."""
    s = f(f(f(y + x) + z) * F3)
    i, j, k = trunc_floor(f(s + x)), trunc_floor(f(s + y)), trunc_floor(f(s + z))
    t = f(f(k + j + i) * G3)
    x0 = f(x - f(f(i) - t)); y0 = f(y - f(f(j) - t)); z0 = f(z - f(f(k) - t))
    sil, dil, bl = int(x0 >= y0), int(y0 >= z0), int(z0 >= x0)
    r11, r10, r9 = sil & (bl ^ 1), dil & (sil ^ 1), bl & (dil ^ 1)          # first corner offset (x, y, z)
    esi, edi, ebx = sil | (bl ^ 1), dil | (sil ^ 1), bl | (dil ^ 1)          # second corner offset
    kk = k & 255
    g0 = PERM[(PERM[(PERM[kk] + j) & 255] + i) & 255] & 15
    g1 = PERM[(PERM[(PERM[(kk + r9) & 255] + r10 + j) & 255] + r11 + i) & 255] & 15
    g2 = PERM[(PERM[(PERM[(kk + ebx) & 255] + edi + j) & 255] + esi + i) & 255] & 15
    g3 = PERM[(PERM[(PERM[(kk + 1) & 255] + j + 1) & 255] + i + 1) & 255] & 15
    c0 = np.array([0, x0, y0, z0], dtype=f)
    c1 = (c0 - np.array([0, r11, r10, r9], dtype=f)) + V1
    c2 = (c0 - np.array([0, esi, edi, ebx], dtype=f)) + V2
    c3 = c0 + V3
    d2 = hsum4(c3 * c3, c1 * c1, c2 * c2, c0 * c0)                         # lanes [c3, c2, c1, c0]
    t = np.maximum(f(0), VR - d2)
    t2 = t * t; t4 = t2 * t2
    dots = hsum4(c3 * GRAD[g3], c1 * GRAD[g1], c2 * GRAD[g2], c0 * GRAD[g0])
    n = dots * t4
    dt3 = (dots * t) * t2
    pair = np.array([n[1], n[0], n[3], n[2]], dtype=f) + n                 # shufps 0xB1 + add
    noise = f(f(pair[0] + pair[2]) * K28)
    dv = (((np.full(4, dt3[2], f) * c1 + np.full(4, dt3[3], f) * c0) + np.full(4, dt3[1], f) * c2) + np.full(4, dt3[0], f) * c3) * VD
    gv = ((np.full(4, t4[3], f) * GRAD[g0] + np.full(4, t4[2], f) * GRAD[g1]) + np.full(4, t4[1], f) * GRAD[g2]) + np.full(4, t4[0], f) * GRAD[g3]
    return noise, (dv + gv) * VS

def uber(p, octaves, a):
    """0x1419BCA30. a: the TkNoiseUberData floats by name."""
    sharp = f(np.round(a['SharpToRoundFeatures'])) if False else f(np.float32(np.sign(a['SharpToRoundFeatures']) * np.floor(abs(a['SharpToRoundFeatures']) + 0.5)))
    sharp_pos, sharp_neg = max(Z, sharp), min(Z, sharp)
    freq = ONE; amp = ONE if octaves == 1 else TWO_OCT; ridge_amp = amp
    amplitude_first = f(a['AmplifyFeatures'] + a['Gain'])
    sumv = Z
    accA = [Z, Z, Z]; accB = [Z, Z, Z]; off = [Z, Z, Z]
    px, py, pz = (f(c) for c in p)
    gainterm = amplitude_first
    for _ in range(octaves):
        y = f(f(py * freq) + off[1]); x = f(f(px * freq) + off[0]); zc = f(f(pz * freq) + off[2])
        N, d = simplex(x, y, zc)
        n1 = f(f(f(f(N * N) - N) * sharp_pos) + N)
        ndx, ndy, ndz = f(N * d[1]), f(N * d[2]), f(N * d[3])
        se = f(a['SlopeErosion'])
        accA = [f(accA[0] + f(ndx * se)), f(accA[1] + f(ndy * se)), f(accA[2] + f(ndz * se))]
        accB = [f(accB[0] + f(ndx * K08)), f(accB[1] + f(ndy * K08)), f(accB[2] + f(ndz * K08))]
        pf = f(a['PerturbFeatures'])
        off = [f(off[0] + f(ndx * pf)), f(off[1] + f(ndy * pf)), f(off[2] + f(ndz * pf))]
        slopeA = f(f(f(f(accA[1] * accA[1]) + f(accA[0] * accA[0])) + f(accA[2] * accA[2])) + ONE)
        n2 = f(f(f(f(f(ONE - abs(N)) * K06) - n1) * abs(sharp_neg)) + n1)
        freq = f(freq * f(a['Lacunarity']))
        contrib = f(f(ONE / slopeA) * f(n2 * ridge_amp))
        sumv = f(sumv + contrib)
        c = min(ONE, max(Z, f(sumv - Z)))
        sm = f(f(K3 - f(c + c)) * f(c * c))
        g = f(f(f(f(sm * gainterm) - gainterm) * f(a['AltitudeErosion'])) + gainterm)
        gainterm = f(a['Gain'])
        amp = f(amp * g)
        slopeB = f(f(f(f(accB[1] * accB[1]) + f(accB[0] * accB[0])) + f(accB[2] * accB[2])) + ONE)
        ridge_amp = f(f(ONE - f(f(a['RidgeErosion']) / slopeB)) * amp)
    v = sumv
    if sharp == Z: v = f(f(v + ONE) * HALF)
    v = f(f(v - f(a['RemapFromMin'])) / f(f(a['RemapFromMax']) - f(a['RemapFromMin'])))
    v = min(ONE, max(Z, v))
    v = f(f(v * f(f(a['RemapToMax']) - f(a['RemapToMin']))) + f(a['RemapToMin']))
    v2 = f(v + v)
    sg, sb = f(a['SlopeGain']), f(a['SlopeBias'])
    if HALF > v:
        r = f(f(v2 / f(f(f(f(ONE / sg) - TWO) * f(ONE - v2)) + ONE)) * HALF)
    else:
        t = f(v2 - ONE)
        r = f(f(f(t / f(f(f(f(ONE / f(ONE - sg)) - TWO) * f(ONE - t)) + ONE)) * HALF) + HALF)
    return f(r / f(f(f(f(ONE / sb) - TWO) * f(ONE - r)) + ONE))

if __name__ == '__main__':
    V = json.load(open(sys.argv[1]))
    ok = 0; bad = []
    for v in V:
        got = uber(v['point'], v['octaves'], v['args'])
        gb = struct.unpack('<I', struct.pack('<f', float(got)))[0]
        if gb == v['bits']: ok += 1
        elif len(bad) < 8: bad.append((v['octaves'], float(got), v['value']))
    print(f'{ok}/{len(V)} bit-exact', bad)
