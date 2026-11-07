"""Read-only build-180836 diagnostic. No injection, remote calls, or writes."""
import ctypes as C,json,subprocess,struct
from ctypes import wintypes as W

def main():
 command="Get-Process NMS -ErrorAction SilentlyContinue | Select-Object -First 1 Id,@{Name='Base';Expression={$_.MainModule.BaseAddress.ToInt64()}} | ConvertTo-Json -Compress"
 result=subprocess.run(['powershell','-NoProfile','-NonInteractive','-Command',command],capture_output=True,text=True)
 if not result.stdout.strip(): print('NMS is not running or is not visible to this diagnostic.');return
 process=json.loads(result.stdout);base=int(process['Base'])
 k=C.WinDLL('kernel32',use_last_error=True)
 k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE
 k.ReadProcessMemory.argtypes=[W.HANDLE,C.c_void_p,C.c_void_p,C.c_size_t,C.POINTER(C.c_size_t)];k.ReadProcessMemory.restype=W.BOOL
 k.CloseHandle.argtypes=[W.HANDLE]
 handle=k.OpenProcess(0x410,False,process['Id'])
 if not handle:raise C.WinError(C.get_last_error())
 try:
  def read(address,size):
   data=C.create_string_buffer(size);got=C.c_size_t()
   if not k.ReadProcessMemory(handle,address,data,size,C.byref(got)) or got.value!=size:raise C.WinError(C.get_last_error())
   return data.raw
  def u64(address):return struct.unpack('<Q',read(address,8))[0]
  def i32(address):return struct.unpack('<i',read(address,4))[0]
  # Build signature guard: runtime maximum must start with its traced prologue.
  if read(base+0x140f400,8)!=bytes.fromhex('488bc44889580848'):raise RuntimeError('Executable differs from the traced build; no structure reads performed.')
  manager=u64(base+0x6e8d708);owner=manager+0x72cad0;controller=u64(owner+0x23d0)
  if not controller:print('Current weapon has no animation controller.');return
  table=controller+0xf0;count=i32(table+0x1920)
  if not 0<=count<=2048:raise RuntimeError('Unexpected animation count; stopping.')
  entries=u64(table+0x1908);rows=[];names=[]
  for index in range(count):
   entry=u64(entries+index*8)
   if not entry:continue
   name=read(entry+0x50,16).split(b'\0')[0].decode('ascii',errors='replace')
   names.append(name)
   if name not in ('OVERHEAT','OVERHEAT_3P'):continue
   rows.append(dict(animation=name,frameEnd=i32(entry+0xc4),frameEndGame=i32(entry+0xc8),frameStart=i32(entry+0xcc),speed=struct.unpack('<f',read(entry+0xdc,4))[0]))
  if not names or any(not name or not all(c.isalnum() or c=='_' for c in name) for name in names):raise RuntimeError('Animation names failed layout validation; stopping.')
  print(json.dumps(dict(sampleAnimationNames=names[:8],primaryMode=i32(owner+0xf74),animationCount=count,overheat=rows),indent=2))
 finally:k.CloseHandle(handle)
try:main()
except Exception as error:print('Read-only diagnostic unavailable:',error)
