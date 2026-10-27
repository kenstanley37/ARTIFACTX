import os
"""usage: python funcof.py 0xADDR [...] - containing function bounds from NMS.exe .pdata"""
import sys, struct, bisect, pefile
p = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
pe = pefile.PE(p, fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
pd = next(x for x in pe.sections if x.Name.startswith(b'.pdata'))
d = pd.get_data()
rows = sorted((base + struct.unpack_from('<I', d, i)[0], base + struct.unpack_from('<I', d, i + 4)[0])
              for i in range(0, len(d) - 11, 12) if struct.unpack_from('<I', d, i)[0])
starts = [r[0] for r in rows]
for a in [int(x, 16) for x in sys.argv[1:]]:
    k = bisect.bisect_right(starts, a) - 1
    print(f'{a:X} in {rows[k][0]:X}-{rows[k][1]:X}')
