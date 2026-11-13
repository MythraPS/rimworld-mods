using HarmonyLib;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>Yuji is psychically hypersensitive and gains levels fast (her call 2026-11-10): every bit of psycast
    /// experience counts twice, Modulo Yuji included. On top of the level each finger gives.</summary>
    [HarmonyPatch(typeof(VanillaPsycastsExpanded.Hediff_PsycastAbilities), nameof(VanillaPsycastsExpanded.Hediff_PsycastAbilities.GainExperience))]
    public static class Patch_YujiFastLevels
    {
        public const float Factor = 2f;

        public static void Prefix(VanillaPsycastsExpanded.Hediff_PsycastAbilities __instance, ref float experienceGain)
        {
            var p = __instance?.pawn;
            if (experienceGain > 0f && p != null && (Yuji.Is(p) || ModuloYuji.Is(p))) experienceGain *= Factor;
        }
    }
}
