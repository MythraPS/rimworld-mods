using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    public class HediffCompProperties_ReverseCursedTechnique : HediffCompProperties
    {
        public float selfHealPerTickAtFullPsyfocus = 1.5f;
        public float allyHealFraction = 0.35f;
        public float allyRadius = 3.9f;
        public float psyfocusPerTick = 0.00012f;
        public float entropyPerTick = 0.05f;

        public HediffCompProperties_ReverseCursedTechnique() => compClass = typeof(HediffComp_ReverseCursedTechnique);
    }

    /// <summary>
    /// Reverse Cursed Technique (from Sukuna's version). Speed follows the psyfocus pool: a full bar
    /// closes wounds almost as fast as they open, a low one is a slow mend. Lost parts regrow alongside
    /// the wounds. Nearby allies heal more slowly from the same pool. Stops at zero psyfocus or maximum
    /// neural heat and resumes on its own.
    /// </summary>
    public class HediffComp_ReverseCursedTechnique : HediffComp
    {
        private const int Interval = 60;
        private int acc;
        private float regrow;

        public HediffCompProperties_ReverseCursedTechnique Props => (HediffCompProperties_ReverseCursedTechnique)props;

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
            float psyfocus = tracker.CurrentPsyfocus;
            if (psyfocus <= 0.001f) return;   // neural heat doesn't stop it (her call 2026-11-10)

            var wounds = Wounds(pawn);
            bool missing = RctRegrow.Next(pawn) != null;
            if (wounds.Count == 0 && !missing && !HealAllies(pawn, psyfocus, steps, dryRun: true)) return;

            Running = true;
            float amount = Props.selfHealPerTickAtFullPsyfocus * psyfocus * Jujutsu.Power(pawn) * steps
                         * Jujutsu.Product(Jujutsu.RctHealFactors, pawn);
            if (missing && wounds.Count > 0) { RctRegrow.Tick(pawn, ref regrow, amount * 0.5f); HealWorst(wounds, amount * 0.5f); }
            else if (missing) RctRegrow.Tick(pawn, ref regrow, amount);
            else HealWorst(wounds, amount);
            HealAllies(pawn, psyfocus, steps, dryRun: false);

            float cost = CostFactor(pawn);
            tracker.OffsetPsyfocusDirectly(-Props.psyfocusPerTick * steps * cost);
            tracker.TryAddEntropy(Props.entropyPerTick * steps * cost, pawn, scale: false, overLimit: false);
        }

        public static float CostFactor(Pawn p) => RctPoison.CostFactor(p) * Jujutsu.Product(Jujutsu.RctCostFactors, p);

        private static List<Hediff_Injury> Wounds(Pawn p) =>
            p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.Visible && !h.IsPermanent())
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
            float amount = Props.selfHealPerTickAtFullPsyfocus * psyfocus * Jujutsu.Power(pawn) * Props.allyHealFraction * steps
                         * Jujutsu.Product(Jujutsu.RctHealFactors, pawn);
            bool any = false;
            foreach (var t in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, Props.allyRadius, true))
            {
                if (!(t is Pawn other) || other == pawn || other.Dead || other.Faction != pawn.Faction) continue;
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
                string s = Running ? "Repairing." : "Idle: no wounds, or out of psyfocus.";
                float c = CostFactor(Pawn);
                return c > 1f ? s + $"\nHealing costs x{c:0.0}." : s;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref acc, "jujutsuRctAcc", 0);
            Scribe_Values.Look(ref regrow, "jujutsuRctRegrow", 0f);
        }
    }

    /// <summary>Poison makes Reverse Cursed Technique cost more: x2 the moment any poison is in the body,
    /// rising to x3 as toxic buildup nears deadly. Mods add their own poisons through Jujutsu.RctCostFactors.</summary>
    public static class RctPoison
    {
        public static float CostFactor(Pawn p)
        {
            var set = p?.health?.hediffSet;
            if (set == null) return 1f;
            var tox = set.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);
            bool any = tox != null || set.HasHediff(HediffDefOf.ToxGasExposure) || set.HasHediff(HediffDefOf.FoodPoisoning) || Jujutsu.Any(Jujutsu.Poisoned, p);
            if (!any) return 1f;
            return 2f + (tox != null ? Mathf.Clamp01(tox.Severity) : 0f);
        }
    }

    /// <summary>Regrows lost parts one at a time, the part closest to the body first.</summary>
    public static class RctRegrow
    {
        /// <summary>Healing needed per point of the part's max health.</summary>
        public const float CostPerHp = 0.5f;

        private static bool Kept(Pawn p, BodyPartRecord part)
        {
            foreach (var f in Jujutsu.RctKeepsLost) if (f(p, part)) return true;
            return false;
        }

        private static int Depth(BodyPartRecord r) { int d = 0; for (var x = r.parent; x != null; x = x.parent) d++; return d; }

        public static Hediff_MissingPart Next(Pawn p) =>
            p.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().Where(h => h.Part != null && !Kept(p, h.Part))
             .OrderBy(h => Depth(h.Part)).FirstOrDefault();

        public static bool Tick(Pawn p, ref float progress, float amount)
        {
            var miss = Next(p);
            if (miss == null) { progress = 0f; return false; }
            progress += amount;
            var part = miss.Part;
            if (progress < part.def.GetMaxHealth(p) * CostPerHp) return true;
            progress = 0f;
            p.health.RestorePart(part, null, true);
            foreach (var a in Jujutsu.RctAfterRegrow) a(p);
            if (p.Spawned) MoteMaker.ThrowText(p.DrawPos, p.Map, part.LabelCap + " regrown", 3f);
            return true;
        }
    }

    /// <summary>Positive energy is what destroys curses: anyone who has learned Reverse Cursed Technique deals
    /// double damage to anomalies (Anomaly's entities and mutants), with every kind of attack.</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_RctVsAnomalies
    {
        public const float Factor = 2f;

        public static bool IsAnomaly(Thing t) => ModsConfig.AnomalyActive && t is Pawn p && (p.RaceProps.IsAnomalyEntity || p.IsMutant);

        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (!(dinfo.Instigator is Pawn attacker) || attacker == __instance) return;
            if (!IsAnomaly(__instance) || !Jujutsu.Knows(attacker, JJKAbilityDefOf.JJK_ReverseCursedTechnique)) return;
            dinfo.SetAmount(dinfo.Amount * Factor);
        }
    }
}
