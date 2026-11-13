using HarmonyLib;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEFAbility = VEF.Abilities.Ability;

namespace SukunaPsycasts
{
    /// <summary>
    /// "Raise cursed energy output" on psycasts from OTHER paths (her call 2026-11-08): more damage
    /// (the ability's power value), shorter cooldown, more range, more psyfocus cost. Neural heat
    /// is left alone ("for simplicity just have it increase psyfocus"). The Shrine path has its own
    /// boosts (Scaling.AbilityBoost).
    /// </summary>
    public static class RaisedOtherPaths
    {
        public static bool Applies(VEFAbility a)
        {
            if (a?.pawn == null || a.def == null) return false;
            var ext = a.def.GetModExtension<AbilityExtension_Psycast>();
            // vessels only (it was their Reinforcement), and not the JJK techniques: those have their own raised boost
            return ext != null && ext.path != SukunaDefOf.Sukuna_Path && !a.def.defName.StartsWith("JJK_")
                   && Scaling.HasVessel(a.pawn) && Scaling.Raised(a.pawn);
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.GetPowerForPawn))]
    public static class Patch_RaisedPower
    {
        public static void Postfix(VEFAbility __instance, ref float __result)
        {
            if (RaisedOtherPaths.Applies(__instance)) __result *= Scaling.RaisedAbilityFactor;
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.GetRangeForPawn))]
    public static class Patch_RaisedRange
    {
        public static void Postfix(VEFAbility __instance, ref float __result)
        {
            if (__result > 0f && RaisedOtherPaths.Applies(__instance)) __result *= Scaling.RaisedAbilityFactor;
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.GetCooldownForPawn))]
    public static class Patch_RaisedCooldown
    {
        public static void Postfix(VEFAbility __instance, ref int __result)
        {
            if (__result > 0 && RaisedOtherPaths.Applies(__instance))
                __result = Mathf.RoundToInt(__result / Scaling.RaisedAbilityFactor);
        }
    }

    [HarmonyPatch(typeof(AbilityExtension_Psycast), nameof(AbilityExtension_Psycast.GetPsyfocusUsedByPawn))]
    public static class Patch_RaisedPsyfocusCost
    {
        public static void Postfix(AbilityExtension_Psycast __instance, Pawn pawn, ref float __result)
        {
            if (__instance.path != SukunaDefOf.Sukuna_Path && Scaling.Raised(pawn)) __result *= Scaling.RaisedAbilityFactor;
        }
    }
}
