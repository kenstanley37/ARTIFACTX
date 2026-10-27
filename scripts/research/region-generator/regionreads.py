# Lists every .data/BSS address the region generator READS that still holds zero in the emulator
# and that nothing during the run wrote first: candidates for state the real game fills at startup.
import os, sys, struct, pefile
EMU = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu')  # emulator code
DATA = os.environ.get('NMS_EMU_DATA') or sys.exit('set NMS_EMU_DATA to the folder with the extracted .mbin files (see README.md)')
os.chdir(DATA); sys.path.insert(0, EMU)
import emu_sys as E
import globals as G
from unicorn import UC_HOOK_CODE, UC_HOOK_MEM_READ, UC_HOOK_MEM_WRITE
from unicorn import x86_const as X
pe = E.pe
data = next(s for s in pe.sections if s.Name.startswith(b'.data'))
lo = E.BASE + data.VirtualAddress; hi = lo + data.Misc_VirtualSize
registered = sorted(G.REGISTRY.values())
def rd(a, fmt): return struct.unpack(fmt, bytes(E.mu.mem_read(a, struct.calcsize(fmt))))
def on_callback(mu, addr, size, _):
    ctx = mu.reg_read(X.UC_X86_REG_RCX); counter = rd(ctx, '<Q')[0]
    E.mu.mem_write(counter, struct.pack('<H', rd(counter, '<H')[0] + 1))
    rsp = mu.reg_read(X.UC_X86_REG_RSP)
    mu.reg_write(X.UC_X86_REG_RIP, rd(rsp, '<Q')[0]); mu.reg_write(X.UC_X86_REG_RSP, rsp + 8)
E.mu.hook_add(UC_HOOK_CODE, on_callback, begin=0x141369C30, end=0x141369C30)
for init in (0x140031340, 0x14003A060, 0x14003A070, 0x14003A110, 0x1401997A0, 0x140199890, 0x1401998A0, 0x1401998B0, 0x1401998C0, 0x140199930, 0x140199990, 0x1401999D0):
    E.call(init, ())
written, zero_reads = set(), {}
def on_write(mu, access, addr, size, value, _):
    for a in range(addr, addr + size): written.add(a)
def on_read(mu, access, addr, size, value, _):
    if addr in written: return
    if any(bytes(mu.mem_read(addr, size))): return
    rip = mu.reg_read(X.UC_X86_REG_RIP)
    zero_reads.setdefault(addr & ~0xF, set()).add(rip)
E.mu.hook_add(UC_HOOK_MEM_WRITE, on_write, begin=lo, end=hi)
E.mu.hook_add(UC_HOOK_MEM_READ, on_read, begin=lo, end=hi)
slot = E.alloc(0x280); E.call(0x1404C5620, (slot,)); offset = E.alloc(0x10)
E.call(0x141367810, ((0 << 32) | (0x5C << 24) | (0x6DC << 12) | 0x00C, slot, offset))
def owner(a):
    regs = [r for r in registered if r <= a]
    return f'globals@{regs[-1]:X}+{a - regs[-1]:X}' if regs and a - regs[-1] < 0x20000 else ''
for a in sorted(zero_reads):
    print(f'{a:X} read-as-zero by {", ".join(f"{r:X}" for r in sorted(zero_reads[a])[:4])} {owner(a)}')
print('total', len(zero_reads))
