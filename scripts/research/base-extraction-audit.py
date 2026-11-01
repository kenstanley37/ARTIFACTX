"""Read-only audit of saved extraction equipment against local game table dumps.

Create dumps with the existing DataCataloger dumpfile command:
  dumpfile basebuildingobjectstable.mbin 6 5000
  dumpfile regionhotspots 5 500

These are research inputs, not shipped game data. --resource is an explicitly
confirmed resource at the selected base, NOT an automatic hotspot prediction.
The upper-word storage interpretation remains a candidate until checked against
the game's display. Never adds candidate amounts to the player's inventory.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re


def text(path):
    raw = path.read_bytes()
    return raw.decode('utf-16' if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')


def definitions(buildings, hotspots):
    parts = {}
    for block in buildings.split('(GcBaseBuildingEntry)')[1:]:
        def number(field):
            match = re.search(r'\n\s+' + field + r':\s*\n\s+\(Int32\) (\d+)', block)
            return int(match[1]) if match else 0
        identity = re.search(r'\n          ID:\s*\n\s+\(wrapped string, NMSString0x10\) "([^"]+)"', block)
        dependency = re.search(r'DependsOnHotspots:\s*\n\s+\(DependsOnHotspotsEnum\) (\w+)', block)
        if identity and dependency and (dependency[1] in ('Mineral', 'Gas') or
                                       (number('NetworkMask') == 2 and number('Storage') > 0)):
            parts[identity[1]] = dict(kind=dependency[1], storage=number('Storage'), rate=number('Rate'))
    yields = {}
    for block in hotspots.split('(GcRegionHotspotSubstance)')[1:]:
        match = re.search(r'SubstanceId:\s*\n\s+\(wrapped string, NMSString0x10\) "([^"]+)".*?'
                          r'AmountCost:\s*\n\s+\(Int32\) (\d+).*?'
                          r'SubstanceYeild:\s*\n\s+\(Int32\) (\d+)', block, re.S)
        if match:
            yields[match[1]] = (int(match[2]), int(match[3]))
    if not parts or not yields:
        raise ValueError('Required definitions missing; check dump depth, list cap and game version.')
    return parts, yields


def audit(save, parts, yields, base_name, resource):
    raw = save.read_bytes()
    root = json.loads(raw.decode('utf-16' if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig').rstrip('\0'))
    context = '2YS' if root.get('XTp') == 'Season' and isinstance(root.get('2YS', {}).get('6f='), dict) else 'vLc'
    bases = root.get(context, {}).get('6f=', {}).get('F?0', [])
    matches = [base for base in bases if base.get('NKm', '').casefold() == base_name.casefold()]
    if len(matches) != 1:
        raise ValueError('Select exactly one base by its full name; no match or ambiguous name.')
    cost, produced = yields[resource]
    if cost <= 0 or produced <= 0:
        raise ValueError('Invalid resource yield definition.')
    objects = []
    for index, obj in enumerate(matches[0].get('@ZJ', [])):
        identity = obj.get('r<7', '').lstrip('^')
        if identity not in parts:
            continue
        definition = parts[identity]
        packed = obj.get('CVX', 0)
        if not isinstance(packed, int) or not 0 <= packed < 2**64:
            raise ValueError('Unexpected packed object state.')
        upper = packed >> 32
        objects.append(dict(index=index, part=identity, kind=definition['kind'], position=obj.get('wMC'),
                            timestamp=obj.get('b1:'), packed=str(packed), lower_word=packed & 0xffffffff,
                            upper_word=upper, catalog_storage=definition['storage'],
                            catalog_rate=definition['rate'],
                            candidate_stored=upper * produced / cost,
                            candidate_capacity=definition['storage'] * produced / cost))
    return dict(save_sha256=hashlib.sha256(raw).hexdigest(), context=context, base=base_name,
                resource=resource, resource_source='Explicit operator confirmation; not inferred from save',
                interpretation='Candidate upper-word storage. Not a verified inventory total or production rate.',
                equipment_count=len(objects), candidate_stored=sum(o['candidate_stored'] for o in objects),
                candidate_capacity=sum(o['candidate_capacity'] for o in objects), objects=objects)


def compare(previous, current):
    """Compare an unchanged equipment set, not an inferred connected network."""
    if any(previous[key] != current[key] for key in ('context', 'base', 'resource')):
        raise ValueError('Base, resource or save context changed; growth comparison is not valid.')
    identity = lambda row: [(o['index'], o['part'], o['position'], o['lower_word']) for o in row['objects']]
    if identity(previous) != identity(current) or not current['objects']:
        raise ValueError('Equipment or lower-word state changed; growth comparison is not valid.')
    times = [{o['timestamp'] for o in row['objects']} for row in (previous, current)]
    if any(len(t) != 1 or None in t for t in times):
        raise ValueError('Equipment timestamps differ; cannot infer one elapsed interval.')
    elapsed = next(iter(times[1])) - next(iter(times[0]))
    if elapsed <= 0:
        raise ValueError('Previous save must precede the current equipment timestamp.')
    growth = current['candidate_stored'] - previous['candidate_stored']
    return dict(elapsed_seconds=elapsed, candidate_stock_growth=growth,
                observed_base_total_growth_per_hour=growth * 3600 / elapsed,
                scope='Observed base equipment total; not an automatic connected-network or future production rate')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--save', type=Path, required=True, help='Decrypted JSON save; read only')
    parser.add_argument('--building-dump', type=Path, required=True)
    parser.add_argument('--hotspot-dump', type=Path, required=True)
    parser.add_argument('--base-name', required=True)
    parser.add_argument('--resource', required=True, help='Confirmed catalog item ID for this base')
    parser.add_argument('--previous-save', type=Path, help='Optional earlier decrypted save of the same equipment')
    args = parser.parse_args()
    parts, yields = definitions(text(args.building_dump), text(args.hotspot_dump))
    result = audit(args.save, parts, yields, args.base_name, args.resource)
    if args.previous_save:
        result['comparison'] = compare(audit(args.previous_save, parts, yields, args.base_name, args.resource), result)
    print(json.dumps(result, indent=2))
