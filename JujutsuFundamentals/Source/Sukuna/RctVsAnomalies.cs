using HarmonyLib;
using RimWorld;
using VEF.Abilities;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>Reverse cursed technique turns cursed energy positive, and positive energy is what
    /// destroys curses (her call 2026-11-06): anyone who has unlocked it deals double damage to
    /// anomalies - Anomaly's entities (revenants, fleshbeasts, noctols...) and mutants (shamblers,
    /// ghouls). Every kind of damage they deal, melee, slashes and shots alike.</summary>
    public static class RctVsAnomalies
    {
        public const float Factor = 2f;

        public static bool IsAnomaly(Thing t) =>
            ModsConfig.AnomalyActive && t is Pawn p && (p.RaceProps.IsAnomalyEntity || p.IsMutant);

        public static bool HasRct(Pawn p)
        {
            var comp = p?.GetComp<CompAbilities>();
            return comp != null && SukunaAbilityDefOf.Sukuna_ReverseCursedTechnique != null
                   && comp.HasAbility(SukunaAbilityDefOf.Sukuna_ReverseCursedTechnique);
        }
    }

    // moved to the shared Reverse Cursed Technique (JujutsuFundamentals.Patch_RctVsAnomalies), 2026-11-10
    public static class Patch_RctVsAnomalies
    {
        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (!(dinfo.Instigator is Pawn attacker) || attacker == __instance) return;
            if (!RctVsAnomalies.IsAnomaly(__instance) || !RctVsAnomalies.HasRct(attacker)) return;
            dinfo.SetAmount(dinfo.Amount * RctVsAnomalies.Factor);
        }
    }
}
