using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    public class HediffCompProperties_BlackFlash : HediffCompProperties
    {
        public float baseChance              = 0.03f;
        public float damageMultiplier        = 2.5f;
        public float armorPenetrationBonus   = 0.50f;
        public float armorPenetrationBonusMax = 1.00f;
        public float armorPenetrationCeiling = 1.90f;
        public float lifestealFraction       = 0.30f;
        public float psyfocusRestored        = 0.05f;
        public float entropyPurged           = 12f;

        public HediffCompProperties_BlackFlash() => compClass = typeof(HediffComp_BlackFlash);
    }

    /// <summary>
    /// Passive. Holds the numbers; the actual proc is applied from the
    /// Pawn.PreApplyDamage prefix in Patches.cs, because that is the one place that
    /// sees both melee and psycast damage before armour is rolled.
    /// </summary>
    public class HediffComp_BlackFlash : HediffComp
    {
        public HediffCompProperties_BlackFlash Props => (HediffCompProperties_BlackFlash)props;

        public float ChanceFor(Pawn p)  => Mathf.Clamp01(Props.baseChance * Scaling.Power(p));

        /// <summary>Flat, not multiplicative: AP is subtracted from the armour
        /// rating, so scaling a small number buys almost nothing.</summary>
        public float ArmorPenBonusFor(Pawn p) =>
            Mathf.Min(Props.armorPenetrationBonus * Scaling.Power(p),
                      Props.armorPenetrationBonusMax);

        public float LifestealFor(Pawn p) => Props.lifestealFraction * Scaling.Power(p);
        public float PsyfocusFor(Pawn p)  => Props.psyfocusRestored  * Scaling.Power(p);
        public float EntropyPurgeFor(Pawn p) => Props.entropyPurged  * Scaling.Power(p);
    }
}
