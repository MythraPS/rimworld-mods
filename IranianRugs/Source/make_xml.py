# Writes Ideology/Defs/RugStyles.xml and Ideology/Patches/RugStyles.xml: one ThingStyleDef per (style, rug size), and the
# patch that hangs them on each style category (behind the mod-settings tick box). Base-game styles, then the ones other
# mods add; a category that isn't loaded is skipped quietly (success Always), one with no thingDefStyles list gets one.
#   python3 Source/make_xml.py
import os
# Only styles with a real carpet tradition of their own get rugs: Islamic (prayer rugs and saf carpets) and Christian
# (an Armenian church carpet), all photos of real carpets. Every other style uses the everyday carpets.
VANILLA = {s: s for s in ["Islamic", "Christian"]}
MODDED = {}
SIZES = [("Small", "VFE_RugSmall", "(2,2)"), ("Medium", "VFE_RugLarge", "(3,3)"), ("Large", "VFE_RugGrand", "(4,4)")]
ALL = {**VANILLA, **MODDED}

# The settings category (IranianRugs_General) is in Defs/Settings.xml, so it exists without Ideology too.
# Each style's texPath is the style folder, which holds Photo/ and Drawn/; the "Rug pictures" setting at the end of Patches/IranianRugs.xml narrows it to one of them.
d = ['<?xml version="1.0" encoding="utf-8"?>', '<!-- One rug look per ideoligion style and rug size. Loaded only with Ideology (see LoadFolders.xml). Written by Source/make_xml.py. -->', '<Defs>']
for folder in ALL.values():
    for sz, _td, ds in SIZES:
        d.append(f'  <ThingStyleDef>\n    <defName>IranianRugs_{folder}_{sz}</defName>\n    <graphicData>\n      <graphicClass>HandwovenRugs.Graphic_RugFolders</graphicClass>\n'
                 f'      <texPath>IranianRugs/Styles/{folder}/{sz}</texPath>\n      <shaderType>CutoutComplex</shaderType>\n      <drawSize>{ds}</drawSize>\n    </graphicData>\n  </ThingStyleDef>\n')
d.append('</Defs>')

def lis(folder, ind):
    return "".join(f"\n{ind}<li>\n{ind}  <thingDef>{td}</thingDef>\n{ind}  <styleDef>IranianRugs_{folder}_{sz}</styleDef>\n{ind}</li>" for sz, td, _ in SIZES)
ops = []
# VFE - Art's rugs have no CompStyleable, and the game refuses a style on a thing without one; give them one.
for _sz, td, _ds in SIZES:
    x = f'Defs/ThingDef[defName="{td}"]'
    ops.append(f'''            <li Class="PatchOperationConditional">
              <xpath>{x}/comps</xpath>
              <match Class="PatchOperationAdd">
                <xpath>{x}/comps</xpath>
                <value>
                  <li Class="CompProperties_Styleable" />
                </value>
              </match>
              <nomatch Class="PatchOperationAdd">
                <xpath>{x}</xpath>
                <value>
                  <comps>
                    <li Class="CompProperties_Styleable" />
                  </comps>
                </value>
              </nomatch>
            </li>''')
for cat, folder in ALL.items():
    x = f'Defs/StyleCategoryDef[defName="{cat}"]'
    ops.append(f'''            <li Class="PatchOperationConditional">
              <success>Always</success>
              <xpath>{x}/thingDefStyles</xpath>
              <match Class="PatchOperationAdd">
                <xpath>{x}/thingDefStyles</xpath>
                <value>{lis(folder, "                  ")}
                </value>
              </match>
              <nomatch Class="PatchOperationAdd">
                <success>Always</success>
                <xpath>{x}</xpath>
                <value>
                  <thingDefStyles>{lis(folder, "                    ")}
                  </thingDefStyles>
                </value>
              </nomatch>
            </li>''')
p = f'''<?xml version="1.0" encoding="utf-8"?>
<!-- With Ideology: a colony whose ideoligion has a style builds VFE - Art's rugs in that style's designs. A tick box in
     the mod settings (on by default) turns this off; rugs then all use the random mix. Only Islamic (prayer and saf
     carpets) and Christian (an Armenian church carpet) have their own rugs; other styles use the everyday carpets.
     Written by Source/make_xml.py. -->
<Patch>
  <Operation Class="ModSettingsFramework.PatchOperationModOption">
    <defaultValue>True</defaultValue>
    <label>Rugs follow your ideoligion's style</label>
    <tooltip>With Ideology, rugs made or built by a colony whose ideoligion uses the Islamic or Christian style use real carpets of that tradition: prayer and saf carpets, or an Armenian church carpet. Other styles use the everyday carpets. A rug made by someone with no ideoligion, such as a robot, takes the colony's main ideoligion. Unticked, every rug picks from the general mix. Takes effect after a restart.</tooltip>
    <id>IranianRugs_FollowStyle</id>
    <category>IranianRugs_General</category>
    <order>10</order>
    <operations>
      <li Class="PatchOperationFindMod">
        <mods>
          <li>Vanilla Furniture Expanded - Art</li>
        </mods>
        <match Class="PatchOperationSequence">
          <operations>
{chr(10).join(ops)}
          </operations>
        </match>
      </li>
    </operations>
  </Operation>
</Patch>
'''
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
open(os.path.join(root, "Ideology/Defs/RugStyles.xml"), "w").write("\n".join(d) + "\n")
open(os.path.join(root, "Ideology/Patches/RugStyles.xml"), "w").write(p)
print(len(ALL), "styles,", len(ALL) * 3, "style defs")
