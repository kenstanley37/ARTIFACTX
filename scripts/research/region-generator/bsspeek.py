# Runs the startup initializers the region generator depends on, then prints runtime (BSS) vectors.
# usage: python bsspeek.py 0xADDR [0xADDR ...]   (each printed as four floats and raw hex)
import os, sys, struct
EMU = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu')
DATA = os.environ.get('NMS_EMU_DATA') or sys.exit('set NMS_EMU_DATA (see README.md)')
os.chdir(DATA); sys.path.insert(0, EMU)
import emu_sys as E
import globals as _globals  # noqa: F401
for init in (0x140031340, 0x14003A060, 0x14003A070, 0x14003A110, 0x1401997A0, 0x140199890, 0x1401998A0, 0x1401998B0, 0x1401998C0, 0x140199930, 0x140199990, 0x1401999D0):
    E.call(init, ())
for a in (int(x, 16) for x in sys.argv[1:]):
    raw = bytes(E.mu.mem_read(a, 16))
    print(f'{a:X}', struct.unpack('<4f', raw), raw.hex())
