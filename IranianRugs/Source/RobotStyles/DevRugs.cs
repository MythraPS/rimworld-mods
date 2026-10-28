// Handwoven Rugs: dev-mode tool to check the rugs. Debug actions > Handwoven Rugs > "Place test rugs", then click the
// map: lays out every rug size (columns: small, large, grand) for the everyday carpets and, with Ideology, the
// Islamic and Christian styles (rows), each column in a different material so the colour tint shows.
using LudeonTK;
using RimWorld;
using Verse;

namespace HandwovenRugs
{
    public static class DevRugs
    {
        static readonly string[] Rugs = { "VFE_RugSmall", "VFE_RugLarge", "VFE_RugGrand" };
        static readonly string[] Sizes = { "Small", "Medium", "Large" };
        static readonly string[] Stuffs = { "Cloth", "Synthread", "DevilstrandCloth" };
        static readonly string[] Styles = { null, "Islamic", "Christian" };

        [DebugAction("Handwoven Rugs", "Place test rugs", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        static void PlaceTestRugs()
        {
            var map = Find.CurrentMap;
            var origin = UI.MouseCell();
            int placed = 0;
            for (int row = 0; row < Styles.Length; row++)
            {
                ThingStyleDef[] style = new ThingStyleDef[Rugs.Length];
                if (Styles[row] != null)
                {
                    bool any = false;
                    for (int i = 0; i < Rugs.Length; i++)
                    {
                        style[i] = DefDatabase<ThingStyleDef>.GetNamedSilentFail("IranianRugs_" + Styles[row] + "_" + Sizes[i]);
                        any |= style[i] != null;
                    }
                    if (!any) continue;                                  // no Ideology, or the style tick box is off
                }
                for (int col = 0; col < Rugs.Length; col++)
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(Rugs[col]);
                    var stuff = DefDatabase<ThingDef>.GetNamedSilentFail(Stuffs[col]) ?? GenStuff.DefaultStuffFor(def);
                    if (def == null) continue;
                    var cell = origin + new IntVec3(col * 5, 0, -row * 7);
                    if (!cell.InBounds(map)) continue;
                    var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? stuff : null);
                    if (style[col] != null) thing.StyleDef = style[col];
                    thing.SetFactionDirect(Faction.OfPlayer);
                    GenSpawn.Spawn(thing, cell, map, Rot4.North, WipeMode.Vanish);
                    placed++;
                }
            }
            Messages.Message("Handwoven Rugs: placed " + placed + " test rugs (rows: everyday, Islamic, Christian).", MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
