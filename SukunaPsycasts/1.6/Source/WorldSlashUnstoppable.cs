using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// World Cutting Slash ignores every defence (her call 2026-10-31): Gojo's
    /// Infinity, shield belts, Thorns, genes and hediffs that scale damage down,
    /// armour, and the incoming damage factor.
    ///
    /// Thing.TakeDamage has one place where defences act, Pawn.PreApplyDamage
    /// (apparel absorb, Pawn_HealthTracker.PreApplyDamage with every hediff comp,
    /// gene and health FactorForDamage). The prefix runs first and skips it for this
    /// damage; the postfix runs last and puts the hit back as it came in, in case
    /// another mod's prefix or postfix still tried to absorb or shrink it. Armour
    /// is skipped with DamageInfo.SetIgnoreArmor, and IncomingDamageFactor (applied
    /// later, in DamageWorker_AddInjury) is divided out by <see cref="MakeUnstoppable"/>.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_WorldSlashUnstoppable
    {
        public static bool Is(DamageInfo dinfo) =>
            SukunaDefOf.Sukuna_WorldSlash != null && dinfo.Def == SukunaDefOf.Sukuna_WorldSlash;

        [HarmonyPriority(Priority.First)]
        public static bool Prefix(ref DamageInfo dinfo, ref bool absorbed, out DamageInfo? __state)
        {
            __state = null;
            if (!Is(dinfo)) return true;
            __state = dinfo;
            absorbed = false;
            return false;
        }

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref DamageInfo dinfo, ref bool absorbed, DamageInfo? __state)
        {
            if (__state == null) return;
            dinfo = __state.Value;
            absorbed = false;
        }

        /// <summary>The hit, made to land in full on this target.</summary>
        public static DamageInfo MakeUnstoppable(DamageInfo dinfo, Thing target)
        {
            dinfo.SetIgnoreArmor(true);
            if (target is Pawn p && dinfo.Def.ExternalViolenceFor(p))
            {
                float factor = p.GetStatValue(StatDefOf.IncomingDamageFactor);
                if (factor > 0.001f && factor < 1f) dinfo.SetAmount(dinfo.Amount / factor);
            }
            return dinfo;
        }
    }
}
