"""Read-only native hotspot attribute vectors, executable inspected 2026-11-01.

Default vectors validate attributes with geometry stubbed. --geometry executes
the native geometry and its vector initializer, also recording directions.
Neither mode resolves nearby-hotspot selection or actual extractor resources.
Requires capstone, pefile and unicorn. Keep generated vectors outside the repo.
"""
import argparse
import json
from pathlib import Path
import random
import struct
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_REG_RIP
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RSP, UC_X86_REG_RCX, UC_X86_REG_RDX, UC_X86_REG_R8, UC_X86_REG_R9, UC_X86_REG_XMM0, UC_X86_REG_XMM1

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--exe', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
parser.add_argument('--geometry', action='store_true')
args = parser.parse_args()
pe = pefile.PE(str(args.exe), fast_load=True)
base, entry = pe.OPTIONAL_HEADER.ImageBase, 0x1412f6580
code = pe.get_data(entry - base, 0x3cf)
if not code.startswith(bytes.fromhex('488bc44889580848896820')):
    raise RuntimeError('Hotspot generator differs; reinspect executable.')
mu = Uc(UC_ARCH_X86, UC_MODE_64)
mapped = set()


def write(address, data):
    for page in range(address & ~0xfff, (address + len(data) + 0xfff) & ~0xfff, 0x1000):
        if page not in mapped:
            mu.mem_map(page, 0x1000)
            mapped.add(page)
    mu.mem_write(address, data)


dis = Cs(CS_ARCH_X86, CS_MODE_64)
dis.detail = True


def load_function(address, length):
    body = pe.get_data(address - base, length)
    write(address, body)
    for instruction in dis.disasm(body, address):
        for operand in instruction.operands:
            if operand.type == 3 and operand.mem.base == X86_REG_RIP:
                target = instruction.address + instruction.size + operand.mem.disp
                write(target, pe.get_data(target - base, 32))


load_function(entry, len(code))

table, rng, output, stack, sentinel = 0x20000000, 0x21000000, 0x22000000, 0x30000000, 0x31000000
write(table, bytes(0x1000))
write(rng, bytes(0x1000))
write(output, bytes(0x1000))
write(stack, bytes(0x10000))
write(sentinel, b'\xc3')
write(0x146ef1da0, struct.pack('<Q', table))
write(table + 0x360, struct.pack('<f', 1000))
if args.geometry:
    for target, length in [(0x140201600, 0xc6), (0x140201520, 0xe0),
                           (0x1413524b0, 0x142), (0x1412f59a0, 0x548), (0x140033520, 0x10)]:
        load_function(target, length)
    rsp = stack + 0xff08
    write(rsp, struct.pack('<Q', sentinel))
    mu.reg_write(UC_X86_REG_RSP, rsp)
    mu.emu_start(0x140033520, sentinel, count=100)
    rotation_inputs = []
    mu.hook_add(UC_HOOK_CODE, lambda m, a, s, u: rotation_inputs.append(
        struct.unpack('<4f', m.mem_read(m.reg_read(UC_X86_REG_R8), 16))),
        begin=0x1412f59a0, end=0x1412f59a0)
else:
    # sin affects geometry only. Coordinate conversion calls have no random draws.
    for target in [0x140201600, 0x1413524b0, 0x1412f59a0]:
        write(target, b'\xc3')
    mu.hook_add(UC_HOOK_CODE, lambda m, a, s, u: m.reg_write(UC_X86_REG_XMM0, 0x3f800000),
                begin=0x140201600, end=0x140201600)
sample = random.Random(1101)
rows = []
for case in range(256):
    configs = []
    total = 0.0
    for kind in range(6):
        weight = float(sample.randint(1, 15))
        total += weight
        write(0x146ef1d88 + kind * 4, struct.pack('<f', total))
        strengths = [float(sample.randint(1, 100)) for _ in range(4)]
        weights = [float(sample.randint(1, 12)) for _ in range(4)]
        minimum = float(sample.randint(10, 100))
        maximum = minimum + sample.randint(1, 300)
        write(table + 0x230 + kind * 0x30,
              struct.pack('<12f', *strengths, *weights, 0, maximum, minimum, weight))
        configs.append(dict(Weight=weight, Strengths=strengths, ClassWeights=weights,
                            MinimumRange=minimum, MaximumRange=maximum))
    write(0x146ef1d9c, struct.pack('<f', total))
    seed = sample.getrandbits(64)
    boundary_addresses = [0, 1, 0xffffffffffffffff, 0x100000000, 0xfff0000000000000, 0x123456789abcdef0]
    address = boundary_addresses[case] if case < len(boundary_addresses) else sample.getrandbits(64)
    pole_a, pole_b = sample.randrange(100), sample.randrange(100)
    planet_radius = float(sample.randint(15000, 140000))
    if args.geometry:
        rsp = stack + 0xff08
        write(rsp, struct.pack('<Q', sentinel))
        mu.reg_write(UC_X86_REG_RSP, rsp)
        mu.reg_write(UC_X86_REG_RCX, output + 0x100)
        mu.reg_write(UC_X86_REG_RDX, address)
        mu.emu_start(0x1413524b0, sentinel, count=10000)
        orientation = struct.unpack('<4f', mu.mem_read(output + 0x100, 16))
    write(rng, struct.pack('<Q', seed))
    rsp = stack + 0xff08
    write(rsp, struct.pack('<Q', sentinel))
    write(rsp + 0x28, struct.pack('<Q', rng))
    write(rsp + 0x30, struct.pack('<Q', output))
    mu.reg_write(UC_X86_REG_RSP, rsp)
    mu.reg_write(UC_X86_REG_RCX, address)
    mu.reg_write(UC_X86_REG_R8, pole_a)
    mu.reg_write(UC_X86_REG_R9, pole_b)
    mu.reg_write(UC_X86_REG_XMM1, struct.unpack('<I', struct.pack('<f', planet_radius))[0])
    mu.emu_start(entry, sentinel, count=10000)
    flag, grade, strength, highest, radius = struct.unpack('<IIfff', mu.mem_read(output + 0x20, 20))
    row = dict(Seed=seed, Configs=configs, Flag=flag, Class=grade, Strength=strength,
               HighestStrength=highest, Range=radius, FinalSeed=struct.unpack('<Q', mu.mem_read(rng, 8))[0])
    if args.geometry:
        row['Direction'] = struct.unpack('<4f', mu.mem_read(output, 16))
        row.update(Address=address, Orientation=orientation, PoleA=pole_a, PoleB=pole_b, Radius=planet_radius)
        row['RotationInput'] = rotation_inputs[-1]
    rows.append(row)
args.out.write_text(json.dumps(rows, indent=2, allow_nan=False), encoding='utf-8')
print(f'Wrote {len(rows)} native vectors; geometry {"executed" if args.geometry else "stubbed"}.')
