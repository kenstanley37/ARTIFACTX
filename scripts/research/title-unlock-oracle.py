"""Read-only build-184271 title grant early-exit check.

Runs native code on synthetic memory with the title lookup stubbed as found.
It never opens saves or attaches to the game. It does not test a new-title
grant or explain when the player's title/reward lists became inconsistent.
Requires pefile and unicorn; addresses and prologues are build-specific.
"""
import argparse
import struct
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE
from unicorn.x86_const import (
    UC_X86_REG_RAX, UC_X86_REG_RCX, UC_X86_REG_RDX, UC_X86_REG_RSP,
)

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--exe', required=True)
args = parser.parse_args()
pe = pefile.PE(args.exe, fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
eligibility, grant = 0x140614BD0, 0x1406153A0
lookup, table_lookup = 0x140451F40, 0x140EFFCF0
global_pointer = 0x146F6B208
for address, signature in [(eligibility, '48895c2410574883ec20'),
                           (grant, '48895c241048896c242057')]:
    if not pe.get_data(address - base, 16).startswith(bytes.fromhex(signature)):
        raise RuntimeError('Native title helper changed; reinspect before running.')


def run(entry):
    mu = Uc(UC_ARCH_X86, UC_MODE_64)
    mapped = set()

    def page(address):
        address &= ~0xfff
        if address not in mapped:
            mu.mem_map(address, 0x1000)
            mapped.add(address)

    for address, size in [(eligibility, 0x100), (grant, 0x220)]:
        page(address)
        page(address + size)
        mu.mem_write(address, pe.get_data(address - base, size))
    for address in [lookup, table_lookup, global_pointer]:
        page(address)
    mu.mem_write(lookup, b'\xc3')
    mu.mem_write(table_lookup, b'\xc3')
    account, title, stack, stop = 0x20000000, 0x20002000, 0x30000000, 0x31000000
    for address in [account, title, stack, stop, account + 0x3000, account + 0x4000]:
        page(address)
    mu.mem_write(global_pointer, struct.pack('<Q', account + 0x3000))
    # Title table object, with one valid synthetic row.
    mu.mem_write(account + 0x3308, struct.pack('<Q', account + 0x4000))
    mu.mem_write(account + 0x4000, struct.pack('<QI', account + 0x800, 1))
    # Account's unlocked-title set has one entry. Lookup returns index zero.
    mu.mem_write(account + 0x150, struct.pack('<QQ', title, title + 16))
    mu.mem_write(title, b'SYNTHETIC_TITLE\0\0')
    calls = []

    def hook(uc, address, size, user):
        if address not in [lookup, table_lookup]:
            return
        calls.append(address)
        if address == table_lookup:
            output = uc.reg_read(UC_X86_REG_RDX)
            uc.mem_write(output, struct.pack('<Q', account + 0x800))
            uc.reg_write(UC_X86_REG_RAX, output)
        else:
            uc.reg_write(UC_X86_REG_RAX, 0)

    mu.hook_add(UC_HOOK_CODE, hook)
    rsp = stack + 0xf08
    mu.mem_write(rsp, struct.pack('<Q', stop))
    mu.reg_write(UC_X86_REG_RSP, rsp)
    mu.reg_write(UC_X86_REG_RCX, account)
    mu.reg_write(UC_X86_REG_RDX, title)
    mu.emu_start(entry, stop, count=1000)
    assert mu.reg_read(UC_X86_REG_RAX) & 0xff == 0
    # The grant routine must not insert the title or reach its specials loop.
    assert calls == ([lookup] if entry == eligibility else [table_lookup, lookup])
    return len(calls)


run(eligibility)
run(grant)
print('PASS: native existing-title eligibility and grant both return false before linked rewards.')
