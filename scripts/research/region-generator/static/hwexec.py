# Owner-approved live trace (2026-11-10): attaches to NMS as a debugger, puts a hardware WRITE watch (8 bytes) on one
# address in every thread, and when the game writes there records RIP, registers and the stack's return addresses
# into NMS.exe. Never writes game memory; always clears the debug registers and detaches (game keeps running).
# usage: python hwwatch.py 0xADDRESS seconds out.json [maxhits]
import ctypes, ctypes.wintypes as W, struct, sys, time, json, subprocess
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
targets_va = [int(x, 16) for x in sys.argv[1].split(',')]; secs, out = float(sys.argv[2]), sys.argv[3]
maxhits = int(sys.argv[4]) if len(sys.argv) > 4 else 8
o = subprocess.check_output(['powershell', '-NoProfile', '-Command', '$p = Get-Process NMS; "$($p.Id) $($p.MainModule.BaseAddress.ToInt64())"']).split()
pid, base = int(o[0]), int(o[1])
VA = lambda live: live - base + 0x140000000
LIVE = lambda va: va - 0x140000000 + base
targets = [LIVE(v) for v in targets_va][:4]
addr = targets[0]

THREAD_ALL = 0x1F03FF
CTX_DEBUG = 0x00100010; CTX_FULLDBG = 0x0010001F
class DEBUG_EVENT(ctypes.Structure):
    _fields_ = [('code', W.DWORD), ('pid', W.DWORD), ('tid', W.DWORD), ('pad', W.DWORD), ('u', ctypes.c_ubyte * 0xA0)]
k32.OpenThread.restype = W.HANDLE
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
k32.GetThreadContext.argtypes = [W.HANDLE, ctypes.c_void_p]; k32.SetThreadContext.argtypes = [W.HANDLE, ctypes.c_void_p]
hproc = k32.OpenProcess(0x0410, False, pid)
def read(a, n):
    b = ctypes.create_string_buffer(n); g = ctypes.c_size_t()
    k32.ReadProcessMemory(hproc, ctypes.c_void_p(a), b, n, ctypes.byref(g)); return b.raw[:g.value]
raw = ctypes.create_string_buffer(0x4D0 + 16)
ctx_addr = (ctypes.addressof(raw) + 15) & ~15
def ctx_bytes(): return ctypes.string_at(ctx_addr, 0x4D0)
def ctx_get(off, fmt='<Q'): return struct.unpack_from(fmt, ctypes.string_at(ctx_addr + off, 8))[0]
def ctx_set(off, v, fmt='<Q'): ctypes.memmove(ctx_addr + off, struct.pack(fmt, v), struct.calcsize(fmt))

def set_dr(tid, enable):
    h = k32.OpenThread(THREAD_ALL, False, tid)
    if not h: return False
    k32.SuspendThread(h)
    ctypes.memset(ctx_addr, 0, 0x4D0); ctx_set(0x30, CTX_DEBUG, '<I')
    ok = k32.GetThreadContext(h, ctx_addr)
    if ok:
        if enable:
            dr7 = ctx_get(0x70) & ~0xFFFF00FF
            for k, t in enumerate(targets):
                ctx_set(0x48 + 8 * k, t); dr7 |= 1 << (2 * k)   # Lk, execute (RW 00, LEN 00)
            ctx_set(0x70, dr7)
        else:
            for k in range(4): ctx_set(0x48 + 8 * k, 0)
            ctx_set(0x70, ctx_get(0x70) & ~0xFFFF00FF)
        ctx_set(0x68, 0)
        ok = k32.SetThreadContext(h, ctx_addr)
    k32.ResumeThread(h); k32.CloseHandle(h); return bool(ok)

def threads():
    TH32CS_SNAPTHREAD = 4
    class TE(ctypes.Structure):
        _fields_ = [('dwSize', W.DWORD), ('cntUsage', W.DWORD), ('th32ThreadID', W.DWORD), ('th32OwnerProcessID', W.DWORD),
                    ('tpBasePri', W.LONG), ('tpDeltaPri', W.LONG), ('dwFlags', W.DWORD)]
    k32.CreateToolhelp32Snapshot.restype = W.HANDLE
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0); te = TE(); te.dwSize = ctypes.sizeof(TE); ids = []
    if k32.Thread32First(snap, ctypes.byref(te)):
        while True:
            if te.th32OwnerProcessID == pid: ids.append(te.th32ThreadID)
            if not k32.Thread32Next(snap, ctypes.byref(te)): break
    k32.CloseHandle(snap); return ids

hits = []; skipped = [0]
if not k32.DebugActiveProcess(pid): raise SystemExit(f'attach failed {ctypes.get_last_error()}')
k32.DebugSetProcessKillOnExit(False)
armed = False
ev = DEBUG_EVENT()
try:
    end = time.time() + secs
    print(f'attached to {pid}; executing watches {[hex(v) for v in targets_va]}; {secs:.0f} s', flush=True)
    while time.time() < end and len(hits) < maxhits:
        if not k32.WaitForDebugEvent(ctypes.byref(ev), 200):
            if not armed:
                n = sum(set_dr(t, True) for t in threads()); armed = True; print(f'armed {n} threads', flush=True)
            continue
        status = 0x00010002   # DBG_CONTINUE
        if ev.code == 1:   # EXCEPTION
            code = struct.unpack_from('<I', bytes(ev.u), 0)[0]
            if code == 0x80000004:   # single step: our hardware watch
                h = k32.OpenThread(THREAD_ALL, False, ev.tid)
                ctypes.memset(ctx_addr, 0, 0x4D0); ctx_set(0x30, CTX_FULLDBG, '<I'); k32.GetThreadContext(h, ctx_addr)
                regs = {n: ctx_get(off) for n, off in (('rax', 0x78), ('rcx', 0x80), ('rdx', 0x88), ('rbx', 0x90), ('rsp', 0x98),
                        ('rbp', 0xA0), ('rsi', 0xA8), ('rdi', 0xB0), ('r8', 0xB8), ('r9', 0xC0), ('r10', 0xC8), ('r11', 0xD0),
                        ('r12', 0xD8), ('r13', 0xE0), ('r14', 0xE8), ('r15', 0xF0), ('rip', 0xF8))}
                dr6 = ctx_get(0x68)
                stack = read(regs['rsp'], 0x800); qs = struct.unpack(f'<{len(stack) // 8}Q', stack)
                rets = [(8 * i, VA(v)) for i, v in enumerate(qs) if base <= v < base + 0x8000000][:24]
                value = dr6 & 0xF
                entry = {r: read(regs[r], 0x60).hex() for r in ('rcx', 'rdx', 'r8', 'rbx', 'rax', 'rsi', 'rdi') if 0x10000 < regs[r] < 0x7FF000000000}
                hit = {'tid': ev.tid, 'rip_va': VA(regs['rip']), 'dr6': dr6, 'value': value, 'regs': regs,
                       'regs_va': {k: VA(v) for k, v in regs.items() if base <= v < base + 0x8000000},
                       'stack_rets': rets, 'stack_raw': stack.hex(), 'entry': entry.hex()}
                if not any(v[0x60:0x76] == '4255494c44424541434f4e' for v in entry.values()):
                    skipped[0] += 1
                else:
                    hits.append(hit)
                if hits and hits[-1] is hit: print(f'HIT {len(hits)}: rip {hit["rip_va"]:#x} value {value:#x} rets {[hex(r) for _, r in rets[:6]]}', flush=True)
                ctx_set(0x68, 0); ctx_set(0x44, ctx_get(0x44, '<I') | 0x10000, '<I')   # RF: resume past the breakpoint
                k32.SetThreadContext(h, ctx_addr); k32.CloseHandle(h)
            elif code != 0x80000003:
                status = 0x80010001   # DBG_EXCEPTION_NOT_HANDLED: the game's own exception
        elif ev.code == 2 and armed:   # new thread
            set_dr(ev.tid, True)
        elif ev.code == 5:   # process exit
            print('game exited'); break
        k32.ContinueDebugEvent(ev.pid, ev.tid, status)
finally:
    n = sum(set_dr(t, False) for t in threads())
    # drain pending events once so nothing is left suspended
    while k32.WaitForDebugEvent(ctypes.byref(ev), 100):
        code = struct.unpack_from('<I', bytes(ev.u), 0)[0] if ev.code == 1 else 0
        k32.ContinueDebugEvent(ev.pid, ev.tid, 0x80010001 if ev.code == 1 and code not in (0x80000003, 0x80000004) else 0x00010002)
    k32.DebugActiveProcessStop(pid)
    print(f'cleared {n} threads, detached; {len(hits)} beacon hits, {skipped[0]} other hits', flush=True)
    json.dump(hits, open(out, 'w'))
