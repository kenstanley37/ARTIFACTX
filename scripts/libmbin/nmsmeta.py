"""Reads the structure metadata NMS.exe carries for every game data class (name, NameHash, GUID, size, fields).
Shared by the libMBIN update tools (docs: scripts/libmbin/README.md). Static file reads only."""
import os, struct, pefile
EXE = os.environ.get("NMS_EXE", r"D:\SteamLibrary\steamapps\common\No Man's Sky\Binaries\NMS.exe")
CLASS_STRIDE, FIELD_STRIDE = 0x28, 0x58

class Image:
    def __init__(self, path=EXE):
        self.pe = pefile.PE(path, fast_load=True)
        self.base = self.pe.OPTIONAL_HEADER.ImageBase
        self.secs = [(self.base + s.VirtualAddress, s.SizeOfRawData, s.get_data(), s.Name.rstrip(b'\0').decode()) for s in self.pe.sections]
    def read(self, va, n):
        for start, size, data, _ in self.secs:
            if start <= va < start + size:
                return data[va - start:va - start + n]
        return None
    def ptr_ok(self, va): return self.read(va, 1) is not None
    def cstr(self, va):
        b = self.read(va, 256)
        return b.split(b'\0')[0].decode('ascii', 'replace') if b else None
    def section(self, name): return next(s for s in self.secs if s[3] == name)

def is_name(img, va):
    s = img.cstr(va) if 0x140000000 < va < 0x150000000 else None
    return bool(s) and s.replace('_', '').isalnum() and s[0].isalpha()

def classes(img):
    """All class records: walks .rdata for 0x28-byte records whose name is a 'cGc..'/'cTk..' string."""
    start, size, data, _ = img.section('.rdata')
    out = {}
    for off in range(0, size - CLASS_STRIDE, 8):
        name_ptr, hash_, guid, fields, count, ssize = struct.unpack_from('<QQQQII', data, off)
        if not (0x140000000 < name_ptr < 0x150000000) or hash_ >> 32 or count > 5000 or not (0 < ssize < 0x1000000): continue
        if not (0x140000000 < fields < 0x150000000) and count: continue
        name = img.cstr(name_ptr)
        if not name or not (name.startswith('cGc') or name.startswith('cTk')): continue
        out[name[1:]] = dict(va=start + off, name=name[1:], hash=hash_, guid=guid, fields=fields, count=count, size=ssize)
    return out

def fields(img, cls):
    out = []
    for i in range(cls['count']):
        raw = img.read(cls['fields'] + i * FIELD_STRIDE, FIELD_STRIDE)
        if raw is None or len(raw) < FIELD_STRIDE: return None   # field list not in the file (filled at run time)
        q = struct.unpack("<11Q", raw)
        out.append(dict(name=img.cstr(q[0]), raw=raw))
    return out


class LiveImage(Image):
    """Image that falls back to the running game's memory (read-only: PROCESS_QUERY_INFORMATION | PROCESS_VM_READ)
    for addresses whose bytes only exist at run time (some classes' field lists are filled by static initialisers).
    Pointers read from memory are converted back to file addresses."""
    def __init__(self, path=EXE):
        super().__init__(path)
        import ctypes, ctypes.wintypes as W, subprocess
        self.k32 = ctypes.WinDLL('kernel32', use_last_error=True); self.ct = ctypes
        self.k32.OpenProcess.restype = W.HANDLE
        self.k32.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
        pid = int(subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process NMS).Id']).strip())
        self.h = self.k32.OpenProcess(0x410, False, pid)
        psapi = ctypes.WinDLL('psapi'); mods = (ctypes.c_void_p * 1)(); need = W.DWORD()
        psapi.EnumProcessModulesEx(self.h, mods, ctypes.sizeof(mods), ctypes.byref(need), 3)
        self.delta = mods[0] - self.base
        self.limit = self.pe.OPTIONAL_HEADER.SizeOfImage
    def read(self, va, n):
        b = super().read(va, n)
        if b is not None and len(b) == n: return b
        if not (self.base <= va < self.base + self.limit): return b
        buf = self.ct.create_string_buffer(n); got = self.ct.c_size_t()
        if not self.k32.ReadProcessMemory(self.h, self.ct.c_void_p(va + self.delta), buf, n, self.ct.byref(got)): return b
        raw = bytearray(buf.raw)
        lo, hi = self.base + self.delta, self.base + self.delta + self.limit
        for o in range(0, n - 7, 8):   # relocated pointers back to file addresses
            q = struct.unpack_from('<Q', raw, o)[0]
            if lo <= q < hi: struct.pack_into('<Q', raw, o, q - self.delta)
        return bytes(raw)
