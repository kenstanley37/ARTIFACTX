"""Read-only native hotspot attribute vectors, executable inspected 2026-11-01.

Default vectors validate attributes with geometry stubbed. --geometry executes
the native geometry and its vector initializer, also recording directions.
Neither mode resolves nearby-hotspot selection or actual extractor resources.
Requires capstone, pefile and unicorn. Keep generated vectors outside the repo.
"""
import argparse
import json
import math
from pathlib import Path
import random
import struct
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86_const import X86_REG_RIP
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RSP, UC_X86_REG_RIP, UC_X86_REG_RAX, UC_X86_REG_RCX, UC_X86_REG_RDX, UC_X86_REG_R8, UC_X86_REG_R9, UC_X86_REG_XMM0, UC_X86_REG_XMM1

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--exe', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
parser.add_argument('--geometry', action='store_true')
parser.add_argument('--poles', action='store_true', help='Execute complete pole generation (requires --geometry).')
parser.add_argument('--near', action='store_true', help='Execute nearby query with synthetic world radius services.')
parser.add_argument('--reference', type=Path, help='External cases containing Address, Radius, Point, Configs and Rules; requires --near.')
args = parser.parse_args()
if args.poles and not args.geometry:
    parser.error('--poles requires --geometry')
if args.near and (not args.geometry or args.poles):
    parser.error('--near requires --geometry and cannot be combined with --poles')
if args.reference and not args.near:
    parser.error('--reference requires --near')
references = json.loads(args.reference.read_text(encoding='utf-8-sig')) if args.reference else None
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
if args.poles or args.near:
    load_function(0x1412f6110, 0x470)
if args.near:
    queried_poles = []
    mu.hook_add(UC_HOOK_CODE, lambda m, a, s, u: queried_poles.append([
        m.reg_read(UC_X86_REG_R8) & 0xffffffff, m.reg_read(UC_X86_REG_R9) & 0xffffffff,
        struct.unpack('<f', struct.pack('<I', m.reg_read(UC_X86_REG_XMM1) & 0xffffffff))[0]]),
        begin=0x1412f6110, end=0x1412f6110)
    load_function(0x1412f4ac0, 0x3ef)
    load_function(0x1412f4eb0, 0x200)
    load_function(0x1412f54f0, 0xa1)

table, rng, output, stack, sentinel = 0x20000000, 0x21000000, 0x22000000, 0x30000000, 0x31000000
write(table, bytes(0x1000))
write(rng, bytes(0x1000))
write(output, bytes(0x1000))
write(stack, bytes(0x10000))
write(sentinel, b'\xc3')
context, nodes = 0x23000000, 0x24000000
write(context, bytes(0x1000))
write(nodes, bytes(0x10000))
write(0x146ef1da0, struct.pack('<Q', table))
write(table + 0x360, struct.pack('<f', 1000))
if args.geometry:
    for target, length in [(0x140201600, 0xc6), (0x140201520, 0xe0),
                           (0x1413524b0, 0x142), (0x1412f59a0, 0x548), (0x1412f55a0, 0x3f9), (0x140033520, 0x10)]:
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
if args.near:
    def scalar(m, register):
        return struct.unpack('<f', struct.pack('<I', m.reg_read(register) & 0xffffffff))[0]

    def world_service(m, pc, size, user):
        if pc == 0x14348c60c:
            write(m.reg_read(UC_X86_REG_RCX), bytes(m.reg_read(UC_X86_REG_R8)))
        elif pc == 0x14169e740:
            m.reg_write(UC_X86_REG_RAX, int(planet_radius / 1024))
        elif pc in [0x1402c5d20, 0x14348c6c6, 0x14348c696]:
            value = scalar(m, UC_X86_REG_XMM0)
            result = math.log2(value) if pc == 0x1402c5d20 else math.ceil(value) if pc == 0x14348c6c6 else value ** scalar(m, UC_X86_REG_XMM1)
            m.reg_write(UC_X86_REG_XMM0, struct.unpack('<I', struct.pack('<f', result))[0])

    for target in [0x14348c60c, 0x1421f0ed0, 0x141351160, 0x14169e740, 0x1402c5d20, 0x14348c6c6, 0x14348c696]:
        write(target, b'\xc3')
        mu.hook_add(UC_HOOK_CODE, world_service, begin=target, end=target)
rows = []
for case in range(len(references) if references else 256):
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
    rules = dict(Spacing=1000, MinimumCount=1.9, MaximumCount=3.8, SameCategorySpacing=10, DifferentCategoryOverlap=150)
    if references:
        configs = references[case]['Configs']
        rules = references[case]['Rules']
        total = 0.0
        for kind, config in enumerate(configs):
            total += config['Weight']
            write(0x146ef1d88 + kind * 4, struct.pack('<f', total))
            write(table + 0x230 + kind * 0x30, struct.pack('<12f', *config['Strengths'], *config['ClassWeights'],
                  0, config['MaximumRange'], config['MinimumRange'], config['Weight']))
    write(table + 0x350, struct.pack('<5f', rules['DifferentCategoryOverlap'], rules['SameCategorySpacing'],
                                   rules['MaximumCount'], rules['MinimumCount'], rules['Spacing']))
    seed = sample.getrandbits(64)
    boundary_addresses = [0, 1, 0xffffffffffffffff, 0x100000000, 0xfff0000000000000, 0x123456789abcdef0]
    address = boundary_addresses[case] if case < len(boundary_addresses) else sample.getrandbits(64)
    pole_a, pole_b = sample.randrange(100), sample.randrange(100)
    planet_radius = float(sample.randint(15000, 140000))
    if args.near:
        planet_radius = float(sample.choice([15, 63, 96, 127]) * 1024)
    if references:
        address = references[case]['Address']
        planet_radius = references[case]['Radius']
    if args.poles:
        first_rotation = len(rotation_inputs)
        write(table + 0x350, struct.pack('<4f', 150, 10, 3.8, 1.9))
        write(context + 0x400, struct.pack('<IIQ', 256, 0, nodes))
        rsp = stack + 0xff08
        write(rsp, struct.pack('<Q', sentinel))
        write(rsp + 0x28, struct.pack('<Q', context))
        mu.reg_write(UC_X86_REG_RSP, rsp)
        mu.reg_write(UC_X86_REG_RCX, address)
        mu.reg_write(UC_X86_REG_R8, pole_a)
        mu.reg_write(UC_X86_REG_R9, pole_b)
        mu.reg_write(UC_X86_REG_XMM1, struct.unpack('<I', struct.pack('<f', planet_radius))[0])
        mu.emu_start(0x1412f6110, sentinel, count=100000)
        if mu.reg_read(UC_X86_REG_RIP) != sentinel:
            raise RuntimeError('Pole execution exceeded instruction budget.')
        count = struct.unpack('<I', mu.mem_read(context + 0x404, 4))[0]
        results = []
        for index in range(count):
            node = bytes(mu.mem_read(nodes + index * 64, 64))
            flag, grade, strength, highest, radius = struct.unpack_from('<IIfff', node, 0x20)
            results.append(dict(Direction=struct.unpack_from('<4f', node), Flag=flag, Class=grade,
                                Strength=strength, HighestStrength=highest, Range=radius))
        rows.append(dict(Address=address, Radius=planet_radius, PoleA=pole_a, PoleB=pole_b,
                         Configs=configs, Results=results, Attempts=len(rotation_inputs) - first_rotation))
        continue
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
    if mu.reg_read(UC_X86_REG_RIP) != sentinel:
        raise RuntimeError('Candidate execution exceeded instruction budget.')
    flag, grade, strength, highest, radius = struct.unpack('<IIfff', mu.mem_read(output + 0x20, 20))
    row = dict(Seed=seed, Configs=configs, Flag=flag, Class=grade, Strength=strength,
               HighestStrength=highest, Range=radius, FinalSeed=struct.unpack('<Q', mu.mem_read(rng, 8))[0])
    if args.geometry:
        row['Direction'] = struct.unpack('<4f', mu.mem_read(output, 16))
        row.update(Address=address, Orientation=orientation, PoleA=pole_a, PoleB=pole_b, Radius=planet_radius)
        row['RotationInput'] = rotation_inputs[-1]
        point = [v * planet_radius for v in row['Direction'][:3]] + [1.0]
        if references:
            point = references[case]['Point'][:3] + [1.0]
        write(output + 0x300, struct.pack('<4f', *point))
        write(rsp, struct.pack('<Q', sentinel))
        mu.reg_write(UC_X86_REG_RSP, rsp)
        mu.reg_write(UC_X86_REG_RCX, output + 0x200)
        mu.reg_write(UC_X86_REG_RDX, output + 0x100)
        mu.reg_write(UC_X86_REG_R8, output + 0x300)
        mu.emu_start(0x1412f55a0, sentinel, count=10000)
        row['Point'] = struct.unpack('<4f', mu.mem_read(output + 0x300, 16))
        row['Inverse'] = struct.unpack('<4f', mu.mem_read(output + 0x200, 16))
        if args.near:
            write(context + 0x400, struct.pack('<IIQ', 256, 0, nodes))
            write(rsp, struct.pack('<Q', sentinel))
            mu.reg_write(UC_X86_REG_RSP, rsp)
            mu.reg_write(UC_X86_REG_RCX, address)
            mu.reg_write(UC_X86_REG_RDX, output + 0x300)
            mu.reg_write(UC_X86_REG_R8, context)
            mu.emu_start(0x1412f4ac0, sentinel, count=200000)
            if mu.reg_read(UC_X86_REG_RIP) != sentinel:
                raise RuntimeError('Nearby query exceeded instruction budget.')
            count = struct.unpack('<I', mu.mem_read(context + 0x404, 4))[0]
            results = []
            for index in range(count):
                node = bytes(mu.mem_read(nodes + index * 64, 64))
                flag, grade, strength, highest, radius = struct.unpack_from('<IIfff', node, 0x20)
                results.append(dict(Direction=struct.unpack_from('<4f', node), Flag=flag, Class=grade,
                                    Strength=strength, HighestStrength=highest, Range=radius))
            row['Results'] = results
            row['QueriedPoles'] = queried_poles[-4:]
            row['Rules'] = rules
            write(rsp, struct.pack('<Q', sentinel))
            mu.reg_write(UC_X86_REG_RSP, rsp)
            mu.reg_write(UC_X86_REG_RCX, address)
            mu.reg_write(UC_X86_REG_RDX, output + 0x300)
            mu.reg_write(UC_X86_REG_R8, 14)  # All mineral hotspot flags.
            mu.reg_write(UC_X86_REG_R9, output + 0x400)
            mu.emu_start(0x1412f4eb0, sentinel, count=200000)
            if mu.reg_read(UC_X86_REG_RIP) != sentinel:
                raise RuntimeError('Closest query exceeded instruction budget.')
            row['ClosestMineralFlag'] = struct.unpack('<I', mu.mem_read(output + 0x420, 4))[0]
            row['ClosestMineralDirection'] = struct.unpack('<4f', mu.mem_read(output + 0x400, 16))
    rows.append(row)
args.out.write_text(json.dumps(rows, indent=2, allow_nan=False), encoding='utf-8')
print(f'Wrote {len(rows)} native vectors; geometry {"executed" if args.geometry else "stubbed"}.')
