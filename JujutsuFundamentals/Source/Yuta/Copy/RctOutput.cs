using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    /// <summary>Reverse Cursed Technique: Output (her design 2026-11-11): once Yuta has Reverse Cursed Technique he can
    /// heal someone else by touch. He has to stay beside them; it draws on his psyfocus as his own healing does.</summary>
    public class Ability_RctOutput : Ability
    {
        public override bool IsEnabledForPawn(out string reason)
        {
            if (!Jujutsu.Knows(pawn, JJKAbilityDefOf.JJK_ReverseCursedTechnique))
            { reason = "Needs Reverse Cursed Technique (Cursed Energy Fundamentals) first."; return false; }
            return base.IsEnabledForPawn(out reason);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true) =>
            base.ValidateTarget(target, showMessages) && target.Pawn != null && target.Pawn != pawn && !target.Pawn.Dead;

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var other = targets?.FirstOrDefault().Thing as Pawn;
            if (other == null || other.Dead || other == pawn) return;
            var old = other.health.hediffSet.GetFirstHediffOfDef(YutaCopyDefOf.JJK_RctOutputHealing);
            if (old != null) other.health.RemoveHediff(old);
            var h = HediffMaker.MakeHediff(YutaCopyDefOf.JJK_RctOutputHealing, other);
            h.TryGetComp<HediffComp_RctOutput>()?.SetHealer(pawn);
            other.health.AddHediff(h);
        }
    }

    public class HediffCompProperties_RctOutput : HediffCompProperties
    {
        /// <summary>x his own healing rate (proposal: twice it, all of it on the one person).</summary>
        public float factor = 2f;
        public float reach = 1.9f;
        public HediffCompProperties_RctOutput() => compClass = typeof(HediffComp_RctOutput);
    }

    public class HediffComp_RctOutput : HediffComp
    {
        private const int Interval = 60;
        private Pawn healer;
        private int acc;
        private float regrow;
        private bool done;

        public HediffCompProperties_RctOutput Props => (HediffCompProperties_RctOutput)props;
        public void SetHealer(Pawn p) => healer = p;

        public override bool CompShouldRemove => base.CompShouldRemove || done;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            acc += delta;
            if (acc < Interval) return;
            int steps = acc / Interval;
            acc %= Interval;
            var p = Pawn;
            if (p == null || p.Dead || healer == null || healer.Dead || !healer.Spawned || healer.Map != p.Map
                || !healer.Position.InHorDistOf(p.Position, Props.reach)) { done = true; return; }
            var tracker = healer.psychicEntropy;
            float psyfocus = tracker?.CurrentPsyfocus ?? 0f;
            if (psyfocus <= 0.001f) { done = true; return; }

            var rp = new HediffCompProperties_ReverseCursedTechnique();   // the self-heal's numbers
            float amount = rp.selfHealPerTickAtFullPsyfocus * psyfocus * Jujutsu.Power(healer) * steps * Props.factor
                         * Jujutsu.Product(Jujutsu.RctHealFactors, healer);
            var wounds = p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.Visible && !h.IsPermanent())
                          .OrderByDescending(h => h.Severity).ToList();
            bool missing = RctRegrow.Next(p) != null;
            if (wounds.Count == 0 && !missing) { done = true; return; }
            if (missing && wounds.Count > 0) { RctRegrow.Tick(p, ref regrow, amount * 0.5f); Heal(wounds, amount * 0.5f); }
            else if (missing) RctRegrow.Tick(p, ref regrow, amount);
            else Heal(wounds, amount);

            float cost = HediffComp_ReverseCursedTechnique.CostFactor(healer) * Props.factor;
            tracker.OffsetPsyfocusDirectly(-rp.psyfocusPerTick * steps * cost);
            if (p.Spawned && Rand.Chance(0.5f)) FleckMaker.ThrowMetaIcon(p.Position, p.Map, FleckDefOf.HealingCross);
        }

        private static void Heal(List<Hediff_Injury> wounds, float amount)
        {
            foreach (var w in wounds)
            {
                if (amount <= 0f) break;
                float take = Mathf.Min(amount, w.Severity);
                w.Heal(take);
                amount -= take;
            }
        }

        public override string CompLabelInBracketsExtra => healer != null ? healer.LabelShort : null;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref healer, "rctOutputHealer");
            Scribe_Values.Look(ref acc, "rctOutputAcc");
            Scribe_Values.Look(ref regrow, "rctOutputRegrow");
        }
    }
}
