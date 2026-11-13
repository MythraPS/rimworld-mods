using HarmonyLib;
using RimWorld;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Raise Cursed Energy Output strengthens this mod's own techniques (her call 2026-11-10): power, range and
    /// radius x1.5 for every JJK_ ability (Cursed Energy Blast, Air Step, Granite Blast, Boogie Woogie...). The
    /// techniques with their own numbers (Simple Domain's circle, Hazy Moon, Granite Tsunami) read Jujutsu.Boost
    /// themselves. Sukuna's Shrine and Gojo's Limitless keep their own raised rules.
    /// </summary>
    public static class RaisedBoost
    {
        public static bool Applies(Ability a) =>
            a?.pawn != null && a.def != null && a.def.defName.StartsWith("JJK_") && Jujutsu.Raised(a.pawn);
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GetPowerForPawn))]
    public static class Patch_JJKRaisedPower
    {
        public static void Postfix(Ability __instance, ref float __result) { if (RaisedBoost.Applies(__instance)) __result *= Jujutsu.RaisedFactor; }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GetRangeForPawn))]
    public static class Patch_JJKRaisedRange
    {
        public static void Postfix(Ability __instance, ref float __result) { if (__result > 0f && RaisedBoost.Applies(__instance)) __result *= Jujutsu.RaisedFactor; }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GetRadiusForPawn))]
    public static class Patch_JJKRaisedRadius
    {
        public static void Postfix(Ability __instance, ref float __result) { if (__result > 0f && RaisedBoost.Applies(__instance)) __result *= Jujutsu.RaisedFactor; }
    }
}
