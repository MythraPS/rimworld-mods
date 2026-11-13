using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEFAbility = VEF.Abilities.Ability;

namespace SukunaPsycasts
{
    public class HediffCompProperties_BenevolentShrine : HediffCompProperties
    {
        public float damageAmount = 12f;
        public int ticksBetween = 30;
        public float radius = 24.9f;
        public float soulPerHit = 0.06f;
        public HediffCompProperties_BenevolentShrine() => compClass = typeof(HediffComp_BenevolentShrine);
    }

    /// <summary>
    /// Yuji's domain (her call 2026-11-01). Anchored where it opens, at most radius 30 like Infinite Void, a
    /// minute at most, and like Infinite Void it collapses the moment he is outside it (her call 2026-11-09).
    /// It cuts no buildings, items, plants or
    /// corpses, and none of Yuji's allies (her call 2026-11-11: everyone else inside, neutrals too; Domains.Hits). They take Malevolent Shrine's
    /// damage as soul slashes, which no armor stops. The King of Curses takes no damage at all: each
    /// cut parts <see cref="HediffCompProperties_BenevolentShrine.soulPerHit"/> of his soul from the host,
    /// about eight seconds from whole to gone.
    /// </summary>
    public class HediffComp_BenevolentShrine : HediffComp
    {
        private IntVec3 anchor = IntVec3.Invalid;
        private int damageAcc, fleckAcc;
        private List<Thing> barrier = new List<Thing>();
        private bool raised;
        private JujutsuFundamentals.DomainInfo info;
        private float radius;   // fixed when the barrier goes up, like Infinite Void's

        private const int MaxTicks = 7200;   // 2 minutes (her call 2026-11-12)

        /// <summary>Her call 2026-11-09: no bigger than Infinite Void (radius 30). Uncapped, Modulo Yuji's came
        /// out bigger than the map, so its barrier fell off the map and the slashes were wherever he walked.</summary>
        public const float MaxRadius = 30f;

        public HediffCompProperties_BenevolentShrine Props => (HediffCompProperties_BenevolentShrine)props;
        public float Radius => radius > 0f ? radius
            : Mathf.Min(Props.radius * Scaling.Power(Pawn) * Scaling.AbilityBoost(Pawn), MaxRadius);

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (Pawn?.Map != null) anchor = Pawn.Position;
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            var map = Pawn?.Map;
            if (map == null || !anchor.IsValid) return;
            if (!raised)
            {
                raised = true; radius = Radius; RaiseBarrier(map);
                info = JujutsuFundamentals.Domains.Make(parent, anchor, radius, "Benevolent Shrine");   // the shared clash rules (2026-11-10)
                JujutsuFundamentals.Domains.Open(info);
                if (info == null || !JujutsuFundamentals.Domains.Active.Contains(info)) return;   // lost on the spot
            }
            else if (info == null) { info = JujutsuFundamentals.Domains.Make(parent, anchor, radius, "Benevolent Shrine"); JujutsuFundamentals.Domains.Rejoin(info); }
            var disappears = parent.TryGetComp<HediffComp_Disappears>();
            if (disappears != null && disappears.ticksToDisappear > MaxTicks) disappears.ticksToDisappear = MaxTicks;

            fleckAcc += delta;
            if (fleckAcc >= 4)
            {
                fleckAcc = 0;
                int cells = GenRadial.NumCellsInRadius(Radius);
                int count = Mathf.Clamp(Mathf.RoundToInt(cells * 0.25f), 40, 500);
                for (int i = 0; i < count; i++)
                {
                    var c = anchor + GenRadial.RadialPattern[Rand.Range(0, cells)];
                    if (c.InBounds(map)) SlashUtility.SmallSlashFleckAt(c, map);
                }
            }

            damageAcc += delta;
            if (damageAcc < Props.ticksBetween) return;
            damageAcc = 0;
            Cut(map);
        }

        private void Cut(Map map)
        {
            var yuji = Pawn;
            float r = Radius, soul = Props.soulPerHit * Mathf.Max(0.5f, Scaling.Power(yuji)) * Scaling.AbilityBoost(yuji);
            foreach (var p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p == yuji || p.Dead || p.Position.DistanceTo(anchor) > r) continue;
                // It cuts souls only, never bodies (her call 2026-11-02). The King is cut loose from his host,
                // anyone possessed is freed, and other enemies take soul wounds that knock them out.
                if (SukunaBehaviour.IsSukuna(p)) { SukunaArc.Sever(p, Props.soulPerHit); continue; }
                if (Possession.Free(p)) { SlashUtility.BlackSparks(p); continue; }
                if (!JujutsuFundamentals.Domains.Hits(yuji, p)) continue;   // everyone but allies (her call 2026-11-11)
                if (JujutsuFundamentals.SureHit.Blocked(p)) continue;   // anti-domain techniques, zero cursed energy (2026-11-10)
                SoulWound.Add(p, soul);
                SlashUtility.NotifyAttacked(p, yuji);
            }
        }

        /// <summary>Unlike Malevolent Shrine, Yuji's domain is closed (her call 2026-11-07): a barrier like
        /// Gojo's, one block per edge cell, that only Yuji walks through. Walls already on the edge stay;
        /// anyone standing on the edge is moved a step inside.</summary>
        private void RaiseBarrier(Map map)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Sukuna_YujiBarrier");
            if (def == null) return;
            float radius = Radius;
            // his empty hometown street over the inside (her call 2026-11-11)
            JujutsuFundamentals.Barrier.Ground("JJK_GroundBenevolent", anchor, Mathf.Min(radius, GenRadial.MaxRadialPatternRadius), map, barrier);
            // the scan reaches one cell past the ring, but never to the game's radial limit (her log
            // 2026-11-08: "Not enough squares to get to radius 200" when the shrine was at its cap)
            float scan = Mathf.Min(radius + 1f, GenRadial.MaxRadialPatternRadius - 0.01f);
            foreach (var c in GenRadial.RadialCellsAround(anchor, scan, true))
            {
                if (!c.InBounds(map)) continue;
                float d = c.DistanceTo(anchor);
                if (d <= radius - 0.75f || d > radius + 0.25f) continue;
                if (c.GetEdifice(map) != null) continue;
                foreach (var p in c.GetThingList(map).OfType<Pawn>().ToList())
                {
                    var v = (anchor - c).ToVector3().normalized * 2f;
                    var inward = c + new IntVec3(Mathf.RoundToInt(v.x), 0, Mathf.RoundToInt(v.z));
                    if (inward.InBounds(map) && inward.Standable(map)) { p.Position = inward; p.Notify_Teleported(true, true); }
                }
                var block = ThingMaker.MakeThing(def);
                if (block is Building_YujiBarrier b) b.owner = Pawn;
                barrier.Add(GenSpawn.Spawn(block, c, map, WipeMode.VanishOrMoveAside));
            }
        }

        /// <summary>Yuji has stepped outside his domain, by walking, being thrown, teleporting or leaving
        /// the map: it collapses, barrier and all, as Infinite Void does.</summary>
        private bool Left
        {
            get
            {
                var pawn = Pawn;
                if (pawn == null || !anchor.IsValid || !raised) return false;
                var domainMap = barrier.FirstOrDefault(b => b != null && b.Spawned)?.Map;
                if (pawn.Dead || pawn.Downed || !pawn.Spawned || (domainMap != null && pawn.Map != domainMap)) return true;   // downed too (her call 2026-11-12)
                return pawn.Position.DistanceTo(anchor) > Radius;
            }
        }

        public override bool CompShouldRemove => base.CompShouldRemove || Left;

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
            foreach (var b in barrier) if (b != null && b.Spawned) b.Destroy();
            barrier.Clear();
            anchor = IntVec3.Invalid;   // closed: a second call does nothing
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref anchor, "sukunaBenevolentAnchor", IntVec3.Invalid);
            Scribe_Values.Look(ref damageAcc, "sukunaBenevolentAcc", 0);
            Scribe_Values.Look(ref raised, "sukunaBenevolentRaised", false);
            Scribe_Values.Look(ref radius, "sukunaBenevolentRadius", 0f);
            if (Scribe.mode == LoadSaveMode.Saving) barrier.RemoveAll(b => b == null || b.Destroyed);
            Scribe_Collections.Look(ref barrier, "sukunaBenevolentBarrier", LookMode.Reference);
            if (barrier == null) barrier = new List<Thing>();
        }
    }

    /// <summary>Soul damage from Benevolent Shrine: it builds up, and at full the pawn collapses. It never
    /// kills, and fades over a couple of days.</summary>
    public static class SoulWound
    {
        public static void Add(Pawn p, float amount)
        {
            if (p?.health == null || p.Dead || amount <= 0f) return;
            var h = p.health.hediffSet.GetFirstHediffOfDef(SukunaArcDefOf.Sukuna_SoulWound)
                    ?? p.health.AddHediff(SukunaArcDefOf.Sukuna_SoulWound);
            h.Severity = Mathf.Min(1f, h.Severity + amount);
        }
    }

    /// <summary>Possession of any kind: Sukuna's own minute in Yuji's body, and the possession,
    /// mind-control and puppet effects of other mods (matched by name).</summary>
    public static class Possession
    {
        private static bool PossessedName(string n) =>
            n != null && !n.Contains("Possessor") && !n.Contains("Puppeteer") && !n.Contains("CoOp")
            && (n.Contains("Possess") || n.Contains("MindControlled") || n.EndsWith("Puppet"));

        public static bool Is(Pawn p)
        {
            if (p == null || p.Dead) return false;
            var ms = p.MentalStateDef;
            if (ms != null && (ms == SukunaArcDefOf.Sukuna_Possessed || PossessedName(ms.defName) || ms.defName.Contains("MindControl"))) return true;
            return p.health.hediffSet.hediffs.Any(h => PossessedName(h.def.defName));
        }

        /// <summary>Frees the pawn; true if there was anything to free.</summary>
        public static bool Free(Pawn p)
        {
            if (!Is(p)) return false;
            if (p.InMentalState) p.MentalState.RecoverFromState();
            foreach (var h in p.health.hediffSet.hediffs.Where(h => PossessedName(h.def.defName)).ToList())
                p.health.RemoveHediff(h);
            if (p.Spawned) Messages.Message($"{p.LabelShortCap} is free of whatever had hold of them.", p, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }
    }

    /// <summary>One block of Benevolent Shrine's barrier: a door only Yuji can pass, drawn as a plain
    /// block (the same as the Gojo mod's domain barrier).</summary>
    public class Building_YujiBarrier : Building_Door
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
            Scribe_References.Look(ref owner, "sukunaBarrierOwner");
        }
    }

    public class Ability_BenevolentShrine : VEFAbility
    {
        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            JujutsuFundamentals.Domains.Strain(pawn);   // Modulo Yuji is a calamity grade; Yuji before that is not (2026-11-10)
            Messages.Message("Domain Expansion: Benevolent Shrine.", pawn, MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
