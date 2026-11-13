using HarmonyLib;
using RimWorld;
using VEF.Abilities;
using Verse;

namespace GojoPsycasts
{
    /// <summary>Reverse cursed technique turns cursed energy positive, and positive energy is what
    /// destroys curses (her call 2026-11-06, same as the Sukuna mod): anyone who has unlocked it deals
    /// double damage to anomalies - Anomaly's entities and mutants. If the Sukuna mod's RCT is also
    /// learned, that mod already doubles it, so this one stays out (no x4).</summary>
    // moved to the shared Reverse Cursed Technique (JujutsuFundamentals.Patch_RctVsAnomalies), 2026-11-10
    public static class Patch_GojoRctVsAnomalies
    {
        private static VEF.Abilities.AbilityDef sukunaRct;
        private static bool looked;

        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (!ModsConfig.AnomalyActive || !(dinfo.Instigator is Pawn attacker) || attacker == __instance) return;
            if (!(__instance is Pawn target) || !(target.RaceProps.IsAnomalyEntity || target.IsMutant)) return;
            var comp = attacker.GetComp<CompAbilities>();
            if (comp == null || GojoAbilityDefOf.Gojo_ReverseCursedTechnique == null || !comp.HasAbility(GojoAbilityDefOf.Gojo_ReverseCursedTechnique)) return;
            if (!looked) { sukunaRct = DefDatabase<VEF.Abilities.AbilityDef>.GetNamedSilentFail("Sukuna_ReverseCursedTechnique"); looked = true; }
            if (sukunaRct != null && comp.HasAbility(sukunaRct)) return;
            dinfo.SetAmount(dinfo.Amount * 2f);
        }
    }
}
