# Checks the simplified emission rule against the emulator: per-cell density (output of the
# fBm call at 0x1413687D3) compared with both thresholds predicts the region's star count.
# usage: python cellrule.py <galaxy> <x> <y> <z>   (hex region words)
import os, sys, struct
SCR = os.path.dirname(os.path.abspath(__file__))
galaxy, X_, Y_, Z_ = int(sys.argv[1]), int(sys.argv[2], 16), int(sys.argv[3], 16), int(sys.argv[4], 16)
sys.argv = [sys.argv[0], 'unused.hg.json']
exec(compile(open(os.path.join(SCR, 'regionbatch.py')).read().split('\nroot = json.loads')[0], 'regionbatch.py', 'exec'))

def xf(mu, reg): return struct.unpack('<f', mu.reg_read(reg).to_bytes(16, 'little')[:4])[0]
cells, context = [], {}
def on_density(mu, addr, size, _):
    cells.append(xf(mu, X.UC_X86_REG_XMM0))
def on_threshold(mu, addr, size, _):
    rbp = mu.reg_read(X.UC_X86_REG_RBP)
    context.setdefault('base', rd(rbp + 0x590, '<f')[0])
    context.setdefault('star', rd(rbp + 0x5D4, '<f')[0])
    context.setdefault('spiral', (mu.reg_read(X.UC_X86_REG_RSI), mu.reg_read(X.UC_X86_REG_R14)))
E.mu.hook_add(UC_HOOK_CODE, on_density, begin=0x1413687D8, end=0x1413687D8)
E.mu.hook_add(UC_HOOK_CODE, on_threshold, begin=0x1413686A0, end=0x1413686A0)
emitted = region_stars(galaxy, X_, Y_, Z_)
n = sum(1 for i in emitted if i < 1000)
rsi, r14 = context['spiral']
factor = struct.unpack('<f', E.pe.get_data(0x141369116 + 8 + 0x37c078a - E.BASE, 4))[0]
threshold = context['star'] * (factor if rsi < r14 else 1.0)
predicted = 122 + sum(1 for d in cells if d > context['base'] and d > threshold)
print(f'cells {len(cells)} base {context["base"]} star {context["star"]} spiral roll {rsi} < {r14}: {rsi < r14} factor {factor}')
print(f'emulator N {n}  rule N {predicted}  {"MATCH" if n == predicted else "DIFF"}')
