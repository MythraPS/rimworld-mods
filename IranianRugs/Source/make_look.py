# Writes the "Rug pictures" mod setting (both / photos / drawn) and the base rug descriptions into Patches/IranianRugs.xml.
# Textures/IranianRugs/<size>/ holds Photo/ and Drawn/; "Both" is the plain folder (HandwovenRugs.Graphic_RugFolders reads
# both subfolders) and the other two options narrow it, the Ideology style rugs too.
# The setting MUST sit in the same file, after the base graphicData replace: the game reads a mod's patch files in disk
# order, not by name, so a separate file can run first and then be overwritten by the base patch.
#   python3 Source/make_look.py
import os, re
SIZES = [("Small", "VFE_RugSmall", "small", ""), ("Medium", "VFE_RugLarge", "large", ""), ("Large", "VFE_RugGrand", "grand", " that dominates a room")]
STYLES = ["Islamic", "Christian"]
TRAD_PHOTO = "city medallion, prayer, Herat vine and palmette, garden, boteh, vase, Heriz, Turkmen, Qashqai kilim, Gabbeh, kilim or Baluch"
DRAWN = {"Small": "A small handwoven rug in one of several weaving traditions - city medallion, Herati, garden panels, boteh, Heriz, Turkmen, Gabbeh, kilim or Baluch - with fringed ends.",
         "Medium": "A large handwoven rug in one of several weaving traditions - city medallion, Herati, garden panels, boteh, vase, Heriz, Turkmen, Qashqai, Gabbeh, kilim or afshan - with fringed ends.",
         "Large": "A grand handwoven rug that dominates a room, in one of several weaving traditions - city medallion, Herati, prayer, garden panels, boteh, vase, Heriz, Turkmen, Qashqai, Gabbeh, kilim, Baluch or afshan - with fringed ends."}


def desc(mode, sz, adj, extra):
    if mode == "Drawn":
        return DRAWN[sz]
    if mode == "Photo":
        return f"A {adj} handwoven rug{extra}, copied from a photo of a real antique carpet: {TRAD_PHOTO}."
    return f"A {adj} handwoven rug{extra}: either a photo of a real antique carpet or a drawn design in the same weaving traditions ({TRAD_PHOTO})."


def ops(mode):
    out = []
    for sz, td, adj, extra in SIZES:
        x = f'Defs/ThingDef[defName="{td}"]'
        if mode != "Both":
            out.append(f"""                <li Class="PatchOperationReplace">
                  <xpath>{x}/graphicData/texPath</xpath>
                  <value><texPath>IranianRugs/{sz}/{mode}</texPath></value>
                </li>""")
        out.append(f"""                <li Class="PatchOperationReplace">
                  <xpath>{x}/description</xpath>
                  <value><description>{desc(mode, sz, adj, extra)}</description></value>
                </li>""")
    if mode != "Both":
        for st in STYLES:
            for sz, *_ in SIZES:
                out.append(f"""                <li Class="PatchOperationReplace">
                  <success>Always</success>
                  <xpath>Defs/ThingStyleDef[defName="IranianRugs_{st}_{sz}"]/graphicData/texPath</xpath>
                  <value><texPath>IranianRugs/Styles/{st}/{sz}/{mode}</texPath></value>
                </li>""")
    return "\n".join(out)


OPTS = [("Both", "Both", "Every rug picks at random from the photos and the drawn designs together.", True),
        ("Photo", "Photos of real carpets", "Rugs are photos of 22 real antique carpets, in the carpets' own colours.", False),
        ("Drawn", "Drawn designs", "The drawn designs from before version 1.3.0 (35 of them), tinted by the cloth or leather the rug is made from.", False)]
lis = []
for mode, label, tip, default in OPTS:
    lis.append(f"""      <li>
        <id>IranianRugs_Look_{mode}</id>
        <label>{label}</label>
        <tooltip>{tip}</tooltip>
        <defaultValue>{default}</defaultValue>
        <operations>
          <li Class="PatchOperationFindMod">
            <mods>
              <li>Vanilla Furniture Expanded - Art</li>
            </mods>
            <match Class="PatchOperationSequence">
              <operations>
{ops(mode)}
              </operations>
            </match>
          </li>
        </operations>
      </li>""")
BEGIN, END = "  <!-- rug pictures setting (written by Source/make_look.py) -->", "  <!-- end rug pictures setting -->"
block = f"""{BEGIN}
  <Operation Class="ModSettingsFramework.PatchOperationRadioButtons">
    <id>IranianRugs_Look</id>
    <label>Rug pictures</label>
    <tooltip>Which pictures the rugs use. Changes right away, rugs already built too.</tooltip>
    <category>IranianRugs_General</category>
    <order>5</order>
    <options>
{chr(10).join(lis)}
    </options>
  </Operation>
{END}
"""
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
old = os.path.join(root, "Patches/RugLook.xml")
if os.path.exists(old): os.remove(old)
p = os.path.join(root, "Patches/IranianRugs.xml"); s = open(p).read()
if BEGIN in s: s = s[:s.index(BEGIN)] + s[s.index(END) + len(END) + 1:]
s = s.replace("</Patch>", block + "</Patch>")
for sz, td, adj, extra in SIZES:
    s = re.sub(rf'(<xpath>Defs/ThingDef\[defName="{td}"\]/description</xpath>\s*<value><description>)[^<]*(</description>)',
               lambda m: m.group(1) + desc("Both", sz, adj, extra) + m.group(2), s)
open(p, "w").write(s)
print("wrote the rug pictures setting into Patches/IranianRugs.xml")

# Defs/RugLook.xml: the same choices as data, so Source/RobotStyles/RugLook.cs can switch them while the game runs.
from xml.sax.saxutils import escape
defs = []
for mode, *_ in OPTS:
    sub = "" if mode == "Both" else "/" + mode
    th = "\n".join(f"      <li><def>{td}</def><texPath>IranianRugs/{sz}{sub}</texPath><description>{escape(desc(mode, sz, adj, extra))}</description></li>"
                   for sz, td, adj, extra in SIZES)
    stl = "\n".join(f"      <li><def>IranianRugs_{st}_{sz}</def><texPath>IranianRugs/Styles/{st}/{sz}{sub}</texPath></li>"
                    for st in STYLES for sz, *_ in SIZES)
    defs.append(f"""  <HandwovenRugs.RugLookDef>
    <defName>IranianRugs_Look_{mode}</defName>
    <things>
{th}
    </things>
    <styles>
{stl}
    </styles>
  </HandwovenRugs.RugLookDef>""")
open(os.path.join(root, "Defs/RugLook.xml"), "w").write(
    '<?xml version="1.0" encoding="utf-8"?>\n<!-- Written by Source/make_look.py: what each "Rug pictures" option sets, for switching without a restart. -->\n<Defs>\n'
    + "\n".join(defs) + "\n</Defs>\n")
print("wrote Defs/RugLook.xml")
