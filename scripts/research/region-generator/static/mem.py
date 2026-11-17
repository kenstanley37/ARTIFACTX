# READ-ONLY NMS memory helpers (access 0x410). VA() maps an NMS.exe VA (image base 0x140000000) to the live address.
import ctypes, ctypes.wintypes as W, struct, subprocess
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = W.HANDLE
k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
_o = subprocess.check_output(['powershell', '-NoProfile', '-Command', '$p = Get-Process NMS; "$($p.Id) $($p.MainModule.BaseAddress.ToInt64())"']).split()
pid, base = int(_o[0]), int(_o[1])
h = k32.OpenProcess(0x410, False, pid)
def read(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got)); return buf.raw[:got.value]
VA = lambda va: va - 0x140000000 + base
u32 = lambda a: struct.unpack('<I', read(a, 4))[0]
i32 = lambda a: struct.unpack('<i', read(a, 4))[0]
u64 = lambda a: struct.unpack('<Q', read(a, 8))[0]
f32 = lambda a, n=1: struct.unpack(f'<{n}f', read(a, 4 * n))
def cstr(a, n=128):
    b = read(a, n); return b.split(b'\0')[0].decode('latin1')
def hexdump(a, n):
    b = read(a, n)
    for i in range(0, len(b), 16):
        row = b[i:i + 16]
        print(f'+{i:04x} {row.hex(" ")}  {"".join(chr(c) if 32 <= c < 127 else "." for c in row)}  {struct.unpack_from("<4f", row) if len(row) == 16 else ""}')
