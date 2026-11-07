"""Read-only: system indices in one region from a decrypted save's discovery records.
usage: python -I regiontruth.py <save.hg.json> <galaxy> <x hex> <y hex> <z hex>"""
import json, sys
path, galaxy, rx, ry, rz = sys.argv[1], int(sys.argv[2]), int(sys.argv[3], 16), int(sys.argv[4], 16), int(sys.argv[5], 16)
root = json.loads(open(path, encoding='utf-8-sig').read().rstrip('\0'))
records = root['fDu']['ETO']['OsQ']['?fB']
def num(v):
    return int(v, 16) if isinstance(v, str) and v.startswith('0x') else int(v)
systems, kinds = set(), {}
for r in records:
    a = num(r['8P3']['5L6'])
    kind = r['8P3'].get('<Dn')
    kinds[kind] = kinds.get(kind, 0) + 1
    x, z, y = a & 0xFFF, (a >> 12) & 0xFFF, (a >> 24) & 0xFF
    g, s = (a >> 32) & 0xFF, (a >> 40) & 0xFFF
    if (g, x, y, z) == (galaxy, rx, ry, rz):
        systems.add(s)
print('record kinds', kinds)
print(f'region systems seen: {len(systems)}  max {max(systems) if systems else None}')
print(sorted(systems))
