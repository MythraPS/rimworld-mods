using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>One open Domain Expansion, as the clash rules see it.</summary>
    public class DomainInfo
    {
        public Pawn caster;
        public Map map;
        public IntVec3 center;
        public float radius;
        /// <summary>Sukuna's: wins every clash of two (her call 2026-11-10).</summary>
        public bool alwaysWins;
        public string label = "domain";
        /// <summary>Called when the domain loses a clash: close it (barrier, hediff and all).</summary>
        public Action collapse;

        /// <summary>The more refined domain is the caster's with the higher psychic sensitivity (her call 2026-11-10).</summary>
        public float Refinement => Jujutsu.Sensitivity(caster);
    }

    /// <summary>
    /// How domains interact (her calls 2026-11-10), allies' as well as enemies'. When a domain opens over one other
    /// domain, the more refined wins (higher psychic sensitivity; Sukuna's always wins) and the other collapses. When
    /// it opens over two or more, all of them collapse and every caster burns out: neural heat to the limit (the
    /// story's cursed technique burnout; at the limit a pawn just can't cast heat-costing psycasts until it cools).
    /// Mods open their domains through Open and close them through Close.
    /// </summary>
    public static class Domains
    {
        public static readonly List<DomainInfo> Active = new List<DomainInfo>();

        /// <summary>Does a domain's sure hit land on <paramref name="target"/>? Her call 2026-11-11: by default everyone
        /// inside who isn't the caster or an ally, neutrals and wild animals included. Ally = the caster's own faction or a
        /// faction allied with it; a pawn counts by its own faction, so a prisoner from another faction is hit. With the
        /// "Domains hit neutrals" setting off, only those hostile to the caster (or the caster's faction).
        /// Malevolent Shrine doesn't use this: its binding vow cuts everyone.</summary>
        public static bool Hits(Pawn caster, Thing target)
        {
            if (caster == null || target == null || target == caster) return false;
            if (!(JujutsuMod.Settings?.domainsHitNeutrals ?? true))
                return target.HostileTo(caster) || (caster.Faction != null && target.HostileTo(caster.Faction));
            var mine = caster.Faction; var theirs = target.Faction;
            if (mine == null || theirs == null) return true;
            return theirs != mine && theirs.RelationKindWith(mine) != FactionRelationKind.Ally;
        }

        /// <summary>Who can open a domain more than once in a row (her call: the calamity grades, Gojo, Modulo Yuji,
        /// Sukuna, later Dabura). Character mods add theirs.</summary>
        public static readonly List<Func<Pawn, bool>> Calamity = new List<Func<Pawn, bool>>();

        /// <summary>The strain of opening a domain. Calamity grades: half their heat limit (they can open it again).
        /// Anyone else: neural heat up to 90% of the limit (Ryu, her example). Young Megumi, later: the whole limit.</summary>
        /// <summary>scale: 1 for every domain; 0.5 for Yuta's on a barrier Dhruv's shikigami traced (DhruvBarrier).</summary>
        public static void Strain(Pawn p, float scale = 1f)
        {
            var tr = p?.psychicEntropy;
            if (tr == null) return;
            float target = Jujutsu.Any(Calamity, p) ? Mathf.Min(tr.MaxEntropy, tr.EntropyValue + tr.MaxEntropy * 0.5f * scale) : Mathf.Max(tr.EntropyValue, tr.MaxEntropy * 0.9f * scale);
            float add = target - tr.EntropyValue;
            if (add > 0f) tr.TryAddEntropy(add, p, scale: false, overLimit: true);
        }

        public static void Burnout(Pawn p)
        {
            var tr = p?.psychicEntropy;
            if (tr == null) return;
            float add = tr.MaxEntropy - tr.EntropyValue;
            if (add > 0f) tr.TryAddEntropy(add, p, scale: false, overLimit: true);
            if (p.Spawned) MoteMaker.ThrowText(p.DrawPos, p.Map, "Burnout", new Color(1f, 0.45f, 0.3f), 2.5f);
        }

        /// <summary>Register a domain that has just opened. False if it collapsed on the spot.</summary>
        public static bool Open(DomainInfo d)
        {
            if (d == null || d.map == null) return false;
            Active.RemoveAll(x => x == null || x.map == null || x.caster == null || x.caster.Destroyed);
            var others = Active.Where(o => o != d && o.map == d.map && Overlap(d, o)).ToList();
            Active.Add(d);
            if (others.Count == 0) return true;

            if (others.Count >= 2)
            {
                var all = new List<DomainInfo>(others) { d };
                Messages.Message("Domain clash: " + all.Count + " domains at once. All of them collapse, and their casters burn out.",
                                 new TargetInfo(d.center, d.map), MessageTypeDefOf.NeutralEvent, false);
                foreach (var x in all)
                {
                    Close(x);
                    x.collapse?.Invoke();
                    Burnout(x.caster);
                    FleckMaker.Static(x.center, x.map, FleckDefOf.PsycastAreaEffect, x.radius);
                }
                return false;
            }

            var o1 = others[0];
            var loser = Loser(d, o1, out string why);
            Messages.Message("Domain clash. " + why, new TargetInfo(loser.center, loser.map), MessageTypeDefOf.NeutralEvent, false);
            FleckMaker.Static(loser.center, loser.map, FleckDefOf.PsycastAreaEffect, loser.radius);
            Close(loser);
            loser.collapse?.Invoke();
            return loser != d;
        }

        public static void Close(DomainInfo d) => Active.Remove(d);

        /// <summary>A domain that was already open when the save was made: back on the list, no new clash.</summary>
        public static void Rejoin(DomainInfo d) { if (d != null && !Active.Contains(d)) Active.Add(d); }

        /// <summary>The usual handle for a domain hediff: lose the clash, lose the hediff.</summary>
        public static DomainInfo Make(Hediff h, IntVec3 center, float radius, string label, bool alwaysWins = false) => new DomainInfo
        {
            caster = h.pawn, map = h.pawn?.Map, center = center, radius = radius, label = label, alwaysWins = alwaysWins,
            collapse = () => { if (h.pawn != null && !h.pawn.Dead && h.pawn.health.hediffSet.hediffs.Contains(h)) h.pawn.health.RemoveHediff(h); },
        };

        public static bool Overlap(DomainInfo a, DomainInfo b) => a.center.DistanceTo(b.center) < a.radius + b.radius;

        private static DomainInfo Loser(DomainInfo opened, DomainInfo standing, out string why)
        {
            if (opened.alwaysWins != standing.alwaysWins)
            {
                var win = opened.alwaysWins ? opened : standing;
                var lose = win == opened ? standing : opened;
                why = $"{win.label} overpowers {lose.label}.";
                return lose;
            }
            float a = opened.Refinement, b = standing.Refinement;
            var l = a > b ? standing : opened;   // a tie goes to the domain already standing
            var w = l == opened ? standing : opened;
            why = $"{w.label} is more refined ({w.caster.LabelShort}: psychic sensitivity {Mathf.Max(a, b):P0} against {Mathf.Min(a, b):P0}): {l.label} collapses.";
            return l;
        }
    }

    /// <summary>A barrier block: a door only the domain's owner can pass, drawn as a plain block (Gojo's, moved here).</summary>
    public class Building_DomainBarrier : Building_Door
    {
        public Pawn owner;

        public override bool PawnCanOpen(Pawn p) => p != null && p == owner;
        public override bool BlocksPawn(Pawn p) => p != owner;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic?.Draw(drawLoc, Rot4.North, this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "jujutsuBarrierOwner");
        }
    }

    /// <summary>Raising and dropping a domain's barrier ring (Gojo's, moved here).</summary>
    public static class Barrier
    {
        public static List<Thing> Raise(Pawn owner, IntVec3 anchor, float radius, Map map)
        {
            var list = new List<Thing>();
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("JJK_DomainBarrier");
            if (def == null) return list;
            foreach (var c in GenRadial.RadialCellsAround(anchor, radius + 1f, true))
            {
                if (!c.InBounds(map)) continue;
                float d = c.DistanceTo(anchor);
                if (d <= radius - 0.75f || d > radius + 0.25f) continue;
                if (c.GetEdifice(map) != null) continue;
                foreach (var p in c.GetThingList(map).OfType<Pawn>().ToList())
                {
                    var dir = (anchor - c).ToVector3().normalized;
                    var inward = (c.ToVector3Shifted() + dir * 2f).ToIntVec3();
                    if (!(inward.InBounds(map) && inward.Standable(map)))
                    {
                        // two cells in is blocked: the nearest open cell inside the ring instead, so nobody is left in a block
                        inward = IntVec3.Invalid;
                        for (int k = 1; k < GenRadial.NumCellsInRadius(6f); k++)
                        {
                            var x = c + GenRadial.RadialPattern[k];
                            if (x.InBounds(map) && x.Standable(map) && x.DistanceTo(anchor) <= radius - 1.5f) { inward = x; break; }
                        }
                    }
                    if (inward.IsValid) { p.Position = inward; p.Notify_Teleported(true, true); }
                }
                var block = ThingMaker.MakeThing(def);
                if (block is Building_DomainBarrier b) b.owner = owner;
                list.Add(GenSpawn.Spawn(block, c, map, WipeMode.VanishOrMoveAside));
            }
            return list;
        }

        /// <summary>The domain's own ground drawn over the inside (her call 2026-11-11, like Infinite Void's space and
        /// white paint): one disc the size of the domain, kept in the barrier's list so it is saved, restored and
        /// destroyed with the barrier.</summary>
        public static void Ground(string overlayDef, IntVec3 anchor, float radius, Map map, List<Thing> into)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(overlayDef);
            if (def == null || map == null || into == null || !anchor.InBounds(map)) return;
            var ov = (GojoPsycasts.DomainOverlay)ThingMaker.MakeThing(def);
            ov.radius = radius;
            into.Add(GenSpawn.Spawn(ov, anchor, map));
        }

        public static void Drop(List<Thing> blocks)
        {
            if (blocks == null) return;
            foreach (var b in blocks) if (b != null && b.Spawned) b.Destroy();
            blocks.Clear();
        }
    }

    /// <summary>Dev test domains (Debug actions ▸ Jujutsu): an enemy's domain with no effect, drawn as a red circle,
    /// gone after 20 seconds, to try the clash rules against a real one.</summary>
    [StaticConstructorOnStartup]   // holds Material fields: the game wants these made on the main thread
    public class TestDomains : MapComponent
    {
        public static readonly List<(DomainInfo d, int until)> Open = new List<(DomainInfo, int)>();
        private static Material mat;
        private static Material Mat => mat ?? (mat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.Transparent, new Color(1f, 0.3f, 0.3f, 0.8f)));

        public TestDomains(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = Open.Count - 1; i >= 0; i--)
                if (Open[i].d.map == map && now > Open[i].until) { Domains.Close(Open[i].d); Open.RemoveAt(i); }
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            foreach (var entry in Open)
            {
                var d = entry.Item1;
                if (d.map == map)
                {
                    var c = d.center.ToVector3Shifted(); c.y = AltitudeLayer.MetaOverlays.AltitudeFor();
                    GenDraw.DrawCircleOutline(c, d.radius, Mat);
                }
            }
        }
    }
}
