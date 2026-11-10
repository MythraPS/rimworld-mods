using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// Every number in this path scales on psychic sensitivity (the stat), not on
    /// psyfocus (the pool). Keeping the two apart is deliberate — see the plan file.
    /// </summary>
    public static class Scaling
    {
        public const float DismantleBaseAP   = 0.25f;
        public const float DismantleCapAP    = 0.60f;
        public const float CleaveBaseAP      = 0.45f;
        public const float CleaveCapAP       = 0.90f;
        public const float CompleteAP        = 2.00f;   // ArmorRatingBase.maxValue
        public const float DerivedAPPerDamage = 0.015f; // the engine's own free rate

        public static float Sensitivity(Pawn p) =>
            p == null ? 1f : Mathf.Max(0f, p.GetStatValue(StatDefOf.PsychicSensitivity));

        /// <summary>
        /// How much of the King of Curses is actually in the body. A one-finger vessel
        /// wields a sliver of the technique (35%); the full twenty wield all of it.
        /// Fingers now come from quests, so the power curve follows the questline
        /// (user 2026-10-15: "scale with fingers").
        /// </summary>
        public const float FingerFactorAtOne = 0.35f;

        public static float FingerFactor(Pawn p)
        {
            int n = FingersEaten(p);
            if (n <= 0) return 1f;
            return Mathf.Lerp(FingerFactorAtOne, 1f, (n - 1) / 19f);
        }

        /// <summary>What every ability scales on: sensitivity times finger factor.
        /// Resistance to the takeover still reads raw sensitivity.</summary>
        /// <summary>"Raise cursed energy output" also strengthens activated Shrine abilities (her call
        /// 2026-11-08): more damage, wider Fire Arrow blast, larger domains, a 5-wide World Cutting Slash.</summary>
        public const float RaisedAbilityFactor = 1.5f;
        public static bool Raised(Pawn p) =>
            p?.health?.hediffSet?.GetFirstHediffOfDef(SukunaDefOf.Sukuna_PseudoInfinity) is Hediff_CursedEnergyReinforcement r && r.Boosted;
        public static float AbilityBoost(Pawn p) => Raised(p) ? RaisedAbilityFactor : 1f;

        public static float Power(Pawn p) => Sensitivity(p) * FingerFactor(p) * (ModuloYuji.Is(p) ? ModuloPower : 1f);

        /// <summary>Modulo Yuji has Sukuna's techniques and is far stronger than him (her call 2026-11-07).</summary>
        public const float ModuloPower = 2f;

        /// <summary>Never below what the engine would have derived for free.</summary>
        public static float ArmorPen(Pawn p, float baseAP, float capAP, float damage)
        {
            float s = Power(p);
            return Mathf.Clamp(Mathf.Max(baseAP * s, damage * DerivedAPPerDamage), 0f, capAP);
        }

        public static float Damage(Pawn p, float baseDamage) => baseDamage * Power(p);

        public static bool HasVessel(Pawn p) =>
            p?.health?.hediffSet?.GetFirstHediffOfDef(SukunaDefOf.Sukuna_Vessel) != null;

        public static int FingersEaten(Pawn p)
        {
            var h = p?.health?.hediffSet?.GetFirstHediffOfDef(SukunaDefOf.Sukuna_Vessel);
            return h == null ? 0 : Mathf.RoundToInt(h.Severity);
        }
    }
}
