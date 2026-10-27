# Dumps every rip-relative constant the cell loop, fBm 0x142D746E0 and simplex 0x142D75250 read,
# as floats and ints, so the C# port uses the exact values.
import os, re, struct, pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
pe = pefile.PE(os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe"), fast_load=True)
B = pe.OPTIONAL_HEADER.ImageBase
md = Cs(CS_ARCH_X86, CS_MODE_64)
def dump(name, start, end):
    print(f'== {name}')
    seen = set()
    for i in md.disasm(pe.get_data(start - B, end - start), start):
        m = re.search(r'\[rip ([+-]) (0x[0-9a-f]+)\]', i.op_str)
        if not m or i.mnemonic == 'lea': continue
        t = i.address + i.size + (1 if m.group(1) == '+' else -1) * int(m.group(2), 16)
        if t in seen: continue
        seen.add(t)
        raw = pe.get_data(t - B, 16)
        if len(raw) < 16: print(f'  {i.address:X} {i.mnemonic:7} -> {t:X}  (BSS: filled at startup)'); continue
        print(f'  {i.address:X} {i.mnemonic:7} -> {t:X}  f={tuple(round(v, 9) for v in struct.unpack("<4f", raw))}  i={struct.unpack("<4i", raw)}  d={struct.unpack("<d", raw[:8])[0]!r}')
dump('generator start (region vector scale)', 0x14136789B, 0x1413678C0)
dump('cell loop up to the density call', 0x1413685DC, 0x1413687D8)
dump('fBm 0x142D746E0', 0x142D746E0, 0x142D74857)
dump('simplex 0x142D75250', 0x142D75250, 0x142D7579D)
dump('spiral factor', 0x141369111, 0x141369130)
