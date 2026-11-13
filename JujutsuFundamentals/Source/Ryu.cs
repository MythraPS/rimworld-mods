using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Sound;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class JJKRyuDefOf
    {
        public static IncidentDef JJK_RyuChallenge;
        public static HediffDef JJK_RyuDomain;
        public static HediffDef JJK_RyuOutput;
        public static VEFAbilityDef JJK_GraniteBlast;
        public static VEFAbilityDef JJK_GraniteRapidFire;
        public static VEFAbilityDef JJK_RyuDomainExpansion;
        static JJKRyuDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(JJKRyuDefOf));
    }

    /// <summary>
    /// Ryu Ishigori (her calls 2026-11-10). His cursed technique, Cursed Energy Discharge: Granite Blast,
    /// a stronger Cursed Energy Blast, as one powerful shot or a burst of smaller rapid ones. His Cursed
    /// Energy Reinforcement is extra strong. Canon never shows his domain; hers: a barrier domain where
    /// granite blasts fall from the sky on every enemy of his inside. He arrives as an enemy looking for a
    /// fight; beat him and capture him to recruit him.
    /// </summary>
    public static class Ryu
    {
        /// <summary>Extra-strong Reinforcement (proposal): x1.5 its strength.</summary>
        public const float ReinforcementFactor = 1.5f;

        public static bool Is(Pawn p) => p != null && CharacterTracker.Instance?.ryu == p;

        public static Pawn Make()
        {
            var req = new PawnGenerationRequest(PawnKindDefOf.Colonist, faction: null, context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, mustBeCapableOfViolence: true, allowAddictions: false,
                fixedGender: Gender.Male, forcedXenotype: XenotypeDefOf.Baseliner, developmentalStages: DevelopmentalStage.Adult,
                // an early-Edo sorcerer, about 400 years ago, reincarnated into a new body (Villains Wiki); the body's
                // age isn't given, so 28 (her call 2026-11-10), and 400 years on top of it chronologically
                fixedBiologicalAge: 28f, fixedChronologicalAge: 428f);
            Pawn p;
            NoMagicClass.making = true;
            try { p = PawnGenerator.GeneratePawn(req); }
            finally { NoMagicClass.making = false; }
            PointSettle.Queue(p);   // no points left to spend once made (her call 2026-11-11)
            p.Name = new NameTriple("Ryu", "Ryu", "Ishigori");
            var melee = p.skills?.GetSkill(SkillDefOf.Melee);
            if (melee != null) { melee.Level = Mathf.Max(melee.Level, 12); melee.passion = Passion.Minor; }
            var shoot = p.skills?.GetSkill(SkillDefOf.Shooting);
            if (shoot != null) { shoot.Level = Mathf.Max(shoot.Level, 14); shoot.passion = Passion.Major; }
            Characters.Psycaster(p, 10);
            // Cursed Energy Blast (shown as his Granite Blast instead) and Air Step (her call 2026-11-10)
            Characters.Learn(p, JJKDefOf.JJK_CursedEnergyFundamentals, JJKAbilityDefOf.JJK_Reinforcement, JJKAbilityDefOf.JJK_Blast, JJKAbilityDefOf.JJK_AirStep);
            var comp = p.GetComp<CompAbilities>();
            foreach (var a in new[] { JJKRyuDefOf.JJK_GraniteBlast, JJKRyuDefOf.JJK_GraniteRapidFire, JJKRyuDefOf.JJK_RyuDomainExpansion })
                if (comp != null && a != null && !comp.HasAbility(a)) comp.GiveAbility(a);
            p.equipment?.DestroyAllEquipment();
            Looks.Ryu(p);
            SyncOutput(p);
            Innate.OpenPath(p, Innate.GranitePath);   // his Granite Blast path (2026-11-12)
            PassiveSync.Sync(p);
            // ideoligion (my pick 2026-11-11, she adjusts after testing): his own, he fights for himself
            JujutsuFundamentals.CharacterIdeos.Give(p, JujutsuFundamentals.CharacterIdeos.Get("ryu", RimWorld.FactionDefOf.PlayerColony, "Individualist"));
            Looks.Refresh(p);
            Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            return p;
        }

        /// <summary>His huge cursed energy output (her call 2026-11-11: in canon he fires off his techniques far more often in
        /// one fight). A hediff, so a Ryu already in a save gets it too (RyuOutputKeeper).</summary>
        public static void SyncOutput(Pawn p)
        {
            if (p?.health == null || p.Dead || JJKRyuDefOf.JJK_RyuOutput == null) return;
            if (!p.health.hediffSet.HasHediff(JJKRyuDefOf.JJK_RyuOutput)) p.health.AddHediff(JJKRyuDefOf.JJK_RyuOutput);
        }

        /// <summary>He comes as an enemy, alone, to fight the colony.</summary>
        public static bool ArriveHostile(Map map)
        {
            var t = CharacterTracker.Instance;
            if (t == null || map == null) return false;
            if (t.ryu == null) t.ryu = Make();
            var p = t.ryu;
            if (p.Dead || p.Spawned) return false;
            var faction = Find.FactionManager.RandomEnemyFaction(false, false, false);
            if (faction == null) return false;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, 0f)) return false;
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            if (p.Faction != faction) p.SetFaction(faction);
            GenSpawn.Spawn(p, cell, map);
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction), map, new List<Pawn> { p });
            t.ryuArrived = true;
            Find.LetterStack.ReceiveLetter("Ryu Ishigori", "Ryu Ishigori has come looking for someone strong enough to fight him, and he has picked your colony. Beat him and capture him, and he may be recruited.",
                                           LetterDefOf.ThreatBig, p);
            return true;
        }
    }

    public class IncidentWorker_RyuChallenge : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) =>
            CharacterTracker.Instance != null && !CharacterTracker.Instance.ryuArrived && parms.target is Map;

        protected override bool TryExecuteWorker(IncidentParms parms) => Ryu.ArriveHostile((Map)parms.target);
    }

    /// <summary>Rapid fire: smaller Granite Blasts, one every few ticks.</summary>
    public class Ability_GraniteRapidFire : Ability_ShootProjectile
    {
        public const int Shots = 8;
        public const int Gap = 5;

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);   // the first shot
            var target = targets.FirstOrDefault();
            for (int i = 1; i < Shots; i++) RapidFire.Queue(this, target, Find.TickManager.TicksGame + i * Gap);
        }

        public void FireOne(GlobalTargetInfo target)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Downed) return;
            if (target.HasThing && (!target.Thing.Spawned || target.Thing.Map != pawn.Map)) return;
            ShootProjectile(target);
        }
    }

    public class RapidFire : GameComponent
    {
        private static readonly List<(Ability_GraniteRapidFire a, GlobalTargetInfo t, int at)> queue = new List<(Ability_GraniteRapidFire, GlobalTargetInfo, int)>();

        public RapidFire(Game game) { queue.Clear(); }

        public static void Queue(Ability_GraniteRapidFire a, GlobalTargetInfo t, int at) => queue.Add((a, t, at));

        public override void GameComponentTick()
        {
            if (queue.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (queue[i].at > now) continue;
                var q = queue[i];
                queue.RemoveAt(i);
                q.a.FireOne(q.t);
            }
        }
    }

    /// <summary>His domain costs what Gojo's does: half the psyfocus pool (times his psyfocus cost factor, so a fifth for Ryu
    /// with his output; 2026-11-11) and half the neural heat limit (proposal).</summary>
    public class Ability_RyuDomain : Ability
    {
        public const float Psyfocus = 0.5f;
        private static StatDef costStat;
        public static float Cost(Pawn p)
        {
            costStat = costStat ?? DefDatabase<StatDef>.GetNamedSilentFail("VPE_PsyfocusCostFactor");
            return Psyfocus * (costStat == null ? 1f : Mathf.Clamp(p.GetStatValue(costStat), 0.1f, 1f));
        }

        public override bool IsEnabledForPawn(out string reason)
        {
            if ((pawn.psychicEntropy?.CurrentPsyfocus ?? 0f) < Cost(pawn)) { reason = $"Needs {Cost(pawn):P0} psyfocus."; return false; }
            if (Jujutsu.Has(pawn, JJKRyuDefOf.JJK_RyuDomain)) { reason = "His domain is already open."; return false; }
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            pawn.psychicEntropy?.OffsetPsyfocusDirectly(-Cost(pawn));
            Domains.Strain(pawn);   // not a calamity grade: neural heat to 90% of his limit (her call 2026-11-10)
            pawn.health.AddHediff(JJKRyuDefOf.JJK_RyuDomain);
        }
    }

    public class HediffCompProperties_RyuDomain : HediffCompProperties
    {
        public float radius = 11.9f;
        public float maxRadius = 30f;
        public int interval = 90;
        public float damage = 24f;
        public HediffCompProperties_RyuDomain() => compClass = typeof(HediffComp_RyuDomain);
    }

    /// <summary>Ryu's domain (hers): a barrier around where he opened it; every so often a granite blast falls
    /// from the sky on every enemy of his inside, as a sure hit. It collapses if he leaves it, dies, or loses a clash.</summary>
    public class HediffComp_RyuDomain : HediffComp
    {
        private IntVec3 anchor = IntVec3.Invalid;
        private float radius;
        private List<Thing> barrier = new List<Thing>();
        private int acc;
        private DomainInfo info;

        public HediffCompProperties_RyuDomain Props => (HediffCompProperties_RyuDomain)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            var pawn = Pawn; var map = pawn?.Map;
            if (map == null) return;
            anchor = pawn.Position;
            radius = Mathf.Min(Props.radius * Jujutsu.Sensitivity(pawn) * Jujutsu.Boost(pawn), Props.maxRadius);
            barrier = Barrier.Raise(pawn, anchor, radius, map);
            Barrier.Ground("JJK_GroundGraniteTsunami", anchor, radius, map, barrier);   // scorched ground
            Register();
        }

        private void Register(bool rejoin = false)
        {
            info = Domains.Make(parent, anchor, radius, "Granite Tsunami");
            if (rejoin) Domains.Rejoin(info); else Domains.Open(info);
        }

        private bool Left
        {
            get
            {
                var pawn = Pawn;
                if (pawn == null || !anchor.IsValid) return false;
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
            float dmg = Props.damage * Jujutsu.Power(pawn);
            // her calls: blasts also hit the open ground all over the domain, even with nobody there (2026-11-10; only a
            // visual: no damage, never on an ally's cell), and they look like Granite Blasts falling from the sky
            // (2026-11-11, RyuSkyBlasts): spread over the interval so they rain down steadily, the hits landing on impact
            int falls = Mathf.Clamp(Mathf.RoundToInt(radius * 1.2f), 6, 30);
            for (int i = 0; i < falls; i++)
            {
                var c = anchor + GenRadial.RadialPattern[Rand.Range(1, GenRadial.NumCellsInRadius(radius))];
                if (!c.InBounds(map) || c.GetFirstPawn(map) != null) continue;
                RyuSkyBlasts.Drop(map, c, Rand.Range(0, Props.interval), false);
            }
            foreach (var victim in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (victim == pawn || victim.Dead || !Domains.Hits(pawn, victim) || victim.Position.DistanceTo(anchor) > radius) continue;
                var v = victim; var caster = pawn;
                RyuSkyBlasts.Drop(map, v.Position, Rand.Range(0, 12), true, () =>
                {
                    if (v.Dead || !v.Spawned || caster == null) return;
                    SureHit.Active = true;
                    try { v.TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamed("JJK_CursedEnergy"), dmg, 0.6f, -1f, caster)); }
                    finally { SureHit.Active = false; }
                });
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            Collapse();
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            Collapse();
        }

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
            Scribe_Values.Look(ref anchor, "ryuDomainAnchor", IntVec3.Invalid);
            Scribe_Values.Look(ref radius, "ryuDomainRadius", 0f);
            if (Scribe.mode == LoadSaveMode.Saving) barrier.RemoveAll(b => b == null || b.Destroyed);
            Scribe_Collections.Look(ref barrier, "ryuDomainBarrier", LookMode.Reference);
            if (barrier == null) barrier = new List<Thing>();
        }
    }

    public class RyuOutputKeeper : GameComponent
    {
        public RyuOutputKeeper(Game game) { }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 2500 != 223) return;
            var r = CharacterTracker.Instance?.ryu;
            if (r != null) Ryu.SyncOutput(r);
        }
    }
}
