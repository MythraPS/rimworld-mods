using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Putting on the ring (her call 2026-11-11, canon ch. 178): Yuta slips his ring onto his finger and connects to
    /// Rika for five minutes. While connected:
    ///  - Rika is fully manifested in her curse look at full power (out as Shikigami Rika, she changes in place;
    ///    not out, she comes);
    ///  - she supplies him cursed energy: a reserve of two more psyfocus bars (proposal) that tops his bar up;
    ///  - Copy works (canon: he copies while connected through the ring).
    /// When it ends, Rika goes back. Without the ring Manifest Rika calls Shikigami Rika (or, before the curse is
    /// unravelled, Cursed Spirit Rika). Adult Yuta's connection has no time limit; he ends it himself.
    /// Needs the ring worn and Manifest Rika learned. Proposal: an hour's wait (2500 ticks) after it ends.
    /// </summary>
    public static class RikaRing
    {
        public const int Cooldown = 2500;
        public static HediffDef Def => DefDatabase<HediffDef>.GetNamedSilentFail("JJK_RikaRingConnection");

        public static bool Connected(Pawn yuta) => Def != null && yuta?.health?.hediffSet?.HasHediff(Def) == true;

        public static void Connect(Pawn yuta)
        {
            if (yuta?.Map == null || Def == null || Connected(yuta)) return;
            var r = Rika.Current;
            IntVec3? at = r != null && r.Spawned && Rika.Owner == yuta ? r.Position : (IntVec3?)null;
            bool drafted = r?.Drafted == true;
            // she comes (or changes) in her curse look, his to order. Summoned before the connection is added, so a
            // failed summon doesn't leave a connection that ends at once and starts the wait.
            var p = Rika.Summon(yuta, curse: true, called: true);
            if (p == null) return;
            var h = yuta.health.AddHediff(Def);
            if (Yuta.Adult(yuta)) { var d = h.TryGetComp<HediffComp_Disappears>(); if (d != null) d.ticksToDisappear = int.MaxValue / 2; }
            RikaTracker.Instance.ring = true;
            if (at.HasValue && at.Value != p.Position && at.Value.Standable(p.Map)) p.Position = at.Value;
            if (drafted && p.drafter != null) p.drafter.Drafted = true;
            FleckMaker.Static(yuta.DrawPos, yuta.Map, FleckDefOf.PsycastAreaEffect, 4f);
            MoteMaker.ThrowText(yuta.DrawPos, yuta.Map, "The ring", Yuta.Purple, 2.5f);
        }

        /// <summary>Ends the connection (time up, taken off, or Rika gone).</summary>
        public static void Disconnect(Pawn yuta, string message)
        {
            if (yuta?.health == null) return;
            var h = yuta.health.hediffSet.GetFirstHediffOfDef(Def);
            if (h != null) yuta.health.RemoveHediff(h);
        }

        public static void Ended(Pawn yuta)
        {
            var rt = RikaTracker.Instance;
            if (rt != null) { rt.ringReadyTick = Find.TickManager.TicksGame + Cooldown; }
            var r = Rika.Current;
            if (rt != null && rt.ring && r != null && Rika.Owner == yuta)
                Rika.Dismiss($"{yuta.LabelShortCap}'s connection to {r.LabelShort} ends: she goes back.");
            if (rt != null) rt.ring = false;
        }
    }

    /// <summary>The connection's energy supply: a reserve that keeps his psyfocus bar topped up.</summary>
    public class HediffCompProperties_RikaRing : HediffCompProperties
    {
        public float reserve = 2f;
        public HediffCompProperties_RikaRing() { compClass = typeof(HediffComp_RikaRing); }
    }

    public class HediffComp_RikaRing : HediffComp
    {
        public float left = -1f;
        private HediffCompProperties_RikaRing Props => (HediffCompProperties_RikaRing)props;

        public override void CompPostMake() { base.CompPostMake(); left = Props.reserve; }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (left < 0f) left = Props.reserve;
            var p = Pawn;
            if (p == null || !p.IsHashIntervalTick(30)) return;
            // Rika gone (struck down, sent back): the connection ends too
            // ...or the ring itself taken off him (stripped, swapped out): it needs the ring worn
            if (Rika.Current == null || Rika.Owner != p || !YutaRing.On(p)) { p.health.RemoveHediff(parent); return; }
            var tr = p.psychicEntropy;
            if (tr == null || left <= 0f || tr.CurrentPsyfocus >= 1f) return;
            float add = Mathf.Min(left, 1f - tr.CurrentPsyfocus);
            tr.OffsetPsyfocusDirectly(add);
            left -= add;
        }

        public override void CompPostPostRemoved() { base.CompPostPostRemoved(); RikaRing.Ended(Pawn); }

        public override string CompLabelInBracketsExtra => left > 0f ? $"+{left * 100f:0}% psyfocus" : null;

        public override void CompExposeData() { base.CompExposeData(); Scribe_Values.Look(ref left, "ringReserve", -1f); }
    }

    /// <summary>The button on Yuta: put the ring on (or take it off).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    [StaticConstructorOnStartup]
    public static class Patch_RikaRingGizmo
    {
        private static Texture2D icon;

        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;
            var p = __instance;
            if (!Yuta.Is(p) || p.Faction != Faction.OfPlayer || !Rika.KnowsManifest(p)) yield break;
            if (icon == null) icon = ContentFinder<Texture2D>.Get("Things/Jujutsu/Tools/YutaRing", false) ?? BaseContent.BadTex;
            if (RikaRing.Connected(p))
            {
                yield return new Command_Action
                {
                    defaultLabel = "Take Off The Ring",
                    defaultDesc = "End the connection to Rika now: she goes back.",
                    icon = icon,
                    action = () => RikaRing.Disconnect(p, null),
                };
                yield break;
            }
            var cmd = new Command_Action
            {
                defaultLabel = "Put On The Ring",
                defaultDesc = "Yuta puts his ring on and connects to Rika for five minutes"
                              + (Yuta.Adult(p) ? " (no limit for adult Yuta)" : "")
                              + ": she manifests fully in her curse look at full power, he draws two more bars of psyfocus from her, and he can Copy. When it ends she goes back.",
                icon = icon,
                action = () => RikaRing.Connect(p),
            };
            int now = Find.TickManager.TicksGame;
            var rt = RikaTracker.Instance;
            if (!YutaRing.On(p)) cmd.Disable("He isn't wearing his ring.");
            else if (rt != null && rt.ringReadyTick > now) cmd.Disable($"Ready in {(rt.ringReadyTick - now).ToStringTicksToPeriod()}.");
            else if (!p.Spawned || p.Downed) cmd.Disable("He can't right now.");
            else if (Rika.Current != null && Rika.Owner == p && Rika.IsCurseForm(Rika.Current) && !Rika.Called)
                cmd.Disable("Cursed Spirit Rika is out of his control.");
            yield return cmd;
        }
    }
}
