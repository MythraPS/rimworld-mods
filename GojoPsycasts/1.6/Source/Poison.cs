using HarmonyLib;
using RimWorld;
using Verse;

namespace GojoPsycasts
{
    /// <summary>Poison (her call 2026-11-04). While Infinity is up nothing poisonous reaches Gojo.
    /// Venom and tox rounds are hits, which Infinity already stops. The rest comes from the air
    /// and the ground: tox gas and rot stink (GasUtility.PawnGasEffectsTickInterval) and toxic
    /// fallout, tox water and the like (ToxicUtility.DoPawnToxicDamage), and food poisoning
    /// (FoodUtility.AddFoodPoisoningHediff, her call 2026-11-04). Poison he already has wears off as normal.</summary>
    public static class Poison
    {
        public static bool InfinityUp(Pawn p)
        {
            var h = p?.health?.hediffSet?.GetFirstHediffOfDef(GojoDefOf.Gojo_Infinity) as HediffWithComps;
            var comp = h?.TryGetComp<HediffComp_Infinity>();
            return comp != null && comp.ShieldActive;
        }

        /// <summary>How much more reverse cursed technique costs: x1 when clean, x2 the moment
        /// any poison is in the body, rising to x3 as toxic buildup nears deadly.</summary>
        public static float RctCostFactor(Pawn p)
        {
            var set = p?.health?.hediffSet;
            if (set == null) return 1f;
            var tox = set.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);
            bool any = tox != null
                || set.HasHediff(HediffDefOf.ToxGasExposure)
                || set.HasHediff(HediffDefOf.FoodPoisoning);
            if (!any) return 1f;
            return 2f + (tox != null ? UnityEngine.Mathf.Clamp01(tox.Severity) : 0f);
        }
    }

    [HarmonyPatch(typeof(GasUtility), nameof(GasUtility.PawnGasEffectsTickInterval))]
    public static class Patch_InfinityGas
    {
        public static bool Prefix(Pawn pawn) => !Poison.InfinityUp(pawn);
    }

    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.AddFoodPoisoningHediff))]
    public static class Patch_InfinityFoodPoisoning
    {
        public static bool Prefix(Pawn pawn) => !Poison.InfinityUp(pawn);
    }

    [HarmonyPatch(typeof(ToxicUtility), nameof(ToxicUtility.DoPawnToxicDamage))]
    public static class Patch_InfinityToxic
    {
        public static bool Prefix(Pawn p) => !Poison.InfinityUp(p);
    }

    /// <summary>Fire (her call 2026-11-04, "Infinity blocks anything Gojo perceives as harmful"):
    /// while Infinity is up he never catches fire, and flames on his cell do not burn him.</summary>
    [HarmonyPatch(typeof(FireUtility), nameof(FireUtility.TryAttachFire))]
    public static class Patch_InfinityNoFire
    {
        public static bool Prefix(Thing t) => !(t is Pawn p && Poison.InfinityUp(p));
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_InfinityFlame
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (dinfo.Def != DamageDefOf.Flame && dinfo.Def != DamageDefOf.Burn) return true;
            if (!Poison.InfinityUp(__instance)) return true;
            absorbed = true;
            return false;
        }
    }

    /// <summary>A hit Infinity stopped never reached him, so it does not stagger him
    /// (her report 2026-11-04: he slowed for a moment after every blocked shot).</summary>
    [HarmonyPatch(typeof(StaggerHandler), nameof(StaggerHandler.StaggerFor))]
    public static class Patch_InfinityNoStagger
    {
        public static bool Prefix(StaggerHandler __instance, ref bool __result)
        {
            if (!Poison.InfinityUp(__instance.parent)) return true;
            __result = false;
            return false;
        }
    }
}
