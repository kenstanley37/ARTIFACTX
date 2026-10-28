"""Generates libMBIN C# class files from the structure metadata in NMS.exe (nmsmeta.py), in libMBIN's own style.
usage:
  python gen.py selftest            regenerate every class whose GUID still matches libMBIN and compare the field lines
  python gen.py diff                list classes changed / new in the game compared with libMBIN
  python gen.py write [Class ...]   (re)write the changed and new classes (or the named ones) into libMBIN's source tree
Static reads of NMS.exe only."""
import os, re, sys, struct
sys.path.insert(0, os.path.dirname(__file__))
from nmsmeta import Image, LiveImage, classes, fields
import libsrc

SIMPLE = {0x1: 'bool', 0x2: 'byte', 0x4: 'NMSTemplate', 0x5: 'LinkableNMSTemplate', 0x6: 'Colour', 0x8: 'VariableSizeString',
          0xA: 'HashedString', 0xC: 'GcFilename', 0xE: 'float', 0x10: 'NMSString0x10', 0x11: 'NMSString0x20A',
          0x12: 'NMSString0x20A', 0x13: 'sbyte', 0x14: 'short', 0x15: 'int', 0x16: 'long', 0x17: 'GcNodeID',
          0x18: 'GcResource', 0x19: 'GcSeed', 0x1B: 'NMSString0x20', 0x1C: 'NMSString0x40', 0x1D: 'NMSString0x80',
          0x1E: 'NMSString0x100', 0x1F: 'NMSString0x200', 0x20: 'NMSString0x400', 0x21: 'NMSString0x800', 0x23: 'ushort',
          0x24: 'uint', 0x25: 'ulong', 0x27: 'Vector2f', 0x28: 'Vector3f', 0x29: 'Vector4f', 0x2B: 'halfVector4',
          0x2C: 'Vector4i', 0x2F: 'Colour32'}
CLASS, ENUM, FLAGS, LIST, ARRAY, HASHMAP = 0x3, 0xB, 0xD, 0x7, 0x1A, 0x2E
CS_KEYWORDS = set('abstract as base bool break byte case catch char checked class const continue decimal default delegate do '
                  'double else enum event explicit extern false finally fixed float for foreach goto if implicit in int '
                  'interface internal is lock long namespace new null object operator out override params private protected '
                  'public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true '
                  'try typeof uint ulong unchecked unsafe ushort using virtual void volatile while'.split())


class Meta:
    def __init__(self):
        self.img = LiveImage() if os.environ.get('NMS_LIVE') else Image()
        self.cls = classes(self.img)
        self.byva = {c['va']: n for n, c in self.cls.items()}
        self.lib = libsrc.load()
        self._enum_owner = None

    def recs(self, name):
        out = []
        for i, f in enumerate(fields(self.img, self.cls[name]) or []):
            w = struct.unpack('<22I', f['raw']); q = struct.unpack('<11Q', f['raw'])
            out.append(dict(index=i, name=f['name'], hash=w[2], type=w[3], inner=w[4], esize=w[5], count=w[6],
                            offset=w[7], cls=self.byva.get(q[4]), enum=q[6] if w[14] else 0, enumcount=w[14]))
        return out

    def enum_values(self, va, count):
        vals = []
        for i in range(count):
            v, p = struct.unpack('<qQ', self.img.read(va + 16 * i, 16))
            vals.append((self.img.cstr(p), v))
        return vals

    def enum_owner(self, va):
        """The wrapper class whose own field declares this enum (for EnumType = typeof(Wrapper.XEnum))."""
        if self._enum_owner is None:
            self._enum_owner = {}
            for n in self.cls:
                for r in self.recs(n):
                    if r['type'] in (ENUM, FLAGS) and r['enum']:
                        self._enum_owner.setdefault(r['enum'], (n, enum_name(self, n, r)))
        return self._enum_owner.get(va)


def ident(name):
    return re.sub(r'[^0-9A-Za-z_]', '', name)


# Types libMBIN spells differently from the plain mapping; kept when a field already exists in libMBIN.
QUIRKS = {('GcFilename', 'VariableSizeString'), ('List<ushort>', 'List<Quaternion>')}


def lib_field(m, cls, fname):
    """The libMBIN field for a game field: same name, a renamed one (Name_Type, MxmlName) or None."""
    if cls not in m.lib: return None
    key = ident(fname)
    fs = m.lib[cls]['fields']
    return (next((f for f in fs if f['name'] == key), None)
            or next((f for f in fs if f['attr'].get('MxmlName') == f'"{fname}"'), None)
            or next((f for f in fs if f['name'].startswith(key + '_')), None))


def cs_name(m, cls, r):
    lf = lib_field(m, cls, r['name'])
    if lf: return lf['name']
    n = ident(r['name'])
    return n[2:] if n == cls else n      # C# forbids a member named like its class: libMBIN drops Gc/Tk


def enum_name(m, cls, r):
    """libMBIN's existing name for the enum of a field when it has one, else FieldNameEnum."""
    lf = lib_field(m, cls, r['name'])
    if lf and re.fullmatch(r'\w+Enum', lf['type']): return lf['type']
    n = ident(r['name'])
    return (n[2:] if n == cls else n) + 'Enum'


def ctype(m, cls, r, own_enums):
    t = r['type']
    if t == CLASS: ty = r['cls']
    elif t in (ENUM, FLAGS):
        name = enum_name(m, cls, r)
        own_enums.append((name, r['enum'], r['enumcount'], t == FLAGS, r['esize'])); ty = name
    elif t in (LIST, ARRAY):
        inner = r['cls'] if r['inner'] == CLASS else SIMPLE.get(r['inner'], f'UNKNOWN_{r["inner"]:X}')
        ty = f'List<{inner}>' if t == LIST else f'{inner}[]'
    elif t == HASHMAP: ty = f'HashMap<{r["cls"]}>'
    else: ty = SIMPLE.get(t, f'UNKNOWN_{t:X}')
    ty = ty or 'UNRESOLVED'
    lf = lib_field(m, cls, r['name'])
    if lf and ((ty, lf['type']) in QUIRKS or ty == 'None' or '<None>' in ty or ty.startswith(('UNKNOWN', 'UNRESOLVED')) or 'UNRESOLVED' in ty): ty = lf['type']
    return ty


def attr(m, cls, r, own_enums):
    parts = [f'Index = {r["index"]}']
    if r['type'] == ARRAY:
        parts.append(f'Size = 0x{r["count"]:X}')
        lf = lib_field(m, cls, r['name'])
        existing = lf['attr'].get('EnumType') if lf else None
        if not r['enum'] and existing:
            parts.append(f'EnumType = {existing}')
        elif r['enum']:
            own = m.enum_owner(r['enum'])
            if existing and '.' in existing:
                parts.append(f'EnumType = {existing}')
            elif own and not existing:
                parts.append(f'EnumType = typeof({own[0]}.{own[1]})')
            else:   # an index enum no wrapper class declares: libMBIN declares it inside this class
                local = existing[7:-1] if existing else ident(r['name']) + 'Enum'
                own_enums.append((local, r['enum'], r['enumcount'], False, 4))
                parts.append(f'EnumType = typeof({local})')
    lf = lib_field(m, cls, r['name'])
    if lf and 'KeyField' in lf['attr']:
        parts.append(f'KeyField = {lf["attr"]["KeyField"]}')
    if ident(r['name']) != r['name'] or r['name'][:1].isdigit():
        parts.append(f'MxmlName = "{r["name"]}"')
    return f'[NMS({", ".join(parts)})]'


def field_lines(m, name):
    recs = sorted(m.recs(name), key=lambda r: (r['offset'], r['index']))
    width = len(f'{max((r["offset"] for r in recs), default=0):X}')
    own, lines, seen = [], [], set()
    for r in recs:
        before = len(own)
        ty = ctype(m, name, r, own)
        a = attr(m, name, r, own)
        for e in own[before:]:          # libMBIN declares an enum just before the field that uses it
            if e[0] not in seen:
                seen.add(e[0]); lines += enum_block(m, *e, cls=name)
        lines.append(f'        {a}')
        lines.append(f'        /* 0x{r["offset"]:0{width}X} */ public {ty} {cs_name(m, name, r)};')
    return own, lines


def enum_block(m, name, va, count, flags, esize=4, cls=None):
    vals = [(('@' + n if n in CS_KEYWORDS else n) if n else 'None', v) for n, v in m.enum_values(va, count)]
    prev = m.lib.get(cls, {}).get('text', '') if cls else ''
    if re.search(r'\[Flags\]\s*public enum ' + re.escape(name) + r'', prev): flags = True
    bits = [v for _, v in vals if v]
    if not flags and vals and vals[0] == ('None', 0) and bits and all(v & (v - 1) == 0 for v in bits) and len(vals) > 2             and any(v != i for i, (_, v) in enumerate(vals)):
        flags = True
    out = [f'        // size: 0x{len(vals):X}']
    if flags: out.append('        [Flags]')
    under = {1: 'byte', 2: 'ushort', 8: 'ulong'}.get(esize, 'uint')
    out.append(f'        public enum {name} : {under} {{')
    seq = all(v == i for i, (_, v) in enumerate(vals))
    for n, v in vals:
        out.append(f'            {n} = 0x{v:X},' if flags else (f'            {n},' if seq else f'            {n} = {v},'))
    out.append('        }')
    return out


def namespace_for(name, m):
    if name in m.lib:
        return os.path.basename(os.path.dirname(m.lib[name]['file']))
    if name.endswith('Globals'): return 'Globals'
    return 'Toolkit' if name.startswith('Tk') else 'GameComponents'


def render(m, name):
    c = m.cls[name]
    own, lines = field_lines(m, name)
    ns = namespace_for(name, m)
    body = lines
    text = '\n'.join(body)
    usings = re.findall(r'^using [^\r\n]+;', m.lib[name]['text'], re.M) if name in m.lib else []
    def need(u):
        if u not in usings: usings.append(u)
    if '[Flags]' in text: need('using System;')
    if 'List<' in text: need('using System.Collections.Generic;')
    if ns != 'Toolkit' and re.search(r'\bTk\w+', text): need('using libMBIN.NMS.Toolkit;')
    if ns != 'GameComponents' and re.search(r'\bGc\w+', text): need('using libMBIN.NMS.GameComponents;')
    head = '\n'.join(usings) + ('\n\n' if usings else '')
    return (f'{head}namespace libMBIN.NMS.{ns}\n{{\n    [NMS(GUID = 0x{c["guid"]:X}, NameHash = 0x{c["hash"]:X})]\n'
            f'    public class {name} : NMSTemplate\n    {{\n{text}\n    }}\n}}\n')


def lib_field_lines(m, name):
    out = []
    for ln in m.lib[name]['text'].split('\n'):
        if (libsrc.ATTR.match(ln) and 'GUID =' not in ln) or libsrc.FIELD.match(ln): out.append(ln.rstrip())
    return out


def selftest(m):
    same = diff = 0; examples = []
    for n, c in m.cls.items():
        if n not in m.lib or m.lib[n]['guid'] != c['guid'] or fields(m.img, c) is None: continue
        ours = [l.rstrip() for l in field_lines(m, n)[1] if libsrc.ATTR.match(l) or libsrc.FIELD.match(l)]
        theirs = lib_field_lines(m, n)
        if ours == theirs: same += 1
        else:
            diff += 1
            if len(examples) < 12:
                d = next(((a, b) for a, b in zip(ours, theirs) if a != b), (len(ours), len(theirs)))
                examples.append((n, d))
    print(f'unchanged classes: field lines identical {same}, different {diff}')
    for n, d in examples: print(' ', n, '\n     ours  :', d[0], '\n     libMBIN:', d[1])


def changed(m):
    return sorted(n for n, c in m.cls.items() if n not in m.lib or m.lib[n]['guid'] != c['guid'])


if __name__ == '__main__':
    m = Meta()
    if sys.argv[1] == 'selftest': selftest(m)
    elif sys.argv[1] == 'diff':
        for n in changed(m): print(n, 'NEW' if n not in m.lib else 'changed')
    elif sys.argv[1] == 'write':
        names = sys.argv[2:] or changed(m)
        for n in names:
            if fields(m.img, m.cls[n]) is None:
                print('SKIPPED (field list only exists at run time):', n); continue
            path = m.lib[n]['file'] if n in m.lib else os.path.join(libsrc.ROOT, namespace_for(n, m), n + '.cs')
            open(path, 'w', encoding='utf-8', newline='\n').write(render(m, n))
            print('wrote', os.path.relpath(path, libsrc.ROOT))
