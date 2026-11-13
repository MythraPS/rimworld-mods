using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace GojoPsycasts
{
    public class HediffCompProperties_InfiniteVoid : HediffCompProperties
    {
        public float radius = 11.9f;      // x psychic sensitivity: under half Malevolent Shrine's 24.9
        public float maxRadius = 30f;
        public int lingerTicks = 600;     // the stun carries on this long after the domain closes
        public HediffCompProperties_InfiniteVoid() => compClass = typeof(HediffComp_InfiniteVoid);
    }

    /// <summary>
    /// Domain Expansion: Infinite Void. Anchored where it was opened, closed by a
    /// barrier ring, and everyone inside who is not Gojo's ally is stunned for as
    /// long as it lasts and <see cref="HediffCompProperties_InfiniteVoid.lingerTicks"/>
    /// after. Whoever is fighting him hand to hand is spared while they stay in
    /// contact: next to him, and one of them attacking the other.
    /// </summary>
    public class HediffComp_InfiniteVoid : HediffComp
    {
        private IntVec3 anchor = IntVec3.Invalid;
        private float radius;
        private Thing overlay;
        private List<Thing> barrier = new List<Thing>();
        private int acc;
        private JujutsuFundamentals.DomainInfo info;

        public const int MaxTicks = 3600;   // 1 minute at most (her call 2026-11-12; was 40 s)
        private const int Interval = 30;

        public HediffCompProperties_InfiniteVoid Props => (HediffCompProperties_InfiniteVoid)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            var pawn = Pawn;
            var map = pawn?.Map;
            if (map == null) return;
            anchor = pawn.Position;
            radius = Mathf.Min(Props.radius * Gojo.Power(pawn), Props.maxRadius);

            var ov = (DomainOverlay)ThingMaker.MakeThing(GojoDefOf.Gojo_VoidOverlay);
            ov.radius = radius;
            overlay = GenSpawn.Spawn(ov, anchor, map);
            RaiseBarrier(map);
            // the shared clash rules (2026-11-10)
            info = JujutsuFundamentals.Domains.Make(parent, anchor, radius, "Infinite Void");
            JujutsuFundamentals.Domains.Open(info);
        }

        /// <summary>One barrier block on each edge cell. Cells that already hold a
        /// wall or other edifice keep it; pawns standing on the edge are moved a
        /// step inside so nobody ends up inside a block.</summary>
        private void RaiseBarrier(Map map)
        {
            foreach (var c in GenRadial.RadialCellsAround(anchor, radius + 1f, true))
            {
                if (!c.InBounds(map)) continue;
                float d = c.DistanceTo(anchor);
                if (d <= radius - 0.75f || d > radius + 0.25f) continue;
                if (c.GetEdifice(map) != null) continue;
                foreach (var p in c.GetThingList(map).OfType<Pawn>().ToList())
                {
                    var inward = Fx.Along(c, (anchor - c).ToVector3(), 2f, map);
                    if (inward != c) { p.Position = inward; p.Notify_Teleported(true, true); }
                }
                var block = ThingMaker.MakeThing(GojoDefOf.Gojo_DomainBarrier);
                if (block is Building_DomainBarrier b) b.owner = Pawn;
                barrier.Add(GenSpawn.Spawn(block, c, map, WipeMode.VanishOrMoveAside));
            }
        }

        /// <summary>Gojo has stepped outside his domain, by walking, being thrown,
        /// teleporting or leaving the map: it collapses, barrier and all
        /// (her call 2026-10-28).</summary>
        private bool Left
        {
            get
            {
                var pawn = Pawn;
                if (pawn == null || !anchor.IsValid) return false;
                var domainMap = overlay?.Map;
                if (pawn.Dead || pawn.Downed || !pawn.Spawned || (domainMap != null && pawn.Map != domainMap)) return true;   // downed too (her call 2026-11-12)
                return pawn.Position.DistanceTo(anchor) > radius;
            }
        }

        public override bool CompShouldRemove => base.CompShouldRemove || Left;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (info == null && anchor.IsValid && Pawn?.Map != null)   // after loading a save
            { info = JujutsuFundamentals.Domains.Make(parent, anchor, radius, "Infinite Void"); JujutsuFundamentals.Domains.Rejoin(info); }
            var disappears = parent.TryGetComp<HediffComp_Disappears>();
            if (disappears != null && disappears.ticksToDisappear > MaxTicks) disappears.ticksToDisappear = MaxTicks;

            acc += delta;
            if (acc < Interval) return;
            acc = 0;
            var map = Pawn?.Map;
            if (map == null || !anchor.IsValid) return;
            foreach (var victim in Inside(map)) victim.stances?.stunner?.StunFor(Interval + 15, Pawn, false, false);
        }

        private IEnumerable<Pawn> Inside(Map map)
        {
            var gojo = Pawn;
            foreach (var p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p == gojo || p.Dead || p.Position.DistanceTo(anchor) > radius) continue;
                if (!JujutsuFundamentals.Domains.Hits(gojo, p)) continue;   // everyone but allies (her call 2026-11-11)
                if (InContact(gojo, p)) continue;
                if (JujutsuFundamentals.SureHit.Blocked(p)) continue;   // anti-domain techniques, zero cursed energy (2026-11-10)
                yield return p;
            }
        }

        /// <summary>Touching him: adjacent, and one of them is attacking the other.</summary>
        public static bool InContact(Pawn gojo, Pawn other)
        {
            if (!gojo.Spawned || !other.Spawned || !gojo.Position.AdjacentTo8WayOrInside(other.Position)) return false;
            return gojo.CurJob?.targetA.Thing == other || other.CurJob?.targetA.Thing == gojo
                || gojo.mindState?.meleeThreat == other || other.mindState?.meleeThreat == gojo;
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            Collapse();
        }

        /// <summary>Dead pawns' hediffs stop ticking and are never removed, so the domain closes here too.</summary>
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            Collapse();
        }

        private void Collapse()
        {
            if (info != null) { JujutsuFundamentals.Domains.Close(info); info = null; }
            var map = Pawn?.Map ?? overlay?.Map;
            if (map != null && anchor.IsValid)
                foreach (var victim in Inside(map)) victim.stances?.stunner?.StunFor(Props.lingerTicks, Pawn, false, true);
            if (overlay != null && overlay.Spawned) overlay.Destroy();
            foreach (var b in barrier) if (b != null && b.Spawned) b.Destroy();
            barrier.Clear();
            overlay = null;
            anchor = IntVec3.Invalid;   // closed: a second call does nothing
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref anchor, "gojoVoidAnchor", IntVec3.Invalid);
            Scribe_Values.Look(ref radius, "gojoVoidRadius", 0f);
            Scribe_References.Look(ref overlay, "gojoVoidOverlay");
            if (Scribe.mode == LoadSaveMode.Saving) barrier.RemoveAll(b => b == null || b.Destroyed);
            Scribe_Collections.Look(ref barrier, "gojoVoidBarrier", LookMode.Reference);
            if (barrier == null) barrier = new List<Thing>();
        }
    }

    /// <summary>One block of the barrier: a door that only Gojo can pass. Drawn as
    /// a plain block, never as a door swinging open.</summary>
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
            Scribe_References.Look(ref owner, "gojoBarrierOwner");
        }
    }

    /// <summary>The inside of the domain: space with white paint thrown across it,
    /// drawn as one disc the size of the domain, under everything standing in it.</summary>
    public class DomainOverlay : Thing
    {
        public float radius = 10f;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var mat = def.graphic?.MatSingle;
            if (mat == null) return;
            var pos = drawLoc; pos.y = AltitudeLayer.FloorEmplacement.AltitudeFor();
            float s = radius * 2f + 1f;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(s, 1f, s)), mat, 0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref radius, "gojoVoidRadius", 10f);
        }
    }
}
