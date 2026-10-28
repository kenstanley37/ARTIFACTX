"""Parses libMBIN's generated C# class files (src/ArtifactX.Tools/NMS.Tools.libMBIN/Source/NMS) into
{class: dict(guid, hash, file, fields=[dict(index, offset, type, name, attr)], enums=[...])}."""
import re, glob, os
ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'src', 'ArtifactX.Tools', 'NMS.Tools.libMBIN', 'Source', 'NMS')
FIELD = re.compile(r'^\s*/\* 0x([0-9A-F]+) \*/ public (.+?) (\w+);')
ATTR = re.compile(r'^\s*\[NMS\((.*)\)\]\s*$')
def parse_attr(text):
    out = {}
    for m in re.finditer(r'(\w+) = (typeof\([^)]*\)|"[^"]*"|0x[0-9A-Fa-f]+|-?\d+|true|false)', text):
        out[m.group(1)] = m.group(2)
    return out
def load():
    lib = {}
    for f in glob.glob(os.path.join(ROOT, '**', '*.cs'), recursive=True):
        lines = open(f, encoding='utf-8-sig').read().split('\n')
        text = '\n'.join(lines)
        h = re.search(r'\[NMS\(GUID = 0x([0-9A-F]+), NameHash = 0x([0-9A-F]+)\)\]\s*\n\s*public class (\w+)', text)
        if not h: continue
        fields, pending = [], None
        for ln in lines:
            a = ATTR.match(ln)
            if a: pending = parse_attr(a.group(1)); continue
            m = FIELD.match(ln)
            if m:
                fields.append(dict(offset=int(m.group(1), 16), type=m.group(2), name=m.group(3), attr=pending or {}))
                pending = None
        lib[h.group(3)] = dict(guid=int(h.group(1), 16), hash=int(h.group(2), 16), file=f, fields=fields, text=text)
    return lib
