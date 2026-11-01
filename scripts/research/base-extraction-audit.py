"""Read-only audit of saved extraction equipment against local game table dumps.

Create dumps with the existing DataCataloger dumpfile command:
  dumpfile basebuildingobjectstable.mbin 9 5000
  dumpfile regionhotspots 5 500

These are research inputs, not shipped game data. --resource is an explicitly
confirmed resource at the selected base, NOT an automatic hotspot prediction.
The upper-word storage interpretation remains a candidate until checked against
the game's display. Never adds candidate amounts to the player's inventory.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re


def text(path):
    raw = path.read_bytes()
    return raw.decode('utf-16' if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')


def definitions(buildings, hotspots):
    parts = {}
    for block in buildings.split('(GcBaseBuildingEntry)')[1:]:
        connection = block.split('DependentConnections:', 1)[0]
        def number(field):
            match = re.search(r'\n\s+' + field + r':\s*\n\s+\(Int32\) (\d+)', block)
            return int(match[1]) if match else 0
        identity = re.search(r'\n          ID:\s*\n\s+\(wrapped string, NMSString0x10\) "([^"]+)"', block)
        dependency = re.search(r'DependsOnHotspots:\s*\n\s+\(DependsOnHotspotsEnum\) (\w+)', block)
        network = re.search(r'\(LinkNetworkTypeEnum\) (\w+)', connection)
        resource_network = re.search(r'\(LinkNetworkTypeEnum\) Resources', connection)
        if identity and dependency and (resource_network or (network is None and
                (dependency[1] in ('Mineral', 'Gas') or (number('NetworkMask') == 2 and number('Storage') > 0)))):
            distance = re.search(r'ConnectionDistance:\s*\n\s+\(Single\) ([\d.eE+-]+)', connection)
            parts[identity[1]] = dict(kind=dependency[1], storage=number('Storage'), rate=number('Rate'),
                                     mask=number('NetworkMask'), group=number('NetworkSubGroup'),
                                     distance=float(distance[1]) if distance else 0,
                                     min_distance='(Boolean) True' in connection.split('UseMinDistance:', 1)[-1],
                                     network_decoded=bool(resource_network))
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


def candidate_networks(objects, parts):
    """Provisional geometry audit; not a port of the game's network traversal.

    For the reference pipeline, At is a displacement from Position, not a unit
    facing vector. Non-storage resource connectors are treated as two endpoints.
    Main-connection masks/subgroups prevent joining nearby equipment directly.
    Threshold combination and endpoint interpretation need independent tests.
    """
    nodes = []
    for index, obj in enumerate(objects):
        identity = obj.get('r<7', '').lstrip('^')
        if identity not in parts or not parts[identity]['network_decoded']:
            continue
        rule = parts[identity]
        if rule['kind'] == 'None' and rule['rate'] != 0:
            continue  # Other producers (including freighter rooms) need their own adapter.
        position = obj.get('wMC')
        if not isinstance(position, list) or len(position) != 3 or not all(math.isfinite(x) for x in position):
            raise ValueError('Invalid resource-network position.')
        endpoints = [position]
        if rule['storage'] == 0 and rule['rate'] == 0:
            delta = obj.get('aNu')
            if not isinstance(delta, list) or len(delta) != 3 or not all(math.isfinite(x) for x in delta):
                raise ValueError('Invalid connector displacement.')
            endpoints.append([a + b for a, b in zip(position, delta)])
        nodes.append(dict(index=index, rule=rule, endpoints=endpoints))
    parent = list(range(len(nodes)))

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    for i, left in enumerate(nodes):
        for j in range(i):
            right = nodes[j]
            a, b = left['rule'], right['rule']
            if not (a['mask'] & (1 << b['group']) and b['mask'] & (1 << a['group'])):
                continue
            threshold = min(a['distance'], b['distance']) if a['min_distance'] or b['min_distance'] else a['distance'] + b['distance']
            if any(math.dist(p, q) < threshold for p in left['endpoints'] for q in right['endpoints']):
                parent[find(i)] = find(j)
    groups = {}
    for i, node in enumerate(nodes):
        groups.setdefault(find(i), []).append(node)
    return [dict(equipment_indices=[n['index'] for n in group if n['rule']['storage'] > 0],
                 connector_indices=[n['index'] for n in group if n['rule']['storage'] == 0],
                 catalog_storage=sum(n['rule']['storage'] for n in group))
            for group in groups.values()]


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
        if identity not in parts or parts[identity]['storage'] <= 0 or (parts[identity]['kind'] == 'None' and parts[identity]['rate'] != 0):
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
    networks = candidate_networks(matches[0].get('@ZJ', []), parts)
    stored_units = sum(o['upper_word'] for o in objects)
    capacity_units = sum(o['catalog_storage'] for o in objects)
    return dict(save_sha256=hashlib.sha256(raw).hexdigest(), context=context, base=base_name,
                resource=resource, resource_source='Explicit operator confirmation; not inferred from save',
                interpretation='Candidate upper-word storage. Not a verified inventory total or production rate.',
                equipment_count=len(objects), candidate_stored=sum(o['candidate_stored'] for o in objects),
                candidate_capacity=sum(o['candidate_capacity'] for o in objects), objects=objects,
                native_displayed_base_stock=stored_units // cost * produced,
                native_displayed_base_capacity=capacity_units // cost * produced,
                candidate_networks=networks,
                network_warning='Provisional graph; disconnected resources are not identified. No automatic supply totals.')


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
