# Step-by-step Python port of NMS.exe build 180836's region star generator 0x141367810,
# checked against the emulator at checkpoints. usage: python regionport.py <galaxy> <x> <y> <z> (hex)
import os, sys, struct
SCR = os.path.dirname(os.path.abspath(__file__))
galaxy, X_, Y_, Z_ = int(sys.argv[1]), int(sys.argv[2], 16), int(sys.argv[3], 16), int(sys.argv[4], 16)
ADDRESS = (galaxy << 32) | (Y_ << 24) | (Z_ << 12) | X_

# ---------------- emulator side: record the MWC state at checkpoints ----------------
sys.argv = [sys.argv[0], 'unused.hg.json']  # the shared header only needs a path
exec(compile(open(os.path.join(SCR, 'regionbatch.py')).read().split('\nroot = json.loads')[0], 'regionbatch.py', 'exec'))
checks = {}
def checkpoint(name):
    def hook(mu, addr, size, _):
        if name in checks: return
        rsp = mu.reg_read(X.UC_X86_REG_RSP)
        checks[name] = rd(rsp + 0x30, '<II')
    return hook
for name, at in (('seeded', 0x141367B57), ('after_setup_draw', 0x141367CE3), ('fixed_loop2', 0x141368071),
                 ('after_fixed', 0x141368267), ('grid_start', 0x141368650)):
    E.mu.hook_add(UC_HOOK_CODE, checkpoint(name), begin=at, end=at)
emitted = region_stars(galaxy, X_, Y_, Z_)
print('emulator', {k: (hex(v[0]), hex(v[1])) for k, v in checks.items()}, 'N', sum(1 for i in emitted if i < 1000))

# ---------------- port ----------------
class MWC:
    def __init__(self, address):
        lo = address & 0xFFFFFFFF; hi = address >> 32
        self.lo = lo if lo else 1
        self.carry = (((lo >> 16) | (lo << 16)) & 0xFFFFFFFF) ^ hi ^ lo
    def word(self):
        v = self.lo * 0x5A76F899 + self.carry
        self.lo, self.carry = v & 0xFFFFFFFF, v >> 32
        return self.lo
    def state(self): return (self.lo, self.carry)
r = MWC(ADDRESS)
port = {'seeded': r.state()}
r.word(); port['after_setup_draw'] = r.state()
port['fixed_loop2'] = r.state()
for _ in range(122): r.word(); r.word()
port['after_fixed'] = r.state()
for k in port:
    if k in checks: print(f'{k:18} port {port[k][0]:08X}/{port[k][1]:08X}  emu {checks[k][0]:08X}/{checks[k][1]:08X}  {"OK" if port[k] == checks[k] else "DIFF"}')

def draws_between(a, b, limit=100000):
    m = MWC(1); m.lo, m.carry = a
    for k in range(limit + 1):
        if (m.lo, m.carry) == b: return k
        m.word()
    return None
names = ['seeded', 'after_setup_draw', 'fixed_loop2', 'after_fixed', 'grid_start']
for a, b in zip(names, names[1:]):
    print(f'draws {a} -> {b}: {draws_between(checks[a], checks[b])}')
