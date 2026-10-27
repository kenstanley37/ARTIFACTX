import os
"""usage: python peek.py 0xADDR [bytes] - section and file contents at a VA in NMS.exe (bss if beyond raw data)"""
import sys, pefile
pe = pefile.PE(os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe"), fast_load=True)
a = int(sys.argv[1], 16); n = int(sys.argv[2]) if len(sys.argv) > 2 else 32
rva = a - pe.OPTIONAL_HEADER.ImageBase
sec = pe.get_section_by_rva(rva)
inraw = rva - sec.VirtualAddress < sec.SizeOfRawData
print(hex(a), sec.Name.rstrip(b'\0').decode(), pe.get_data(rva, n).hex() if inraw else '(bss: zero in file)')
