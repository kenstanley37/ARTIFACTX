# Traces the spiral-arm step of the region generator for one region: the setup values, then for every
# grid star that reaches the spiral check its inclusion inputs, arm angle, arm point and final position.
# usage: python spiraltrace.py <galaxy> <x hex> <y hex> <z hex> [max stars]
import os, sys, struct
EMU = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu')
DATA = os.environ.get('NMS_EMU_DATA') or sys.exit('set NMS_EMU_DATA (see README.md)')
os.chdir(DATA); sys.path.insert(0, EMU)
import emu_sys as E
import globals as _globals  # noqa: F401
from unicorn import UC_HOOK_CODE
from unicorn import x86_const as X
for init in (0x140031340, 0x14003A060, 0x14003A070, 0x14003A110, 0x1401997A0, 0x140199890, 0x1401998A0, 0x1401998B0, 0x1401998C0, 0x140199930, 0x140199990, 0x1401999D0):
    E.call(init, ())
galaxy, x, y, z = int(sys.argv[1]), int(sys.argv[2], 16), int(sys.argv[3], 16), int(sys.argv[4], 16)
limit = int(sys.argv[5]) if len(sys.argv) > 5 else 12
mu = E.mu
def f32(a): return struct.unpack('<f', bytes(mu.mem_read(a, 4)))[0]
def v4(a): return struct.unpack('<4f', bytes(mu.mem_read(a, 16)))
def xmm(reg): return struct.unpack('<4f', mu.reg_read(reg).to_bytes(16, 'little'))
def rbp(): return mu.reg_read(X.UC_X86_REG_RBP)
def rsp(): return mu.reg_read(X.UC_X86_REG_RSP)
state = dict(stars=0, setup=False)
def on_setup(mu, addr, size, _):
    if state['setup']: return
    state['setup'] = True
    b = rbp()
    print('setup armStep', f32(b + 0x928), 'inclusion', f32(b - 0x5C), 'twist', f32(b - 0x54), 'flex', f32(b - 0x58))
    for off in (0x230, 0x240, 0x250, 0x90, 0x140, 0x260):
        print(f'  row +{off:X}', v4(b + off))
def on_check(mu, addr, size, _):
    if state['stars'] >= limit: return
    b, s = rbp(), rsp()
    roll, chance = mu.reg_read(X.UC_X86_REG_RSI), mu.reg_read(X.UC_X86_REG_R14)
    print(f'star check: r {f32(s + 0x54)!r} band {xmm(X.UC_X86_REG_XMM3)[0]!r} inclusion {f32(b - 0x5C)!r} roll {roll} chance {chance} moved {xmm(X.UC_X86_REG_XMM10)[:3]}')
def on_angle(mu, addr, size, _):
    if state['stars'] >= limit: return
    print(f'  angle {xmm(X.UC_X86_REG_XMM7)[0]!r} outer {xmm(X.UC_X86_REG_XMM8)[0]!r}')
def on_point(mu, addr, size, _):
    if state['stars'] >= limit: return
    print(f'  point {xmm(X.UC_X86_REG_XMM1)[:3]}  D0 {v4(rbp() + 0x610)[:3]} D1 {v4(rbp() + 0x620)[:3]} D2 {v4(rbp() + 0x630)[:3]} v {xmm(X.UC_X86_REG_XMM9)[:3]}')
def on_done(mu, addr, size, _):
    if state['stars'] >= limit: return
    state['stars'] += 1
    print(f'  final {xmm(X.UC_X86_REG_XMM10)[:3]}')
mu.hook_add(UC_HOOK_CODE, on_setup, begin=0x141368650, end=0x141368650)
mu.hook_add(UC_HOOK_CODE, on_check, begin=0x141368BB5, end=0x141368BB5)
mu.hook_add(UC_HOOK_CODE, on_angle, begin=0x141368C35, end=0x141368C35)
mu.hook_add(UC_HOOK_CODE, on_point, begin=0x141368F1D, end=0x141368F1D)
mu.hook_add(UC_HOOK_CODE, on_done, begin=0x141369092, end=0x141369092)
slot = E.alloc(0x280); E.call(0x1404C5620, (slot,)); offset = E.alloc(0x10)
E.call(0x141367810, ((galaxy << 32) | (y << 24) | (z << 12) | x, slot, offset))
