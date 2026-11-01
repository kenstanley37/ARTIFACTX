"""Synthetic native vectors for the hotspot resource selector inspected 2026-11-01.

Requires pefile and unicorn. Only the loaded-world/address service calls are
stubbed; the native resource selection and biome indexing execute unchanged.
No running game, save writes or real game content is required beyond the local
executable. Generated vectors must remain outside source control.
"""
import argparse
import json
from pathlib import Path
import struct
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RSP, UC_X86_REG_RCX, UC_X86_REG_RDX, UC_X86_REG_R8, UC_X86_REG_R9, UC_X86_REG_RAX

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--exe', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()
pe = pefile.PE(str(args.exe), fast_load=True)
base, entry = pe.OPTIONAL_HEADER.ImageBase, 0x1412f5ef0
code = pe.get_data(entry - base, 0x151)
if not code.startswith(bytes.fromhex('48895c240848896c24104889742418')):
    raise RuntimeError('Resource selector prologue differs; reinspect executable.')
for signature in ['83ff02', '83ff04', '83ff08', '83ff10', '83ff20']:
    if bytes.fromhex(signature) not in code:
        raise RuntimeError('Resource selector branches differ; reinspect executable.')
mu = Uc(UC_ARCH_X86, UC_MODE_64)
mapped = set()


def map_region(address, size):
    for page in range(address & ~0xfff, (address + size + 0xfff) & ~0xfff, 0x1000):
        if page not in mapped:
            mu.mem_map(page, 0x1000)
            mapped.add(page)


def write(address, value):
    map_region(address, len(value))
    mu.mem_write(address, value)


write(0x1412f5000, pe.get_data(0x1412f5000 - base, 0x2000))
# The selector's empty-id constant is a real executable constant, not fabricated.
empty = 0x1412f5f12 + 7 + 0x37fdb67
write(empty, pe.get_data(empty - base, 16))
state, universe, table, output = 0x20000000, 0x21000000, 0x22000000, 0x23000000
stack, sentinel = 0x30000000, 0x31000000
map_region(state, 0x730000)
map_region(universe, 0x10000)
map_region(table, 0x1000)
map_region(output, 0x1000)
map_region(stack, 0x10000)
map_region(sentinel, 0x1000)
write(0x146f5f868, struct.pack('<Q', state))
write(state + 0x72b610, struct.pack('<Q', universe))
write(universe + 0x2544, struct.pack('<i', 1))
write(0x146ef1da0, struct.pack('<Q', table))
system = 0x123456789
address = (1 << 52) | system


def service(mu, pc, size, user):
    if pc == 0x14058ce20:
        mu.reg_write(UC_X86_REG_RAX, address)
    elif pc == 0x141678600:
        mu.reg_write(UC_X86_REG_RAX, system)


for target in [0x14058ce20, 0x141678600]:
    write(target, b'\xc3')
    mu.hook_add(UC_HOOK_CODE, service, begin=target, end=target)


def identifier(value):
    return value.encode('ascii').ljust(16, b'\0')


planet = universe + 0x2e30
inputs = dict(CommonMineral='COMMON', BiomeMineral='BIOME', RareMineral='RARE')
write(planet + 0x3430, identifier(inputs['CommonMineral']))
write(planet + 0x3490, identifier(inputs['BiomeMineral']))
write(planet + 0x3460, identifier(inputs['RareMineral']))
types = [('None', 0), ('Power', 1), ('Mineral1', 2), ('Mineral2', 4), ('Mineral3', 8), ('Gas1', 16), ('Gas2', 32)]
rows = []
for biome in range(17):
    first, second = f'GAS_A_{biome}', f'GAS_B_{biome}'
    write(table + biome * 32, identifier(first) + identifier(second))
    write(planet + 0x3318, struct.pack('<i', biome))
    for kind, flag in types:
        rsp = stack + 0xff08
        write(rsp, struct.pack('<Q', sentinel))
        mu.reg_write(UC_X86_REG_RSP, rsp)
        mu.reg_write(UC_X86_REG_RCX, output)
        mu.reg_write(UC_X86_REG_RDX, 1)  # Resource network, rather than power.
        mu.reg_write(UC_X86_REG_R8, flag)
        mu.reg_write(UC_X86_REG_R9, address)
        mu.emu_start(entry, sentinel, count=10000)
        result = bytes(mu.mem_read(output, 16)).split(b'\0')[0].decode('ascii')
        rows.append(dict(**inputs, FirstGas=first, SecondGas=second, Type=kind, Expected=result))
args.out.write_text(json.dumps(rows, indent=2), encoding='utf-8')
print(f'Wrote {len(rows)} native resource selections.')
