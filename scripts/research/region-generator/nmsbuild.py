# Translates the build-184271 code addresses the emulators were written against to the installed NMS.exe's build.
# Rows come from static/remap.py (old function bytes from a Ghidra export of 184271, moving fields masked, unique match
# in the new .text) and static/insdiff.py (whole bodies equal apart from RIP displacements, call targets and jump-table
# offsets). Build 185034 (Forsaken 7.14, 2026-11-10): all 13 functions below are the same code, only moved; 12 match
# byte for byte once masked, and 0x1412B7F70 differs only in its image-relative jump-table offsets (+0x3A0, its own
# shift). emu_columns.py on today's planet-5 snapshot: column height 267.67 m, as Core's Height at the beacon.
# usage: from nmsbuild import A, BUILD;  call(A(0x1412B5F70), ...)
import os, pefile

EXE = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")

def _build(path):
    pe = pefile.PE(path, fast_load=True)
    pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_RESOURCE"]])
    for info in getattr(pe, 'FileInfo', []) or []:
        for entry in info:
            for table in getattr(entry, 'StringTable', []):
                v = table.entries.get(b'FileVersion')
                if v: return v.decode().split('.')[-1].strip() if '.' in v.decode() else v.decode().strip()
    return None

BUILD = _build(EXE)

MOVES = {
    '184271': {},
    '185034': {
        0x14128DB30: 0x14128DED0,   # column table fill (noise layers)
        0x14128F0A0: 0x14128F440,   # building pads
        0x141291310: 0x1412916B0,   # region point -> world
        0x141291DF0: 0x141292190,   # region transform setup
        0x141294A00: 0x141294DA0,   # Far region regenerate
        0x1412B1D60: 0x1412B2100,   # density pass
        0x1412B3770: 0x1412B3B10,   # region generator
        0x1412B5F70: 0x1412B6310,   # column table setup
        0x1412B7F70: 0x1412B8310,   # spawn sampling
        0x142C91990: 0x142C91F70,   # frame allocator: get
        0x142C92910: 0x142C92EF0,   # frame allocator: alloc
        0x142C93ED0: 0x142C944B0,   # frame allocator: rewind
        0x142DEC840: 0x142DECD80,   # building-site Voronoi wrapper
    },
}

def A(va_184271):
    """The installed build's address for a build-184271 code address; stops if this build has no row for it."""
    moves = MOVES.get(BUILD)
    if moves is None:
        raise SystemExit(f'NMS.exe build {BUILD} has no address table in nmsbuild.py: run static/remap.py first')
    if not moves: return va_184271
    if va_184271 not in moves:
        raise SystemExit(f'{va_184271:#x} not remapped for build {BUILD}: add it with static/remap.py')
    return moves[va_184271]
