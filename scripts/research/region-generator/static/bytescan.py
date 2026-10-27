import os
"""usage: python bytescan.py <regex-of-bytes-in-hex-escapes>  - finds byte patterns in NMS.exe .text, prints VAs"""
import sys, re, pefile
p = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
d = open(p, 'rb').read()
pe = pefile.PE(data=d, fast_load=True)
s = next(x for x in pe.sections if x.Name.startswith(b'.text'))
t = d[s.PointerToRawData:s.PointerToRawData + s.SizeOfRawData]
va = pe.OPTIONAL_HEADER.ImageBase + s.VirtualAddress
pattern = re.compile(sys.argv[1].encode('ascii'), re.S)  # regex handles \xNN escapes itself
for m in pattern.finditer(t):
    print(hex(va + m.start()), t[m.start():m.end()].hex())
