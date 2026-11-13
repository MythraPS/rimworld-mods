using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class JJKToolDefOf2
    {
        public static ThingDef JJK_BlackRope, JJK_YutaRing, JJK_Gauntlets;
        public static HediffDef JJK_GauntletGrip;
        static JJKToolDefOf2() => DefOfHelper.EnsureInitializedInCtor(typeof(JJKToolDefOf2));
    }

    // ==================================================================== Black Rope

    public class CompProperties_BlackRope : CompProperties
    {
        public int uses = 12;   // proposal
        public CompProperties_BlackRope() => compClass = typeof(CompBlackRope);
    }

    /// <summary>
    /// Black Rope (her call 2026-11-11: consumable). Canon: it disrupts or cancels any cursed technique, which is how
    /// Miguel hurt Gojo through Infinity, and it wears away the more it is used. A hit that cancels something (psycast
    /// buffs, shields, Infinity, raised Reinforcement) uses up part of the rope; at nothing left it falls apart. Hits
    /// on someone with nothing to cancel cost nothing.
    /// </summary>
    public class CompBlackRope : ThingComp
    {
        public int left = -1;
        public CompProperties_BlackRope Props => (CompProperties_BlackRope)props;
        public int Left => left < 0 ? (left = Props.uses) : left;

        public void Used(Pawn holder)
        {
            left = Left - 1;
            if (left > 0) return;
            if (holder?.Spawned == true)
                Messages.Message($"{holder.LabelShortCap}'s Black Rope has worn away to nothing.", holder, MessageTypeDefOf.NegativeEvent, false);
            parent.Destroy();
        }

        public override string CompInspectStringExtra() => $"Uses left: {Left} of {Props.uses}";
        public override void PostExposeData() { base.PostExposeData(); Scribe_Values.Look(ref left, "jjkRopeLeft", -1); }
    }

    // ==================================================================== Yuta's ring

    /// <summary>Yuta's ring (her call 2026-11-11): on Yuta it lets Rika use her full power; on anyone else it gives
    /// Yuta's huge cursed energy reserve (the def's stat offsets). Yuta's side is read through RingOn().</summary>
    public static class YutaRing
    {
        public static bool On(Pawn p) => p?.apparel?.WornApparel?.Any(a => a.def == JJKToolDefOf2.JJK_YutaRing) == true;
    }

    // ==================================================================== Yuji's gauntlets

    /// <summary>
    /// The gauntlets (her calls 2026-11-11; no canon powers found, Yuji wears them in Shinjuku). Heavy armour on the
    /// arms and hands; "Guard" holds them up so half of the hits that come at the wearer land on the gauntlets
    /// instead; and while worn, missing fingers and hands cost the wearer no Manipulation.
    /// </summary>
    public class Apparel_Gauntlets : Apparel
    {
        public const float GuardChance = 0.5f;   // proposal
        public bool guard;

        public override IEnumerable<Gizmo> GetWornGizmos()
        {
            foreach (var g in base.GetWornGizmos()) yield return g;
            if (Wearer?.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "Guard",
                defaultDesc = $"Hold the gauntlets up: {GuardChance:P0} of the hits that come at the wearer land on the gauntlets instead.",
                icon = def.uiIcon,
                isActive = () => guard,
                toggleAction = () => guard = !guard,
            };
        }

        public override void Notify_Equipped(Pawn pawn) { base.Notify_Equipped(pawn); Gauntlets.Sync(pawn); }
        public override void Notify_Unequipped(Pawn pawn) { base.Notify_Unequipped(pawn); Gauntlets.Sync(pawn); }

        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref guard, "jjkGuard"); }
    }

    public static class Gauntlets
    {
        public static Apparel_Gauntlets Worn(Pawn p) => p?.apparel?.WornApparel?.OfType<Apparel_Gauntlets>().FirstOrDefault();

        private static bool HandPart(BodyPartRecord r) =>
            r != null && (r.def == BodyPartDefOf.Hand || r.def.defName == "Finger" || (r.parent != null && r.parent.def == BodyPartDefOf.Hand));

        /// <summary>Guarding: some hits are moved onto an arm or hand, where the gauntlets' armour takes them.</summary>
        public static void Guard(Pawn victim, ref DamageInfo dinfo)
        {
            var g = Worn(victim);
            if (g == null || !g.guard || victim.Downed || dinfo.Def == null || !dinfo.Def.harmsHealth || dinfo.Def.isExplosive) return;
            if (!Rand.Chance(Apparel_Gauntlets.GuardChance)) return;
            var parts = victim.health.hediffSet.GetNotMissingParts()
                .Where(r => r.def == BodyPartDefOf.Hand || r.def.defName == "Arm" || r.def.defName == "Forearm" || r.def.defName == "Hand").ToList();
            if (parts.Count == 0) return;
            dinfo.SetHitPart(parts.RandomElement());
        }

        /// <summary>Keep the grip hediff on exactly while gauntlets are worn.</summary>
        public static void Sync(Pawn p)
        {
            if (p?.health == null || JJKToolDefOf2.JJK_GauntletGrip == null) return;
            var h = p.health.hediffSet.GetFirstHediffOfDef(JJKToolDefOf2.JJK_GauntletGrip);
            bool worn = Worn(p) != null;
            if (worn && h == null) p.health.AddHediff(JJKToolDefOf2.JJK_GauntletGrip);
            else if (!worn && h != null) p.health.RemoveHediff(h);
        }

        /// <summary>How much Manipulation the wearer has lost to missing fingers and hands, measured by the game's
        /// own calculation run once as things are and once as if those parts were there.</summary>
        public static float MissingHandPenalty(Pawn p, Hediff self)
        {
            var real = new HediffSet(p);
            var whole = new HediffSet(p);
            foreach (var h in p.health.hediffSet.hediffs)
            {
                if (h == self) continue;
                real.hediffs.Add(h);
                if (h is Hediff_MissingPart && HandPart(h.Part)) continue;
                whole.hediffs.Add(h);
            }
            float a = PawnCapacityUtility.CalculateCapacityLevel(real, PawnCapacityDefOf.Manipulation);
            float b = PawnCapacityUtility.CalculateCapacityLevel(whole, PawnCapacityDefOf.Manipulation);
            return Mathf.Max(0f, b - a);
        }
    }

    /// <summary>The gauntlets' grip: its Manipulation offset is whatever missing fingers and hands are costing,
    /// re-measured every few seconds.</summary>
    public class Hediff_GauntletGrip : HediffWithComps
    {
        private HediffStage stage;
        private float offset = -1f;

        public override HediffStage CurStage
        {
            get
            {
                if (stage == null || offset < 0f)
                {
                    offset = pawn == null ? 0f : Gauntlets.MissingHandPenalty(pawn, this);
                    stage = new HediffStage { becomeVisible = true };
                    stage.capMods.Add(new PawnCapacityModifier { capacity = PawnCapacityDefOf.Manipulation, offset = offset });
                }
                return stage;
            }
        }

        public override string LabelInBrackets => offset > 0.001f ? $"+{offset:P0} manipulation" : null;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!pawn.IsHashIntervalTick(250, delta)) return;
            if (Gauntlets.Worn(pawn) == null) { pawn.health.RemoveHediff(this); return; }
            float before = offset;
            offset = -1f;
            var s = CurStage;
            if (Mathf.Abs(offset - before) > 0.001f) pawn.health.Notify_HediffChanged(this);
        }
    }
}
