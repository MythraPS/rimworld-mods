import re, sys, glob
WRITE = len(sys.argv) > 1 and sys.argv[1] == "write"
ITEM_HINTS = re.compile(r"<apparel>|<equipmentType>|<category>Item</category>|<thingCategories>|ParentName=\"[^\"]*(Weapon|Apparel|Resource|Drug|Item|Book|Artifact|Tool|Finger)[^\"]*\"|<weaponTags>|<tradeTags>|<comps>\s*<li Class=\"CompProperties_Usable", re.S)
NOT_ITEM = re.compile(r"<category>(Mote|Projectile|Pawn|Building|Filth|Plant|Ethereal|Gas|Attachment)</category>|<race>|<projectile>|<mote>|thingClass>[^<]*(Mote|Projectile|Overlay|Barrier|Filth|Building)", re.S)
SMALL = set()   # her call: capital letters for each word

def title(t):
    def word(m):
        w = m.group(0)
        return w[0].upper() + w[1:]
    # first letter of each word: after start, space, hyphen, slash, colon, open bracket or quote
    return re.sub(r"(?:(?<=^)|(?<=[\s\-/(\"“:]))[a-z]", lambda m: m.group(0).upper(), t)

changed = []
for f in sorted(glob.glob("Common/**/*.xml", recursive=True)):
    if "/Patches/" in f: continue
    s = open(f, encoding="utf-8").read()
    out = s
    for tag in ("VEF.Abilities.AbilityDef", "ThingDef", "AbilityDef"):
        def block(m):
            b = m.group(0)
            if tag == "ThingDef" and (not ITEM_HINTS.search(b) or NOT_ITEM.search(b)):
                return b
            def lab(mm):
                new = title(mm.group(2))
                if new != mm.group(2): changed.append((f, mm.group(2), new))
                return mm.group(1) + new + mm.group(3)
            # only the def's own <label>, not labels nested in comps/stages
            return re.sub(r"(^\s{4}<label>)([^<]+)(</label>)", lab, b, count=1, flags=re.M)
        out = re.sub(rf"<{re.escape(tag)}\b[^>]*>.*?</{re.escape(tag)}>", block, out, flags=re.S)
    if WRITE and out != s: open(f, "w", encoding="utf-8").write(out)
for f, a, b in changed: print(f"{f.split('Common/Defs/')[-1]}: {a!r} -> {b!r}")
print(len(changed), "labels")
