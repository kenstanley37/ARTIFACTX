# READ-ONLY diagnostic (docs/research/PLANETARY_MULTITOOL_LOCATIONS.md, "L-system expander ported"). Run while NMS is
# open on a planet: dumps the L-system manager's stored building layouts (build 184271) to JSON for lsyscheck.py.
#   python scripts/diagnostics/read_lsystem_samples.py <out.json>
# Manager: static object 0x1472E8AF0 (returned by 0x140334DB0); stored list +0x170, 0x2D0 bytes per L-system: path at +0,
# seed +0x100, sample seeds +0x140 (stride 0x10, seed lo + 0/100/200), XZ diagonals +0x170 (3 floats), samples +0x188
# (stride 0x30: node transforms (0x40 each) +0, node count +0xC, resource handles +0x10, flags +0x20), bounds +0x210
# (min, max per sample, stride 0x20). Handles -> resource table 0x146EFD7E0 (+0x60, entry [h & 0x7FFFF - 1], name +0xC).
# Query/read access only (0x410), no writes.
import ctypes, ctypes.wintypes as W, json, struct, subprocess, sys
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
out = subprocess.check_output(['powershell', '-NoProfile', '-Command',
                               '$p = Get-Process NMS; "$($p.Id) $($p.MainModule.BaseAddress.ToInt64())"']).split()
pid, base = int(out[0]), int(out[1])
h = k32.OpenProcess(0x410, False, pid)


def read(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got))
    return buf.raw[:got.value]


va = lambda a: a - 0x140000000 + base
u64 = lambda a: struct.unpack('<Q', read(a, 8))[0]
i32 = lambda a: struct.unpack('<i', read(a, 4))[0]
f32 = lambda a, n: list(struct.unpack(f'<{n}f', read(a, 4 * n)))
cstr = lambda a, n=0x100: read(a, n).split(b'\0')[0].decode('latin1')

table = u64(u64(va(0x146EFD7E0)) + 0x60)
name = lambda hd: cstr(u64(table - 8 + (hd & 0x7FFFF) * 8) + 0xC)
stored = u64(va(0x1472E8AF0) + 0x170)
result = []
for i in range(256):
    r = stored + i * 0x2D0
    path = cstr(r, 0x80)
    if not path.upper().endswith('.LSYSTEM.MBIN'):
        break
    ls = {'path': path, 'seed': f'{u64(r + 0x100):016X}', 'diag': f32(r + 0x170, 3), 'samples': []}
    for n in range(3):
        s = r + 0x188 + 0x30 * n
        count, T, H = i32(s + 0xC), u64(s), u64(s + 0x10)
        handles = struct.unpack(f'<{count}I', read(H, 4 * count))
        ls['samples'].append({'seed': f'{u64(r + 0x140 + 0x10 * n):016X}', 'bmin': f32(r + 0x210 + 0x20 * n, 4),
                              'bmax': f32(r + 0x220 + 0x20 * n, 4),
                              'nodes': [{'model': name(hd), 'm': f32(T + 0x40 * k, 16)} for k, hd in enumerate(handles)]})
    result.append(ls)
json.dump(result, open(sys.argv[1], 'w'), indent=0)
print(len(result), 'stored L-systems:', ', '.join(f"{l['path'].split('/')[-1]} ({l['seed']})" for l in result))
