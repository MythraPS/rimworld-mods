using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GojoPsycasts
{
    /// <summary>The Black Flash mod options (her ask 2026-11-04): a chance multiplier, and
    /// whether damage that is not a melee attack (abilities) can trigger it. Gunshots never do.</summary>
    public static class BlackFlashRules
    {
        /// <summary>True while a melee attack is landing (set around Verb_MeleeAttack.TryCastShot,
        /// which applies its damage in the same call).</summary>
        public static int meleeDepth;
        public static bool InMelee => meleeDepth > 0;

        public static float Multiplier => JujutsuFundamentals.JujutsuMod.Settings?.blackFlashChance ?? 1f;

        public static bool Allowed(DamageInfo dinfo)
        {
            if (Multiplier <= 0f) return false;
            if (dinfo.Weapon != null && dinfo.Weapon.IsRangedWeapon && !InMelee) return false;
            if (InMelee) return true;
            return JujutsuFundamentals.JujutsuMod.Settings?.blackFlashAbilities ?? false;
        }

        public static float Scale(float chance) => Mathf.Clamp01(chance * Multiplier);

        /// <summary>Black Flash starts rare and comes more often as the psycaster grows (her call
        /// 2026-11-06, same curve as the Sukuna mod): x0.25 at psycast level 1, x0.6 at 10, x1 at 20,
        /// x1.5 at 30. No psylink counts as level 1.</summary>
        public static readonly SimpleCurve LevelFactor = new SimpleCurve
        {
            new CurvePoint(1f, 0.25f), new CurvePoint(10f, 0.6f), new CurvePoint(20f, 1f), new CurvePoint(30f, 1.5f)
        };

        public static int PsycastLevel(Pawn p)
        {
            var set = p?.health?.hediffSet;
            if (set == null) return 1;
            foreach (var h in set.hediffs)
                if (h is VanillaPsycastsExpanded.Hediff_PsycastAbilities psy) return Mathf.Max(1, psy.level);
            return 1;
        }

        public static float Scale(float chance, Pawn p) => Mathf.Clamp01(chance * Multiplier * LevelFactor.Evaluate(PsycastLevel(p))
                                                                         * (BlackFlashFx.InZone(p) ? BlackFlashFx.ZoneChance : 1f));
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_BlackFlashMeleeContext
    {
        public static void Prefix() => BlackFlashRules.meleeDepth++;
        public static void Finalizer() => BlackFlashRules.meleeDepth = Mathf.Max(0, BlackFlashRules.meleeDepth - 1);
    }
}
