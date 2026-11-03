"""Read-only build-184271 known-specials account synchronization check.

Executes only the native load-loop fragment using synthetic memory. The account
grant/persist helpers are recorded stubs, not emulated. No save or running
process is opened. This establishes a possible restoration path, not which
save/context supplied the owner's restored Furnace entitlement.
"""
import argparse
import struct
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64, UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RBP, UC_X86_REG_RSI, UC_X86_REG_R15, UC_X86_REG_RSP, UC_X86_REG_RDX, UC_X86_REG_RAX

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--exe', required=True)
args = parser.parse_args()
pe = pefile.PE(args.exe, fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
start, stop = 0x1404A0634, 0x1404A06AF
code = pe.get_data(start - base, stop - start)
if not code.startswith(bytes.fromhex('32db418bff')):
    # Verify decoded instructions rather than assuming an address still matches.
    from capstone import Cs, CS_ARCH_X86, CS_MODE_64
    decoded = list(Cs(CS_ARCH_X86, CS_MODE_64).disasm(code, start))
    if [(i.mnemonic, i.op_str) for i in decoded[:3]] != [
            ('xor', 'bl, bl'), ('mov', 'edi, r15d'), ('cmp', 'dword ptr [rsi + 0x83d08], 0')]:
        raise RuntimeError('Load fragment changed; reinspect before running.')

grant, persist = 0x1406155C0, 0x1406157A0
# RIP-relative global at the verified 0x1404A0650 instruction.
pointer = 0x1404A0657 + struct.unpack('<i', pe.get_data(0x1404A0653 - base, 4))[0]

def run(ids):
    mu = Uc(UC_ARCH_X86, UC_MODE_64)
    pages = set()
    def map_range(address, size):
        for page in range(address & ~0xfff, (address + size + 0xfff) & ~0xfff, 0x1000):
            if page not in pages:
                mu.mem_map(page, 0x1000)
                pages.add(page)
    state, global_state, entries, stack = 0x20000000, 0x22000000, 0x23000000, 0x24000000
    for address, size in [(start, len(code)), (pointer, 8), (state, 0x84000),
                          (global_state, 0x316000), (entries, 0x1000), (stack, 0x2000),
                          (grant, 1), (persist, 1)]:
        map_range(address, size)
    mu.mem_write(start, code)
    mu.mem_write(pointer, struct.pack('<Q', global_state))
    mu.mem_write(state + 0x83d00, struct.pack('<QI', entries, len(ids)))
    for n, identity in enumerate(ids):
        mu.mem_write(entries + n * 16, identity.encode().ljust(16, b'\0'))
    for address in [grant, persist]:
        mu.mem_write(address, b'\xc3')
    calls = []
    def hook(uc, address, size, user):
        if address == grant:
            identity = bytes(uc.mem_read(uc.reg_read(UC_X86_REG_RDX), 16)).split(b'\0')[0].decode()
            calls.append(identity)
            uc.reg_write(UC_X86_REG_RAX, 1)
        elif address == persist:
            raise AssertionError('Synthetic account has no dirty flag; persist must not be called.')
    mu.hook_add(UC_HOOK_CODE, hook)
    mu.reg_write(UC_X86_REG_RSI, state)
    mu.reg_write(UC_X86_REG_R15, 0)
    mu.reg_write(UC_X86_REG_RBP, stack + 0x800)
    mu.reg_write(UC_X86_REG_RSP, stack + 0x1000)
    mu.emu_start(start, stop, count=1000)
    assert calls == ids, (calls, ids)

run([])
run(['TRUCK_BODY03'])
run(['TRUCK_BODY03', 'SYNTHETIC'])
print('PASS: native load fragment forwards every known special to the account grant helper; empty list forwards none.')
