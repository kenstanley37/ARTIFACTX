"""Read-only native extraction-stock vectors for the executable inspected 2026-11-01.

Runs only the stock conversion helper with synthetic resource definitions.
Requires pefile and unicorn. Does not read or modify the running game or saves.
Generated vectors and executable bytes must not be committed.
"""
import argparse
import json
from pathlib import Path
import struct
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64
from unicorn.x86_const import UC_X86_REG_RSP, UC_X86_REG_RCX, UC_X86_REG_RDX, UC_X86_REG_RAX

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--exe', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()
pe = pefile.PE(str(args.exe), fast_load=True)
entry = 0x1412f5400
code = pe.get_data(entry - pe.OPTIONAL_HEADER.ImageBase, 0x9c)
if not code.startswith(bytes.fromhex('40534883ec30488339008bda')) or bytes.fromhex('f779100faf4114') not in code:
    raise RuntimeError('Stock helper differs; reinspect the executable before updating the oracle.')
mu = Uc(UC_ARCH_X86, UC_MODE_64)
mu.mem_map(entry & ~0xfff, 0x1000)
mu.mem_write(entry, code)
global_pointer = 0x146ef1da0
mu.mem_map(global_pointer & ~0xfff, 0x1000)
heap, stack, sentinel = 0x20000000, 0x30000000, 0x31000000
mu.mem_map(heap, 0x4000)
mu.mem_map(stack, 0x10000)
mu.mem_map(sentinel, 0x1000)
mu.mem_write(global_pointer, struct.pack('<Q', heap))
mu.mem_write(heap + 0x220, struct.pack('<Q', heap + 0x1000))
mu.mem_write(heap + 0x228, struct.pack('<i', 1))
item = b'SYNTHETIC'.ljust(16, b'\0')
mu.mem_write(heap + 0x2000, item)
vectors = []
for cost, produced in [(1, 1), (4, 7), (123, 19), (360000, 250)]:
    mu.mem_write(heap + 0x1000, item + struct.pack('<ii', cost, produced))
    for amount in sorted(set([0, 1, cost - 1, cost, cost + 1, cost * 2 - 1, cost * 2,
                              9386961, 10033886, 10134456])):
        rsp = stack + 0xff08  # Windows x64 entry alignment, including return address.
        mu.mem_write(rsp, struct.pack('<Q', sentinel))
        mu.reg_write(UC_X86_REG_RSP, rsp)
        mu.reg_write(UC_X86_REG_RCX, heap + 0x2000)
        mu.reg_write(UC_X86_REG_RDX, amount)
        mu.emu_start(entry, sentinel, count=1000)
        result = mu.reg_read(UC_X86_REG_RAX) & 0xffffffff
        vectors.append(dict(AmountCost=cost, Yield=produced, StoredUnits=amount, Expected=result))
args.out.write_text(json.dumps(vectors, indent=2), encoding='utf-8')
print(f'Wrote {len(vectors)} native stock conversions.')
