// Handwoven Rugs: a rug made by someone with no ideoligion (a robot, a mechanoid) takes the style of the colony's
// primary ideoligion instead of none. The game gives a crafted item the crafter's ideoligion style
// (GenRecipe.PostProcessProduct); with no crafter ideoligion it falls back to the `style` argument, which this fills in.
// Only VFE - Art's three rugs are touched. With the "rugs follow your ideoligion's style" box unticked the rug styles
// are not in any style category, so this finds nothing and changes nothing.
using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HandwovenRugs
{
    public class HandwovenRugsMod : Mod
    {
        public HandwovenRugsMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("mythraps.iranianrugs");
            try
            {
                RugLook.Patch(harmony);
            }
            catch (Exception e)
            {
                Log.Warning("[Handwoven Rugs] could not hook the rug pictures setting, it will need a restart: " + e.Message);
            }
            if (!ModsConfig.IdeologyActive) return;
            try
            {
                var target = AccessTools.Method(typeof(GenRecipe), "PostProcessProduct");
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(RobotStyles), nameof(RobotStyles.Prefix)));
            }
            catch (Exception e)
            {
                Log.Warning("[Handwoven Rugs] could not patch crafting, robot-made rugs will not follow the ideoligion style: " + e.Message);
            }
        }
    }

    public static class RobotStyles
    {
        static bool IsRug(ThingDef def) =>
            def != null && (def.defName == "VFE_RugSmall" || def.defName == "VFE_RugLarge" || def.defName == "VFE_RugGrand");

        public static void Prefix(Thing product, Pawn worker, Precept_ThingStyle precept, ref ThingStyleDef style)
        {
            try
            {
                if (style != null || precept != null || product == null || !IsRug(product.def)) return;
                if (worker != null && worker.Ideo != null) return;          // a colonist's own ideoligion decides, as usual
                var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                if (ideo != null) style = ideo.GetStyleFor(product.def);
            }
            catch (Exception) { }                                        // never stop a craft over a style
        }
    }
}
