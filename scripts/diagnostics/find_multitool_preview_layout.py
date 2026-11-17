"""Finds MultiToolPreviewRuntimeService's BuildLayout values in the running NMS.exe after a game update.

Read-only: OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ) and ReadProcessMemory only. No injection,
no remote calls, no writes. Run with the game loaded into a save (the controller search needs a live player).
Prints one C# `new(...)` row to paste into MultiToolPreviewRuntimeService.Layouts; check every "FOUND" line first.
Python standard library only.
First used 2026-11-17 for build 185034.
"""
import ctypes as C, hashlib, json, re, struct, subprocess, sys
from ctypes import wintypes as W

k = C.WinDLL('kernel32', use_last_error=True)
k.OpenProcess.argtypes = [W.DWORD, W.BOOL, W.DWORD]; k.OpenProcess.restype = W.HANDLE
k.ReadProcessMemory.argtypes = [W.HANDLE, C.c_void_p, C.c_void_p, C.c_size_t, C.POINTER(C.c_size_t)]
k.ReadProcessMemory.restype = W.BOOL
k.CloseHandle.argtypes = [W.HANDLE]

command = ("$p=Get-Process NMS -ErrorAction SilentlyContinue | Select-Object -First 1; if ($p) { @{Id=$p.Id;"
           "Base=$p.MainModule.BaseAddress.ToInt64();Size=$p.MainModule.ModuleMemorySize;Path=$p.MainModule.FileName}"
           " | ConvertTo-Json -Compress }")
out = subprocess.run(['powershell', '-NoProfile', '-NonInteractive', '-Command', command], capture_output=True, text=True).stdout
if not out.strip(): sys.exit('NMS is not running.')
info = json.loads(out); base = int(info['Base']); size = int(info['Size'])
handle = k.OpenProcess(0x410, False, info['Id'])
if not handle: raise C.WinError(C.get_last_error())


def read(address, length):
    buffer = C.create_string_buffer(length); got = C.c_size_t()
    if not k.ReadProcessMemory(handle, C.c_void_p(address), buffer, length, C.byref(got)) or got.value != length:
        raise IOError(hex(address))
    return buffer.raw


def u64(a): return struct.unpack('<Q', read(a, 8))[0]
def i32(a): return struct.unpack('<i', read(a, 4))[0]
def rel(img, at): return struct.unpack('<i', img[at:at + 4])[0]


def image():
    parts = []
    for offset in range(0, size, 0x100000):
        length = min(0x100000, size - offset)
        try: parts.append(read(base + offset, length))
        except IOError:
            chunk = bytearray(length)
            for page in range(0, length, 0x1000):
                try: chunk[page:page + 0x1000] = read(base + offset + page, 0x1000)
                except IOError: pass
            parts.append(bytes(chunk))
    return b''.join(parts)


def one(label, matches):
    if len(matches) != 1: sys.exit(f'{label}: expected one match, found {len(matches)} - trace it by hand.')
    print('FOUND', label, hex(matches[0]))
    return matches[0]


img = image()
exe_hash = hashlib.sha256(open(info['Path'], 'rb').read()).hexdigest().upper()

# 1. Weapon owner call: mov rcx,[rip+manager]; mov r8,r15; add rcx,OWNER; xor edx,edx
site = one('owner call', [m.start() for m in re.finditer(rb'\x48\x8b\x0d....\x4d\x8b\xc7\x48\x81\xc1....\x33\xd2', img, re.S)])
manager_global = site + 7 + rel(img, site + 3)
owner_offset = struct.unpack('<I', img[site + 13:site + 17])[0]
owner_bytes = img[site:site + 19].hex().upper()

# 2. Animation controller pointer inside the owner: the one whose table holds valid animation names.
owner = u64(base + manager_global) + owner_offset
controllers = []
for offset in range(0, 0x4000, 8):
    try:
        controller = u64(owner + offset)
        if not 0x10000 < controller < 0x7FFFFFFFFFFF: continue
        count = i32(controller + 0xF0 + 0x1920)
        if not 0 < count <= 2048 or i32(controller + 0xC0) != count: continue
        entries = u64(controller + 0xF0 + 0x1908)
        names = [read(u64(entries + i * 8) + 0x50, 16).split(b'\0')[0] for i in range(count)]
        if all(n and re.fullmatch(rb'[A-Z0-9_]+', n) for n in names): controllers.append((offset, names))
    except IOError: pass
controller_offset = one('controller offset', [o for o, _ in controllers])
names = controllers[0][1]
print('   animations:', len(names), 'OVERHEAT present:', any(n.startswith(b'OVERHEAT') for n in names))

# 3. Duration function: table bound check, entry Has30Hz test, 1/30 or 1/60 period, times playback speed.
duration = one('duration function', [m.start() for m in re.finditer(
    rb'\x48\x8b\xc1\x3b\x91\x20\x19\x00\x00\x7c\x04\x0f\x57\xc0\xc3\x48\x8b\x80\x08\x19\x00\x00\x48\x63\xca'
    rb'\x48\x8b\x14\xc8\x80\xba\x23\x01\x00\x00\x00\x74\x0a\xf3\x0f\x10\x15....\xeb\x08\xf3\x0f\x10\x15....'
    rb'\xf3\x0f\x10\x05....\xf3\x0f\x59\x82\xdc\x00\x00\x00', img, re.S)])
# rip-relative targets: displacement at +0x2A/+0x34/+0x3C, each instruction ending 4 bytes later.
period30 = duration + 0x2E + rel(img, duration + 0x2A)
period60 = duration + 0x38 + rel(img, duration + 0x34)
playback = duration + 0x40 + rel(img, duration + 0x3C)
print('   1/30', struct.unpack('<f', img[period30:period30 + 4])[0], '1/60', struct.unpack('<f', img[period60:period60 + 4])[0],
      'playback', struct.unpack('<f', img[playback:playback + 4])[0])

# 4. Illegal-module override: active-mode test, two season flags, override flag, all compared with bl (zero),
#    followed in the same function by the forced-best byte global (cmp byte [rip+X], sil).
mode_global = manager_global + 0xC
pattern = (rb'\x8b\x05(....)\x85\xc0\x74\x09\x83\xf8\x06\x0f\x85....\x48\x8b\x05....'
           rb'\x38\x98(....)\x75.\x38\x98(....)\x0f\x84....\x38\x98(....)\x0f\x84')
sites = [m for m in re.finditer(pattern, img, re.S) if m.start() + 6 + rel(img, m.start() + 2) == mode_global]
branch = one('illegal-module override', [m.start() for m in sites])
flag_a, flag_b, flag_override = (struct.unpack('<I', sites[0].group(i))[0] for i in (2, 3, 4))
forced = [m.start() for m in re.finditer(rb'\x40\x38\x35....', img[branch:branch + 0x200], re.S)]
forced_global = one('forced-best global', [branch + f + 7 + rel(img, branch + f + 3) for f in forced])

print(f'''
        new("{exe_hash}", BUILD,
            0x{site:X}, "{owner_bytes}", 0x{manager_global:X}, 0x{owner_offset:X}, 0x{controller_offset:X},
            0x{playback:X}, 0x{period60:X}, 0x{period30:X},
            0x{branch:X}, "{img[branch:branch + 8].hex().upper()}", 0x{forced_global:X}, 0x{flag_a:X}, 0x{flag_b:X}, 0x{flag_override:X}),''')
k.CloseHandle(handle)
