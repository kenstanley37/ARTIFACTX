"""Read-only native food-bonus oracle for the executable inspected 2026-11-01.

Requires pefile and unicorn. Product/reality dumps come from DataCataloger
dumpfile (depth 5 / list cap 10000 for products, 4 / 300 for DefaultReality).
No game bytes or extracted vectors belong in source control.
"""
import argparse
import json
from pathlib import Path
import re
import struct
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RAX, UC_X86_REG_RSP, UC_X86_REG_RCX, UC_X86_REG_RDX, UC_X86_REG_XMM0

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--exe', type=Path, required=True)
parser.add_argument('--products', type=Path, required=True)
parser.add_argument('--reality', type=Path, required=True)
parser.add_argument('--stats', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()

def text(path):
    raw = path.read_bytes()
    return raw.decode('utf-16' if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')

pe = pefile.PE(str(args.exe), fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
entry = 0x140ed70f0
lookup = 0x140ee4a70
code = pe.get_data(entry - base, 0x1c0)
if not code.startswith(bytes.fromhex('48895c2408574883ec20')):
    raise RuntimeError('Executable does not match the inspected food helper prologue.')
if bytes.fromhex('b325222284e49cf2cb') not in code and bytes.fromhex('25232284e49cf2cb') not in code:
    raise RuntimeError('Expected FNV constant is missing. Reinspect the executable.')

reality = text(args.reality).split('    FoodStatValues:', 1)[1]
ranges = {int(i): (float(mi), float(ma)) for i, ma, mi in re.findall(
    r'\[(\d+)\]\s*\(GcMinMaxFloat\)\s*Max:\s*\(Single\) ([^\n]+)\s*Min:\s*\(Single\) ([^\n]+)', reality)}
enum_body = re.search(r'enum StatsTypeEnum.*?\{(.*?)\}', text(args.stats), re.S)[1]
names = [x.strip().split('=')[0].strip() for x in enum_body.split(',') if x.strip()]
uc = Uc(UC_ARCH_X86, UC_MODE_64)
for address in [0x140ed7000, 0x140ee4000, 0x144bea000, 0x144beb000]:
    uc.mem_map(address, 4096)
    uc.mem_write(address, pe.get_data(address - base, 4096))
uc.mem_write(lookup, b'\xc3')
uc.mem_map(0x2000000, 0x40000)
uc.mem_map(0x3000000, 0x10000)
uc.mem_map(0x4000000, 4096)
uc.mem_write(0x2000000, struct.pack('<Q', 0x2010000))
for stat, (minimum, maximum) in ranges.items():
    if stat < len(names):
        uc.mem_write(0x2010000 + 0xc570 + stat * 8, struct.pack('<ff', maximum, minimum))
def hook(machine, address, size, user):
    if address == lookup:
        machine.reg_write(UC_X86_REG_RAX, 0x2030000)
uc.hook_add(UC_HOOK_CODE, hook)

vectors = []
for block in re.split(r'^      \[\d+\]\s*$', text(args.products), flags=re.M):
    identity = re.search(r'^          ID:\s*\n.*?"([^"]+)"', block, re.M)
    cooking = re.search(r'CookingValue:\s*\(Single\) ([^\n]+)', block)
    stat = re.search(r'FoodBonusStat:.*?\(StatsTypeEnum\) (\S+)', block, re.S)
    amount = re.search(r'FoodBonusStatAmount:\s*\(Single\) ([^\n]+)', block)
    if not all([identity, cooking, stat, amount]) or float(cooking[1]) <= 0 or stat[1] not in names:
        continue
    index = names.index(stat[1])
    minimum, maximum = ranges[index]
    product = identity[1]
    value, explicit = float(cooking[1]), float(amount[1])
    if len(product.encode('ascii')) > 16:
        raise RuntimeError('Product ID exceeds native storage')
    uc.mem_write(0x2020000, product.encode('ascii').ljust(16, b'\0'))
    uc.mem_write(0x20301bc, struct.pack('<if', index, explicit))
    uc.mem_write(0x20301a0, struct.pack('<f', value))
    uc.mem_write(0x300ff00, struct.pack('<Q', 0x4000000))
    uc.reg_write(UC_X86_REG_RSP, 0x300ff00)
    uc.reg_write(UC_X86_REG_RCX, 0x2000000)
    uc.reg_write(UC_X86_REG_RDX, 0x2020000)
    uc.emu_start(entry, 0x4000000, count=2000)
    expected = struct.unpack('<f', struct.pack('<I', uc.reg_read(UC_X86_REG_XMM0) & 0xffffffff))[0]
    vectors.append(dict(Id=product, Stat=index, Effect=stat[1], CookingValue=value,
        ExplicitBonus=explicit, Minimum=minimum, Maximum=maximum,
        Percentage=stat[1] != 'Suit_AutoTranslator', LowerIsBetter=stat[1] == 'Weapon_Scan_Recharge_Time', Expected=expected))
args.out.write_text(json.dumps(vectors), encoding='utf-8')
print(f'Wrote {len(vectors)} native food-bonus vectors.')
