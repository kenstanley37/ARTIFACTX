# Every globals file at the address the game registers it at (0x142e18d40 calls), as
# the running game has them after startup - zero in the executable file.
import os, emu_sys as E
REGISTRY = {
 'terrain': 0x144f38910, 'smoketestoptions': 0x144f3a1f0, 'sceneoptions': 0x144f3c070, 'scratchpad': 0x144f3c630,
 'simulation': 0x144f3eb10, 'multiplayer': 0x144f4b2a0, 'effects': 0x144f5bb60, 'fishing': 0x144f5ef80,
 'character': 0x144f65520, 'collisiontable': 0x144f698f8, 'camera': 0x144f699f0, 'atlas': 0x144f77280,
 'audio': 0x144f77470, 'buildableship': 0x144f78e90, 'vehicle': 0x1451ea600, 'ui': 0x14520b190,
 'solargeneration': 0x145215050, 'spaceship': 0x145224bc0, 'gravitygun': 0x145234ef0, 'navigation': 0x145235030,
 'player': 0x145249330, 'graphics': 0x14524d320, 'freighterbase': 0x14524e620, 'galaxy': 0x14524e6c0,
 'gameplay': 0x14525f6e0, 'environment': 0x1452613a0, 'fleet': 0x145261af0, 'debugoptions': 0x14526d330,
 'building': 0x14527f080, 'aispaceship': 0x145289140,
}
loaded = []
ADDRS = sorted(REGISTRY.values()) + [0x14527cbe0, 0x145289140 + 0x10000]
def room(addr):  # space up to the next registered instance - a file holds its lists after the struct
    return min(a for a in ADDRS + [0x14527cbe0, 0x14527f080] if a > addr) - addr
for f in sorted(os.listdir('globals')):
    key = f.replace('gc', '', 1).replace('globals.global.mbin', '').replace('.global.mbin', '')
    if key not in REGISTRY: print('no address for', f); continue
    ptr = E.load_mbin(os.path.join('globals', f)); n = os.path.getsize(os.path.join('globals', f)) - 0x20
    E.mu.mem_write(REGISTRY[key], bytes(E.mu.mem_read(ptr, min(n, room(REGISTRY[key]))))); loaded.append(key)
