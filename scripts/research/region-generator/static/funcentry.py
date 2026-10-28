import os
"""usage: python funcentry.py 0xADDR [...] - the primary function entry of an address in NMS.exe: finds its .pdata chunk,
then follows chained unwind info (UNW_FLAG_CHAININFO) back to the function's first chunk."""
import sys, struct, bisect, pefile
pe = pefile.PE(os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe"), fast_load=True)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY['IMAGE_DIRECTORY_ENTRY_EXCEPTION']])
base = pe.OPTIONAL_HEADER.ImageBase
rows = sorted((e.struct.BeginAddress, e.struct.EndAddress, e.struct.UnwindData) for e in pe.DIRECTORY_ENTRY_EXCEPTION)
starts = [r[0] for r in rows]
def chunk(rva):
    i = bisect.bisect_right(starts, rva) - 1
    return rows[i] if i >= 0 and rows[i][0] <= rva < rows[i][1] else None
for a in (int(x, 16) for x in sys.argv[1:]):
    row, path = chunk(a - base), []
    while row:
        path.append(row[0])
        info = pe.get_data(row[2], 4)
        flags, codes = info[0] >> 3, info[2]
        if not flags & 0x4: break
        off = row[2] + 4 + 2 * (codes + (codes & 1))
        begin, end, unwind = struct.unpack('<III', pe.get_data(off, 12))
        row = (begin, end, unwind)
    print(f'{a:X}: entry {base + path[-1]:X} (chunks ' + ' <- '.join(f'{base + p:X}' for p in path) + ')' if path else f'{a:X}: no pdata')
