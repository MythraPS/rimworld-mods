#!/usr/bin/env python3
"""Static validator for GojoPsycasts.

Catches, without launching RimWorld:
  1. malformed XML
  2. def tags that are not real fields on the def's class
  3. defName references that resolve to nothing
  4. texture paths that point at no file

Field data comes from ikdasm output of the actual assemblies, so it reflects the
installed versions rather than anything remembered.
"""
import os, re, subprocess, sys, json, xml.etree.ElementTree as ET
from collections import defaultdict

MOD   = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME  = os.path.expanduser("~/.steam/steam/steamapps/common/RimWorld")
WS    = os.path.expanduser("~/.local/share/Steam/steamapps/workshop/content/294100")
CACHE = "/tmp/claude-1000/-home-mythraps/gojo-fieldindex.json"

ASSEMBLIES = [
    f"{GAME}/RimWorldLinux_Data/Managed/Assembly-CSharp.dll",
    f"{WS}/2023507013/1.6/Assemblies/VEF.dll",
    f"{WS}/2842502659/1.6/Assemblies/VanillaPsycastsExpanded.dll",
    f"{MOD}/1.6/Assemblies/GojoPsycasts.dll",
]
DEF_DIRS = [f"{GAME}/Data/Core/Defs", f"{GAME}/Data/Royalty/Defs",
            f"{GAME}/Data/Ideology/Defs", f"{GAME}/Data/Biotech/Defs",
            f"{GAME}/Data/Anomaly/Defs", f"{GAME}/Data/Odyssey/Defs",
            f"{WS}/2023507013", f"{WS}/2842502659", f"{MOD}"]

CLS_RE   = re.compile(r'^\.class\s+(?:\w+\s+)*?([\w.`<>/]+)\s*$', re.M)
CLASS_HDR= re.compile(r'^\.class\s+(.*?)$', re.M)
FIELD_RE = re.compile(r'^\s+\.field\s+(?:public|private|family|assembly)\s+'
                      r'(?:static\s+)?(?:initonly\s+)?(?:literal\s+)?'
                      r'(?:class\s+|valuetype\s+)?\S+\s+(\'?[\w<>`.]+\'?)\s*$', re.M)

def build_index():
    """FULL class name -> (set of field names, full base class name).

    Keyed on the fully-qualified name, NOT the short one. There are two
    AbilityDef classes in play — RimWorld.AbilityDef and VEF.Abilities.AbilityDef —
    and merging them by short name silently accepts vanilla-only fields on a VEF
    def, which the game then rejects at load. That mistake cost a launch.
    """
    idx, bases = defaultdict(set), {}
    cls_re  = re.compile(r'^\.class\s+.*?([A-Za-z_][\w.`]*(?:/[\w.`]+)*)\s*$')
    ext_re  = re.compile(r'^\s+extends\s+(?:\[[^\]]+\])?([\w.`]+(?:/[\w.`]+)*)')
    fld_re  = re.compile(r"^\s+\.field\s+.*?\s('?)([A-Za-z_][\w]*)\1\s*$")
    for dll in ASSEMBLIES:
        if not os.path.exists(dll):
            sys.exit(f"missing assembly: {dll}")
        il = subprocess.run(["ikdasm", dll], capture_output=True, text=True).stdout
        cur = None
        for line in il.splitlines():
            if line.startswith(".class"):
                m = cls_re.match(line)
                cur = m.group(1) if m else None
                if cur: idx.setdefault(cur, set())
            elif cur and line.startswith("       extends "):
                m = ext_re.match(line)
                if m: bases[cur] = m.group(1)
            elif cur and line.lstrip().startswith(".field "):
                m = fld_re.match(line)
                if m: idx[cur].add(m.group(2))
    return {k: sorted(v) for k, v in idx.items()}, bases

def load_index():
    if os.path.exists(CACHE):
        newest = max(os.path.getmtime(a) for a in ASSEMBLIES if os.path.exists(a))
        if os.path.getmtime(CACHE) > newest:
            d = json.load(open(CACHE))
            if d.get("schema") == 2: return d["fields"], d["bases"]
    fields, bases = build_index()
    os.makedirs(os.path.dirname(CACHE), exist_ok=True)
    json.dump({"schema": 2, "fields": fields, "bases": bases}, open(CACHE, "w"))
    return fields, bases

def resolve(name, fields):
    """XML class name -> full class name. Exact match wins; otherwise match on the
    short name, and refuse to guess when that is ambiguous."""
    if name in fields: return name, None
    short = name.split('.')[-1]
    hits = [k for k in fields if k.split('/')[-1].split('.')[-1] == short]
    if len(hits) == 1: return hits[0], None
    if len(hits) > 1:  return None, f"ambiguous class '{name}' — could be {sorted(hits)}; write it fully qualified"
    return None, None

def fields_for(cls, fields, bases, seen=None):
    seen = seen or set()
    if cls in seen: return set()
    seen.add(cls)
    out = set(fields.get(cls, []))
    b = bases.get(cls)
    if b and not b.endswith(("System.Object", "System.ValueType")):
        out |= fields_for(b, fields, bases, seen)
    return out

def our_xml():
    for root, _, files in os.walk(f"{MOD}/1.6/Defs"):
        for f in sorted(files):
            if f.endswith(".xml"): yield os.path.join(root, f)

def known_defnames():
    names = set()
    for d in DEF_DIRS:
        if not os.path.isdir(d): continue
        for root, _, files in os.walk(d):
            for f in files:
                if not f.endswith(".xml"): continue
                try: txt = open(os.path.join(root, f), encoding="utf-8", errors="ignore").read()
                except OSError: continue
                names.update(re.findall(r'<defName>([^<]+)</defName>', txt))
                names.update(re.findall(r'<(?:ThingDef|HediffDef|AbilityDef|[\w.]+)\s+Name="([^"]+)"', txt))
    return names

def main():
    errs, warns = [], []
    fields, bases = load_index()

    files = list(our_xml())
    if not files:
        print("no def files yet — nothing to validate"); return 0

    # 1. well-formedness
    trees = {}
    for f in files:
        r = subprocess.run(["xmllint", "--noout", f], capture_output=True, text=True)
        if r.returncode: errs.append(f"XML  {os.path.relpath(f,MOD)}: {r.stderr.strip()}")
        else: trees[f] = ET.parse(f).getroot()

    # 2. field names
    def check_node(node, cls, rel, path):
        """Check every direct child tag against the class's real fields, then
        recurse into any child that declares its own Class= (mod extensions,
        hediff comps, verbs). This is where most typos actually live."""
        full, amb = resolve(cls, fields)
        if amb:
            errs.append(f"CLASS {rel}: {path} {amb}"); return
        if full is None:
            warns.append(f"CLASS {rel}: unknown class '{cls}' at {path} — not checked "
                         f"(expected if it lives in our own not-yet-built DLL)")
            return
        known = fields_for(full, fields, bases)
        if not known:
            warns.append(f"CLASS {rel}: no fields found on {full} at {path}")
            return
        cls = full
        for child in node:
            if not isinstance(child.tag, str): continue
            sub = child.get("Class")
            if child.tag == "li":
                if sub: check_node(child, sub, rel, f"{path}/li[{sub}]")
                continue
            if child.tag not in known:
                near = sorted(k for k in known if k.lower().startswith(child.tag[:3].lower()))[:4]
                errs.append(f"FIELD {rel}: {path} <{child.tag}> is not a field on {cls}"
                            + (f"; closest: {near}" if near else ""))
                continue
            if sub:
                check_node(child, sub, rel, f"{path}/{child.tag}[{sub}]")
            else:
                for gc in child:
                    if isinstance(gc.tag, str) and gc.tag == "li" and gc.get("Class"):
                        check_node(gc, gc.get("Class"), rel,
                                   f"{path}/{child.tag}/li[{gc.get('Class')}]")

    for f, root in trees.items():
        rel = os.path.relpath(f, MOD)
        for defnode in root:
            if not isinstance(defnode.tag, str): continue
            cls = defnode.get("Class") or defnode.tag
            check_node(defnode, cls, rel, f"<{defnode.tag}>")

    # 3. dangling defName references
    known_defs = known_defnames()
    REF_TAGS = {"damageDef","hediffDef","thingDef","abilityClass","soundDef",
                "explosionSound","projectileDef","requiredGene","requiredMeme",
                "requiredFocus","stuffCategories","hediff","statDef"}
    for f, root in trees.items():
        rel = os.path.relpath(f, MOD)
        for el in root.iter():
            if isinstance(el.tag, str) and el.tag in REF_TAGS and el.text:
                v = el.text.strip()
                if v and not v[0].isdigit() and v not in known_defs and '.' not in v:
                    errs.append(f"REF   {rel}: <{el.tag}>{v}</{el.tag}> resolves to no def")

    # 3b. Traps the game only reports as "Config error" at load.
    for f, root in trees.items():
        rel = os.path.relpath(f, MOD)
        for defnode in root:
            if not isinstance(defnode.tag, str): continue
            name = defnode.findtext("defName") or "?"
            # VEF.Abilities.Ability declares Tick/TickInterval, so using it directly
            # gives "has a Tick method but doesn't have the needsTicking field.
            # It will not work." Every VPE self-buff uses Ability_Blank instead.
            ac = defnode.findtext("abilityClass")
            if ac and ac.strip() == "VEF.Abilities.Ability" \
               and defnode.findtext("needsTicking") is None:
                errs.append(f"TRAP  {rel}: {name} uses the raw VEF.Abilities.Ability. "
                            f"Use VEF.Abilities.Ability_Blank (what all 48 VPE self-buffs "
                            f"use) or set needsTicking/needsTickingInterval.")
            # A FleckDef whose graphicClass is not Graphic_Fleck is rejected at load
            # and the effect simply never appears, with no runtime error.
            if defnode.tag.endswith("FleckDef"):
                gc = defnode.find("graphicData")
                gcls = gc.findtext("graphicClass") if gc is not None else None
                if gcls and "Fleck" not in gcls:
                    errs.append(f"TRAP  {rel}: {name} is a FleckDef with graphicClass "
                                f"'{gcls}'. Flecks need Graphic_Fleck — anything else is "
                                f"rejected at load and the effect never draws.")

            # A shield comp with no compClass silently never instantiates.
            for li in defnode.iter("li"):
                cls = li.get("Class") or ""
                if cls.endswith("HediffCompProperties_Shield") and li.findtext("compClass") is None:
                    errs.append(f"TRAP  {rel}: {name} has a HediffCompProperties_Shield "
                                f"with no <compClass> — the comp will never be created.")

    # 4. texture paths
    for f, root in trees.items():
        rel = os.path.relpath(f, MOD)
        for el in root.iter():
            if isinstance(el.tag, str) and el.tag in ("iconPath","background","altBackground","texPath") and el.text:
                p = el.text.strip()
                if not any(os.path.exists(f"{MOD}/Textures/{p}{e}") for e in (".png",".jpg","")):
                    # Paths under our own folders must exist; anything else is assumed
                    # to be vanilla or dependency art resolved from a bundle.
                    ours = p.startswith(("Things/Gojo", "Things/Mote/Gojo",
                                         "Abilities/Gojo", "UI/"))
                    if ours:
                        errs.append(f"TEX   {rel}: {p} — ours, but no file under Textures/")
                    else:
                        print(f"note  {rel}: {p} — assumed vanilla/dependency art")

    for w in warns: print("warn  " + w)
    for e in errs:  print("ERROR " + e)
    print(f"\n{len(files)} file(s), {len(errs)} error(s), {len(warns)} warning(s)")
    return 1 if errs else 0

if __name__ == "__main__":
    sys.exit(main())
