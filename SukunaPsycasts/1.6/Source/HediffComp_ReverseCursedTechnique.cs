using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    public class HediffCompProperties_ReverseCursedTechnique : HediffCompProperties
    {
        public float selfHealPerTickAtFullPsyfocus = 0.06f;
        public float allyHealFraction = 0.35f;
        public float allyRadius       = 3.9f;
        public float psyfocusPerTick  = 0.00012f;
        public float entropyPerTick   = 0.05f;

        public HediffCompProperties_ReverseCursedTechnique()
            => compClass = typeof(HediffComp_ReverseCursedTechnique);
    }

    /// <summary>
    /// Passive. Speed reads the psyfocus POOL every tick, so a full bar closes
    /// wounds almost as fast as they open and a low bar is a slow mend — one
    /// continuous rate, not two modes. Stops dead at zero psyfocus or at maximum
    /// neural heat, and resumes on its own.
    /// </summary>
    public class HediffComp_ReverseCursedTechnique : HediffComp
    {
        private const int Interval = 60;
        private int acc;
        private float regrow;

        public HediffCompProperties_ReverseCursedTechnique Props =>
            (HediffCompProperties_ReverseCursedTechnique)props;

        public bool Running { get; private set; }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            acc += delta;
            if (acc < Interval) return;
            int steps = acc / Interval;
            acc %= Interval;

            var pawn = Pawn;
            Running = false;
            if (pawn == null || pawn.Dead || pawn.Map == null) return;

            var tracker = pawn.psychicEntropy;
            if (tracker == null) return;

            // Both hard stops.
            float psyfocus = tracker.CurrentPsyfocus;
            if (psyfocus <= 0.001f) return;
            if (tracker.EntropyValue >= tracker.MaxEntropy) return;

            var wounds = Wounds(pawn);
            // limbs regrow mid-fight, alongside the wounds (her call 2026-11-07)
            bool missing = RctRegrow.Next(pawn) != null;
            bool anyAlly = false;
            if (wounds.Count == 0 && !missing)
            {
                anyAlly = HealAllies(pawn, psyfocus, steps, dryRun: true);
                if (!anyAlly) return;
            }

            Running = true;
            float sens = Scaling.Power(pawn);
            float amount = Props.selfHealPerTickAtFullPsyfocus * psyfocus * sens * steps * YujiRct.HealFactor(pawn);

            // Wounds and lost parts at once: half to each while both are there.
            if (missing && wounds.Count > 0) { RctRegrow.Tick(pawn, ref regrow, amount * 0.5f); HealWorst(wounds, amount * 0.5f); }
            else if (missing) RctRegrow.Tick(pawn, ref regrow, amount);
            else HealWorst(wounds, amount);
            HealAllies(pawn, psyfocus, steps, dryRun: false);

            // poison makes healing cost more (her call 2026-11-04)
            float cost = RctPoison.CostFactor(pawn);
            tracker.OffsetPsyfocusDirectly(-Props.psyfocusPerTick * steps * cost);
            tracker.TryAddEntropy(Props.entropyPerTick * steps * cost, pawn, scale: false, overLimit: false);
        }

        private static List<Hediff_Injury> Wounds(Pawn p) =>
            p.health.hediffSet.hediffs.OfType<Hediff_Injury>()
             .Where(h => h.Visible && !h.IsPermanent())
             .OrderByDescending(h => h.Severity).ToList();

        private static void HealWorst(List<Hediff_Injury> wounds, float amount)
        {
            foreach (var w in wounds)
            {
                if (amount <= 0f) break;
                float take = Mathf.Min(amount, w.Severity);
                w.Heal(take);
                amount -= take;
            }
        }

        private bool HealAllies(Pawn pawn, float psyfocus, int steps, bool dryRun)
        {
            float radius = Props.allyRadius;
            float amount = Props.selfHealPerTickAtFullPsyfocus * psyfocus
                         * Scaling.Power(pawn) * Props.allyHealFraction * steps;
            bool any = false;
            foreach (var t in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, radius, true))
            {
                if (!(t is Pawn other) || other == pawn || other.Dead) continue;
                if (other.Faction != pawn.Faction) continue;
                var wounds = Wounds(other);
                if (wounds.Count == 0) continue;
                any = true;
                if (dryRun) return true;
                HealWorst(wounds, amount);
            }
            return any;
        }

        public override string CompTipStringExtra
        {
            get
            {
                string s = Running ? "Repairing." : "Idle — no wounds, or out of psyfocus or neural heat.";
                float c = RctPoison.CostFactor(Pawn);
                return c > 1f ? s + $"\nPoisoned: healing costs x{c:0.0}." : s;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref acc, "sukunaRctAcc", 0);
            Scribe_Values.Look(ref regrow, "sukunaRctRegrow", 0f);
        }
    }

    /// <summary>Poison makes reverse cursed technique cost more (her call 2026-11-04): x1 when
    /// clean, x2 the moment any poison is in the body, rising to x3 as toxic buildup nears deadly.</summary>
    public static class RctPoison
    {
        public static float CostFactor(Pawn p)
        {
            var set = p?.health?.hediffSet;
            if (set == null) return 1f;
            var tox = set.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);
            bool any = tox != null
                || set.HasHediff(HediffDefOf.ToxGasExposure)
                || set.HasHediff(HediffDefOf.FoodPoisoning)
                || (PoisonBlood.Def != null && set.HasHediff(PoisonBlood.Def))
                || (SukunaArcDefOf.Sukuna_BloodPoison != null && set.HasHediff(SukunaArcDefOf.Sukuna_BloodPoison));
            if (!any) return 1f;
            return 2f + (tox != null ? Mathf.Clamp01(tox.Severity) : 0f);
        }
    }
}
