# Port of the game's L-system expander (cTkLSystemManager, build 184271) for planet building layouts, and the stored
# sample's bounds that give a building record its radius. docs/research/PLANETARY_MULTITOOL_LOCATIONS.md
# ("L-system expander ported").
#  - rules: templates merged first, the file on top; rules match by model; Balance appends new models to an inner rule,
#    Prioritize puts them first, Replace removes the inner rule and appends the new one; the first BaseRule is the root;
#  - chain restrictions (0x142D768B0): each AtLeast global adds AtLeast 1 to its single reachable producer, up the tree;
#  - GenerateStep 0x142D79260: breadth-first queue; per inner rule, each active locator (a direct child of the node, or a
#    child of its Interior_ locator; upper-cased name containing the LocatorType) gets one pick (0x142D851D0: a forced
#    minimum, else one MWC draw on the manager's running state) and is switched off; the child is aligned to its own
#    matching locator (opposite _IN_/_OUT_ type, any type for plain ones), which is switched off too;
#  - bounds: each node model's meshes (scene MESH AABB attributes, mesh-local) flattened in the model's own frame,
#    referenced scenes included, then that box's corners through the node; Euler order X then Y then Z (R = Rx Ry Rz).
# Checked 2026-11-06 against the 8 stored Vy'keen L-systems x 3 samples of Kaim 30/19 (read live, read-only): 24/24
# layouts, 1,056/1,056 nodes in order, positions 0.0000 m, rotations 2e-6; radii 22/24 exact, the shelter's other two
# within 5 mm (21.383 / 21.260 vs 21.379 / 21.255).
# Data: scene/L-system JSON from the LsysDump tool (full game paths) in LSYS_DATA.
# usage: python lsysport.py [lsystem path] [seed hex]    (prints the layout and its radius)
import json, math, os, sys
import numpy as np

G = os.environ.get('LSYS_DATA', '')
M = 0x5A76F899
F32 = np.float32
MIN_RADIUS = 10.0          # GcBuildingGlobals.MinRadius (+0xC8C), build 184271


def gpath(p):
    return p.replace('\\', '/').upper()


def load(p):
    with open(os.path.join(G, gpath(p) + '.json')) as f:
        return json.load(f)


# ---------------------------------------------------------------- scenes
def rot(rx, ry, rz):
    rx, ry, rz = (math.radians(a) for a in (rx, ry, rz))
    X = np.array([[1, 0, 0], [0, math.cos(rx), -math.sin(rx)], [0, math.sin(rx), math.cos(rx)]])
    Y = np.array([[math.cos(ry), 0, math.sin(ry)], [0, 1, 0], [-math.sin(ry), 0, math.cos(ry)]])
    Z = np.array([[math.cos(rz), -math.sin(rz), 0], [math.sin(rz), math.cos(rz), 0], [0, 0, 1]])
    return X @ Y @ Z


def local(t):
    m = np.eye(4)
    m[:3, :3] = rot(t['RotX'], t['RotY'], t['RotZ']) @ np.diag([t['ScaleX'], t['ScaleY'], t['ScaleZ']])
    m[:3, 3] = [t['TransX'], t['TransY'], t['TransZ']]
    return m


class Scene:
    """Candidate locators in scene order (direct children of the model, and children of an Interior_ locator; transform
    relative to the scene root), mesh AABBs and references (flattened transforms)."""

    def __init__(self, path):
        self.path = gpath(path)
        self.locators, self.meshes, self.refs = [], [], []
        for c in load(path)['Children'] or []:
            self._walk(c, np.eye(4), 1, '')

    def _walk(self, n, parent, depth, parent_name):
        m = parent @ local(n['Transform'])
        a = {x['Name']: x['Value'] for x in n['Attributes']}
        if n['Type'] == 'LOCATOR' and (depth == 1 or (depth == 2 and parent_name.upper() == 'INTERIOR_')):
            self.locators.append((n['Name'], m))
        elif n['Type'] == 'MESH' and 'AABBMINX' in a:
            self.meshes.append((m, np.array([float(a['AABBMINX']), float(a['AABBMINY']), float(a['AABBMINZ'])]),
                                np.array([float(a['AABBMAXX']), float(a['AABBMAXY']), float(a['AABBMAXZ'])])))
        elif n['Type'] == 'REFERENCE' and 'SCENEGRAPH' in a:
            self.refs.append((gpath(a['SCENEGRAPH']), m))
        for c in n['Children'] or []:
            self._walk(c, m, depth + 1, n['Name'])


_scenes = {}


def scene(path):
    p = gpath(path)
    if p not in _scenes:
        _scenes[p] = Scene(p)
    return _scenes[p]


# ---------------------------------------------------------------- rules
RESTRICTION = {'NoMoreThan': 0, 'AtLeast': 1, 'AtLeastIfICan': 2}


class Entry:
    def __init__(self, model, p, restr):
        self.model, self.p, self.restr = gpath(model), F32(p), restr


class Inner:
    def __init__(self, loc, entries):
        self.loc, self.entries = loc.upper(), entries


class Rule:
    def __init__(self, name, model):
        self.name, self.model, self.inner = name, gpath(model), []


def restrictions(lst):
    return [(RESTRICTION[r['Restriction']], r['Amount']) for r in lst]


class RuleSet:
    def __init__(self, path):
        self.rules, self.globals, self.root = [], {}, None     # globals: model -> [(type, amount)]
        self._merge(gpath(path))
        self.root = self.root or self.rules[0]
        self._chain()

    def rule_for(self, model):
        return next((r for r in self.rules if r.model == model), None)

    def _merge(self, path):
        d = load(path)
        for t in d['Templates']:
            self._merge(gpath(t['LSystem']))
        for g in d['GlobalRestriction']:
            rs = restrictions(g['Restrictions'])
            if rs or gpath(g['Model']) not in self.globals:   # an empty list keeps the template's (shop nRooms: unchecked)
                self.globals[gpath(g['Model'])] = rs
        for r in d['Rules']:
            rule = self.rule_for(gpath(r['Model']))
            if rule is None:
                rule = Rule(r['Name'], r['Model'])
                self.rules.append(rule)
            if r['RuleType'] == 'BaseRule' and self.root is None:
                self.root = rule
            for ir in r['Rules']:
                entries = [Entry(e['Model'], e['Probability'], restrictions(e['Restrictions'])) for e in ir['Entries']]
                loc, merge = ir['LocatorType'].upper(), ir['MergeProbabilityOptions']
                old = next((x for x in rule.inner if x.loc == loc), None)
                if merge == 'Replace' or old is None:
                    if old is not None:
                        rule.inner.remove(old)
                    rule.inner.append(Inner(loc, entries))
                else:
                    new = [e for e in entries if not any(x.model == e.model for x in old.entries)]
                    if merge == 'Prioritize':
                        old.entries[:0] = new
                    else:
                        old.entries.extend(new)

    def _chain(self):
        reach, stack = {self.root.model}, [self.root.model]
        while stack:
            r = self.rule_for(stack.pop())
            for e in (e for ir in (r.inner if r else []) for e in ir.entries):
                if e.model not in reach:
                    reach.add(e.model)
                    stack.append(e.model)
        todo, seen = [m for m, rs in self.globals.items() if any(t == 1 and a > 0 for t, a in rs)], set()
        while todo:
            m = todo.pop(0)
            if m in seen:
                continue
            seen.add(m)
            producers = [r for r in self.rules if r.model in reach and any(e.model == m for ir in r.inner for e in ir.entries)]
            if len(producers) != 1 or producers[0] is self.root:
                continue
            self.globals.setdefault(producers[0].model, []).append((1, 1))
            todo.append(producers[0].model)


# ---------------------------------------------------------------- expansion
class Node:
    def __init__(self, model, world, parent):
        self.model, self.world, self.parent, self.children = model, world, parent, []
        self.off = set()                    # locator indices switched off


def locator_type(name):
    u = name.upper()
    return 0 if '_IN_' in u else (1 if '_OUT_' in u else 2)


def mwc_start(seed):
    lo, hi = seed & 0xFFFFFFFF, seed >> 32
    return [lo or 1, (((lo >> 16) | (lo << 16)) & 0xFFFFFFFF) ^ hi ^ lo]


def pick(rs, ir, node, counts, st):
    """0x142D851D0: the first entry below a local or global minimum, else one draw r = u * 100 over the candidates
    (entries under both maxima), each weighted p + pooled / n, pooled = the excluded entries' p; -1 = nothing."""
    cand, pooled = [], F32(0)
    for i, e in enumerate(ir.entries):
        lmax = next((a for t, a in reversed(e.restr) if t == 0), 1000000)
        gmax = next((a for t, a in reversed(rs.globals.get(e.model, [])) if t == 0), 1000000)
        lmin = sum(a for t, a in e.restr if t != 0)
        gmin = sum(a for t, a in rs.globals.get(e.model, []) if t != 0)
        lc = sum(1 for c in node.children if c.model == e.model)
        gc = counts.get(e.model, 0)
        if lc < lmin or gc < gmin:
            return i
        if lc < lmax and gc < gmax:
            cand.append(i)
        else:
            pooled = F32(pooled + e.p)
    if not cand:
        return -1
    s = st[0] * M + st[1]
    st[0], st[1] = s & 0xFFFFFFFF, s >> 32
    r = F32(F32(st[0] * 2.0 ** -32) * F32(100.0))
    cum = F32(0)
    for i in cand:
        cum = F32(cum + F32(F32(pooled / F32(len(cand))) + ir.entries[i].p))
        if r < cum:
            return i
    return -1


def generate(path, seed):
    """One stored sample: the layout for this seed (sample n of a record uses seed lo + 100 n). Root at the origin."""
    rs = RuleSet(path)
    st, counts = mwc_start(seed), {}
    root = Node(rs.root.model, np.eye(4), None)
    nodes, queue = [root], [root]
    while queue:
        node = queue.pop(0)
        rule = rs.rule_for(node.model)
        if rule is None:
            continue
        locators = scene(node.model).locators
        for ir in rule.inner:
            for li, (name, m) in enumerate(locators):
                if li in node.off or ir.loc not in name.upper():
                    continue
                node.off.add(li)
                i = pick(rs, ir, node, counts, st)
                if i < 0:
                    continue
                e = ir.entries[i]
                child = Node(e.model, node.world @ m, node)
                want = {0: 1, 1: 0, 2: 2}[locator_type(name)]
                for ci, (cn, cm) in enumerate(scene(e.model).locators):
                    if ir.loc in cn.upper() and (want == 2 or locator_type(cn) == want):
                        child.world = node.world @ m @ np.linalg.inv(cm)
                        child.off.add(ci)
                        break
                node.children.append(child)
                counts[e.model] = counts.get(e.model, 0) + 1
                nodes.append(child)
                if rs.rule_for(e.model):
                    queue.append(child)
    return nodes


# ---------------------------------------------------------------- bounds
def corners(lo, hi):
    return np.array([[x, y, z, 1.0] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])])


def _mesh_points(path, M4, out):
    try:
        sc = scene(path)
    except FileNotFoundError:          # effects and props outside the building folders: no mesh AABBs we use
        return
    for m, lo, hi in sc.meshes:
        out.append((corners(lo, hi) @ (M4 @ m).T)[:, :3])
    for r, m in sc.refs:
        _mesh_points(r, M4 @ m, out)


_model_box = {}


def model_box(path):
    if path not in _model_box:
        pts = []
        _mesh_points(path, np.eye(4), pts)
        _model_box[path] = (np.vstack(pts).min(0), np.vstack(pts).max(0)) if pts else None
    return _model_box[path]


def bounds(nodes):
    pts = [(corners(*model_box(n.model)) @ n.world.T)[:, :3] for n in nodes if model_box(n.model) is not None]
    p = np.vstack(pts)
    return p.min(0), p.max(0)


def radius(lo, hi):
    """Building record radius (+0xC0) without an OverrideRadius: half the bounds' XZ diagonal, at least MinRadius."""
    return max(0.5 * float(np.hypot(hi[0] - lo[0], hi[2] - lo[2])), MIN_RADIUS)


def short(m):
    return m.split('/')[-1].replace('.SCENE.MBIN', '')


if __name__ == '__main__':
    path = sys.argv[1] if len(sys.argv) > 1 else 'MODELS/PLANETS/BIOMES/COMMON/BUILDINGS/SHOP/SHOPWARRIOR.LSYSTEM.MBIN'
    seed = int(sys.argv[2], 16) if len(sys.argv) > 2 else 0xC9BC0BFCE72F16EF
    nodes = generate(path, seed)
    lo, hi = bounds(nodes)
    print(f'{len(nodes)} nodes; bounds {np.round(lo, 3)} .. {np.round(hi, 3)}; radius {radius(lo, hi):.3f}; '
          f'root offset (re-centred on the XZ bounds) ({-(lo[0] + hi[0]) / 2:.3f}, 0, {-(lo[2] + hi[2]) / 2:.3f})')
    for k, n in enumerate(nodes):
        p = n.world[:3, 3]
        print(f'  {k:2} {short(n.model):40} ({p[0]:8.3f}, {p[1]:8.3f}, {p[2]:8.3f})')
