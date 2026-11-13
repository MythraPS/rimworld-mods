using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class UroDomainDefOf
    {
        public static VEFAbilityDef JJK_UroDomainExpansion;
        public static HediffDef JJK_UroDomain;
        static UroDomainDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(UroDomainDefOf));
    }

    /// <summary>
    /// Uro's Domain Expansion (her call 2026-11-11). Canon shows her open it (ch. 179, against Yuta and Ryu) but the clash
    /// breaks it before its name or sure hit are seen; hers: named Skyfall, the sure hit is Thin Ice-Breaker. Built like Ryu's: a
    /// barrier, and every second and a half Thin Ice-Breaker lands on every enemy of hers inside (shields and buffs
    /// stripped, shield belts broken, armour useless for a while, damage through armour). Same cost rule as Ryu's:
    /// half a psyfocus bar times her psyfocus cost factor, neural heat to 90% of her limit. It isn't on any tree; only
    /// Uro has it (YutaCore's Uro.Allowed lets it through).
    /// </summary>
    public class Ability_UroDomain : Ability
    {
        public override bool IsEnabledForPawn(out string reason)
        {
            if ((pawn.psychicEntropy?.CurrentPsyfocus ?? 0f) < UroDomain.Cost(pawn)) { reason = $"Needs {UroDomain.Cost(pawn):P0} psyfocus."; return false; }
            if (Jujutsu.Has(pawn, UroDomainDefOf.JJK_UroDomain)) { reason = "Her domain is already open."; return false; }
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            UroDomain.Open(pawn);
        }
    }

    public static class UroDomain
    {
        public const float Psyfocus = 0.5f;
        private static StatDef costStat;

        public static float Cost(Pawn p)
        {
            costStat = costStat ?? DefDatabase<StatDef>.GetNamedSilentFail("VPE_PsyfocusCostFactor");
            return Psyfocus * (costStat == null ? 1f : Mathf.Clamp(p.GetStatValue(costStat), 0.1f, 1f));
        }

        public static bool Open(Pawn p)
        {
            if (p?.Map == null || Jujutsu.Has(p, UroDomainDefOf.JJK_UroDomain)) return false;
            if ((p.psychicEntropy?.CurrentPsyfocus ?? 0f) < Cost(p)) return false;
            p.psychicEntropy?.OffsetPsyfocusDirectly(-Cost(p));
            Domains.Strain(p);
            p.health.AddHediff(UroDomainDefOf.JJK_UroDomain);
            return true;
        }
    }

    public class HediffCompProperties_UroDomain : HediffCompProperties
    {
        public float radius = 11.9f;
        public float maxRadius = 30f;
        public int interval = 90;
        public float damage = 18f;   // Thin Ice-Breaker's
        public HediffCompProperties_UroDomain() => compClass = typeof(HediffComp_UroDomain);
    }

    public class HediffComp_UroDomain : HediffComp
    {
        private IntVec3 anchor = IntVec3.Invalid;
        private float radius;
        private List<Thing> barrier = new List<Thing>();
        private int acc;
        private DomainInfo info;
        private static DamageDef dmgDef;

        public HediffCompProperties_UroDomain Props => (HediffCompProperties_UroDomain)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            var pawn = Pawn; var map = pawn?.Map;
            if (map == null) return;
            anchor = pawn.Position;
            radius = Mathf.Min(Props.radius * Jujutsu.Sensitivity(pawn) * Jujutsu.Boost(pawn), Props.maxRadius);
            barrier = Barrier.Raise(pawn, anchor, radius, map);
            Barrier.Ground("JJK_GroundSkyfall", anchor, radius, map, barrier);   // the ground turned to sky
            Register();
        }

        private void Register(bool rejoin = false)
        {
            info = Domains.Make(parent, anchor, radius, "Skyfall");
            if (rejoin) Domains.Rejoin(info); else Domains.Open(info);
        }

        private bool Left
        {
            get
            {
                var pawn = Pawn;
                if (pawn == null) return false;
                // collapsed already (on his death): the hediff goes too, or a resurrected caster is stuck with "already open"
                if (!anchor.IsValid) return true;
                return pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.Position.DistanceTo(anchor) > radius;   // downed too (her call 2026-11-12)
            }
        }

        public override bool CompShouldRemove => base.CompShouldRemove || Left;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (info == null && anchor.IsValid && Pawn?.Map != null) Register(rejoin: true);   // after loading a save
            acc += delta;
            if (acc < Props.interval) return;
            acc = 0;
            var pawn = Pawn; var map = pawn?.Map;
            if (map == null || !anchor.IsValid) return;
            dmgDef = dmgDef ?? DefDatabase<DamageDef>.GetNamedSilentFail("JJK_CursedEnergy") ?? DamageDefOf.Blunt;
            float dmg = Props.damage * Jujutsu.Power(pawn);
            // the sky cracking all over the domain, then the shockwave on every enemy inside
            for (int i = 0; i < Mathf.Clamp(Mathf.RoundToInt(radius), 4, 20); i++)
            {
                var c = anchor + GenRadial.RadialPattern[Rand.Range(1, GenRadial.NumCellsInRadius(radius))];
                if (c.InBounds(map)) FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(), map, 1.3f, new Color(0.75f, 0.85f, 1f));
            }
            // and a few breaks in the space itself, anywhere in the domain (each enemy hit gets its own in ThinIce.Break)
            for (int i = 0; i < Mathf.Clamp(Mathf.RoundToInt(radius / 4f), 2, 6); i++)
            {
                var c = anchor + GenRadial.RadialPattern[Rand.Range(0, GenRadial.NumCellsInRadius(radius))];
                if (c.InBounds(map)) ThinIce.Crack(c.ToVector3Shifted(), map, Rand.Range(2.5f, 4.5f));
            }
            bool sound = false;
            foreach (var v in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (v == pawn || v.Dead || !Domains.Hits(pawn, v) || v.Position.DistanceTo(anchor) > radius) continue;
                if (SureHit.Blocked(v)) continue;   // Hollow Wicker Basket, Simple Domain, Domain Amplification
                FleckMaker.Static(v.Position, map, FleckDefOf.PsycastAreaEffect, 3f);
                FleckMaker.Static(v.Position, map, FleckDefOf.ExplosionFlash, 2.5f);
                if (!sound) { SoundDefOf.Thunder_OnMap?.PlayOneShot(new TargetInfo(v.Position, map)); sound = true; }
                SureHit.Active = true;
                try
                {
                    ThinIce.Break(v, pawn);
                    var d = new DamageInfo(dmgDef, dmg, 1f, -1f, pawn);
                    d.SetIgnoreArmor(true);
                    v.TakeDamage(d);
                }
                finally { SureHit.Active = false; }
            }
        }

        public override void CompPostPostRemoved() { base.CompPostPostRemoved(); Collapse(); }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null) { base.Notify_PawnDied(dinfo, culprit); Collapse(); }

        private void Collapse()
        {
            if (info != null) Domains.Close(info);
            info = null;
            Barrier.Drop(barrier);
            anchor = IntVec3.Invalid;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref anchor, "uroDomainAnchor", IntVec3.Invalid);
            Scribe_Values.Look(ref radius, "uroDomainRadius", 0f);
            if (Scribe.mode == LoadSaveMode.Saving) barrier.RemoveAll(b => b == null || b.Destroyed);
            Scribe_Collections.Look(ref barrier, "uroDomainBarrier", LookMode.Reference);
            if (barrier == null) barrier = new List<Thing>();
        }
    }

    /// <summary>Gives Uro her domain (a Uro already in a save too), and has hostile Uro open it once three or more
    /// enemies are within 12 cells of her (proposal; the game's AI wouldn't use it on its own).</summary>
    public class UroDomainKeeper : GameComponent
    {
        public const int Gap = 3600;   // the ability's cooldown
        private int next;

        public UroDomainKeeper(Game game) { }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 120 != 47) return;
            var u = YutaTracker.Instance?.uro;
            if (u == null || u.Dead) return;
            var comp = u.GetComp<CompAbilities>();
            if (comp != null && UroDomainDefOf.JJK_UroDomainExpansion != null && !comp.HasAbility(UroDomainDefOf.JJK_UroDomainExpansion))
                comp.GiveAbility(UroDomainDefOf.JJK_UroDomainExpansion);
            if (now < next || !u.Spawned || u.Downed || u.Faction == null || !u.Faction.HostileTo(Faction.OfPlayer)) return;
            int near = u.Map.mapPawns.AllPawnsSpawned.Count(v => !v.Dead && !v.Downed && v.HostileTo(u) && v.Position.InHorDistOf(u.Position, 12f));
            if (near >= 3 && UroDomain.Open(u)) next = now + Gap;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref next, "uroDomainNext");
        }
    }
}
