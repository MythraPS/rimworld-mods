using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using VanillaPsycastsExpanded;

namespace SukunaPsycasts
{
    /// <summary>
    /// Uraume (her ask 2026-11-11), Sukuna's servant. Made once per game, in the King of Curses' hidden faction and
    /// ideoligion. The whole Frostshaper path (VPE), and of the Fundamentals Cursed Energy Reinforcement, Reverse
    /// Cursed Technique and Air Step.
    /// Arrival (her pick): when Sukuna takes over a host and heads off the map, Uraume walks in from the map edge
    /// nearest him, goes to him (cutting down anyone hostile in the way) and leaves with him. If Uraume survives, he
    /// is at the lair beside Sukuna when the colony attacks it. If he dies, he is gone for good.
    /// </summary>
    public class UraumeTracker : GameComponent
    {
        public static UraumeTracker Instance;

        public Pawn uraume;
        public bool arrived;
        /// <summary>On the map right now, on his way to Sukuna.</summary>
        public bool escorting;

        /// <summary>Uraume keeps four of the twenty fingers (her call 2026-11-12): they never turn up in rumours, and he
        /// gives them, with Sukuna's old body's head, to Sukuna when the city opens. If he dies first they fall out.</summary>
        public bool holdsFingers = true;
        public const int HeldFingers = 4;

        public UraumeTracker(Game game) { Instance = this; }

        /// <summary>The four fingers and the head (it counts as the last finger: full power).</summary>
        public void HandOver(Pawn king)
        {
            if (!holdsFingers || Dead || king == null || king.Dead) return;
            holdsFingers = false;
            VesselMaker.Make(king, HeldFingers + 1);
            Find.LetterStack.ReceiveLetter("Uraume's gift",
                "Uraume has given Sukuna the four fingers they kept for him, and the head of his old body, which takes the place of "
                + "the last finger. The King of Curses is at full power.", LetterDefOf.ThreatBig);
        }

        public override void GameComponentTick()
        {
            if (holdsFingers && Dead && Find.TickManager.TicksGame % 250 == 61) DropHeld();
            TickEscort();
        }

        /// <summary>Uraume died holding them: they fall out where the body is, or are lost with it.</summary>
        private void DropHeld()
        {
            holdsFingers = false;
            var corpse = uraume?.Corpse;
            if (corpse != null && corpse.Spawned && SukunaThingDefOf.Sukuna_Finger != null)
            {
                var f = ThingMaker.MakeThing(SukunaThingDefOf.Sukuna_Finger);
                f.stackCount = HeldFingers;
                GenPlace.TryPlaceThing(f, corpse.Position, corpse.Map, ThingPlaceMode.Near);
                Messages.Message("Uraume was carrying four of Sukuna's fingers. They fell out.", corpse, MessageTypeDefOf.PositiveEvent, false);
            }
            else if (SukunaArc.Instance != null) SukunaArc.Instance.fingersDestroyed += HeldFingers;
        }

        /// <summary>An Uraume made before he was given his thousand years (2026-11-11) gets them on load.</summary>
        public override void LoadedGame()
        {
            base.LoadedGame();
            var at = uraume?.ageTracker;
            if (at != null && at.AgeChronologicalYearsFloat < 1000f)
                at.AgeChronologicalTicks = at.AgeBiologicalTicks + (long)(1000f * GenDate.TicksPerYear);
            // tough, brawler, jogger (her call 2026-11-11) for an Uraume made before
            var traits = uraume?.story?.traits;
            if (traits != null)
                foreach (var e in new[] { ("Tough", 0), ("Brawler", 0), ("SpeedOffset", 2) })
                {
                    var d = DefDatabase<TraitDef>.GetNamedSilentFail(e.Item1);
                    if (d != null && !traits.HasTrait(d)) traits.GainTrait(new Trait(d, e.Item2, true));
                }
        }

        public static bool Is(Pawn p) => p != null && Instance?.uraume == p;
        public bool Dead => uraume != null && (uraume.Dead || uraume.Discarded);

        public Pawn Get()
        {
            if (uraume == null)
            {
                uraume = Uraume_.Make();
                Find.WorldPawns.ForcefullyKeptPawns.Add(uraume);
            }
            return uraume;
        }

        /// <summary>Sukuna is free and heading out: Uraume comes for him.</summary>
        public void ComeFor(Pawn king)
        {
            if (arrived || Dead || king?.Map == null) return;
            var map = king.Map;
            var cell = EdgeNear(map, king.Position);
            if (!cell.IsValid) return;
            var p = Get();
            if (!SukunaArc.CanSpawnFromWorld(p)) return;
            arrived = true;
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            SukunaArc.KeepForever(p);   // RemovePawn dropped him from the kept set
            Uraume_.Serve(p);
            GenSpawn.Spawn(p, cell, map);
            escorting = true;
            Find.LetterStack.ReceiveLetter("Uraume",
                "Uraume, Sukuna's servant, has come for him. Uraume will meet the King of Curses and leave with him, and will cut down anyone in the way.",
                LetterDefOf.ThreatBig, p);
        }

        private static IntVec3 EdgeNear(Map map, IntVec3 from)
        {
            return CellRect.WholeMap(map).EdgeCells
                .Where(c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReach(c, from, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)))
                .OrderBy(c => c.DistanceToSquared(from))
                .FirstOrFallback(IntVec3.Invalid);
        }

        /// <summary>The lair map is made: Uraume stands beside Sukuna, fighting the colony.</summary>
        public void AtLair(Map map, IntVec3 near)
        {
            if (Dead || map == null) return;
            var p = Get();
            if (!SukunaArc.CanSpawnFromWorld(p)) return;
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            SukunaArc.KeepForever(p);
            if (!CellFinder.TryFindRandomCellNear(near, map, 4, c => c.Standable(map), out var cell)) cell = near;
            GenSpawn.Spawn(p, cell, map);
            SukunaFaction.Claim(p);   // the King's faction, his ideoligion, and an assault lord like Sukuna's
            escorting = false;
        }

        private void TickEscort()
        {
            if (!escorting || Find.TickManager.TicksGame % 60 != 23) return;
            var p = uraume;
            if (p == null || p.Dead || !p.Spawned) { escorting = false; return; }
            if (p.IsPrisonerOfColony || p.IsSlaveOfColony || p.Faction == Faction.OfPlayer) { escorting = false; return; }   // captured
            if (p.Downed || p.InMentalState) return;
            var king = SukunaArc.Instance?.sukuna;
            bool kingHere = king != null && king.Spawned && !king.Dead && king.Map == p.Map;

            // anyone hostile in the way gets cut down first
            if (p.CurJobDef != JobDefOf.AttackMelee)
            {
                var blocker = GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 2.9f, true).OfType<Pawn>()
                    .FirstOrDefault(o => o != p && !o.Dead && !o.Downed && o.HostileTo(p));
                if (blocker != null)
                {
                    var job = JobMaker.MakeJob(JobDefOf.AttackMelee, blocker);
                    job.expiryInterval = 300;
                    p.jobs.StartJob(job, JobCondition.InterruptForced);
                    return;
                }
            }
            if (p.CurJobDef == JobDefOf.AttackMelee) return;

            var lord = p.GetLord();
            if (kingHere && !p.Position.InHorDistOf(king.Position, 4f) && (lord == null || lord != king.GetLord()))
            {
                var go = JobMaker.MakeJob(JobDefOf.Goto, king);
                go.locomotionUrgency = LocomotionUrgency.Jog;
                go.expiryInterval = 120;
                go.checkOverrideOnExpire = true;
                p.jobs.StartJob(go, JobCondition.InterruptForced);
                return;
            }
            // beside him: join his way out; or he's already gone: follow him out
            if (lord != null) return;
            var kingLord = kingHere ? king.GetLord() : null;
            if (kingLord != null) kingLord.AddPawn(p);
            else LordMaker.MakeNewLord(p.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, true), p.Map, new[] { p });
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_References.Look(ref uraume, "uraume", true);
            Scribe_Values.Look(ref arrived, "uraumeArrived");
            Scribe_Values.Look(ref escorting, "uraumeEscorting");
            Scribe_Values.Look(ref holdsFingers, "uraumeHoldsFingers", true);
        }
    }

    public static class Uraume_
    {
        /// <summary>Enough to hold all of Frostshaper (10 techniques) and the three Fundamentals (proposal).</summary>
        public const int PsycastLevel = 14;

        public static PsycasterPathDef Frostshaper => DefDatabase<PsycasterPathDef>.GetNamedSilentFail("VPE_Frostshaper");

        /// <summary>Age has no canon number (a Heian-era sorcerer in a modern body); 25 is a proposal.</summary>
        public static Pawn Make()
        {
            var p = JujutsuFundamentals.Characters.Make("Uraume", "Uraume", "", Gender.Male, 25f);
            p.Name = new NameSingle("Uraume");
            // body 25, but a Heian-era sorcerer like Sukuna: about a thousand years old (her call 2026-11-11)
            if (p.ageTracker != null) p.ageTracker.AgeChronologicalTicks = (long)(1025f * GenDate.TicksPerYear);
            JujutsuFundamentals.Looks.Uraume(p);
            // canon: Sukuna's cook (skills are proposals)
            JujutsuFundamentals.Characters.Skill(p, SkillDefOf.Melee, 12, Passion.Major);
            JujutsuFundamentals.Characters.Skill(p, SkillDefOf.Cooking, 14, Passion.Major);
            JujutsuFundamentals.Characters.Psycaster(p, PsycastLevel);
            if (Frostshaper != null) JujutsuFundamentals.Characters.Learn(p, Frostshaper);
            JujutsuFundamentals.Characters.Learn(p, JujutsuFundamentals.JJKDefOf.JJK_CursedEnergyFundamentals,
                JujutsuFundamentals.JJKAbilityDefOf.JJK_Reinforcement, JujutsuFundamentals.JJKAbilityDefOf.JJK_ReverseCursedTechnique,
                JujutsuFundamentals.JJKAbilityDefOf.JJK_AirStep);
            JujutsuFundamentals.PassiveSync.Sync(p);
            p.equipment?.DestroyAllEquipment();
            p.Notify_DisabledWorkTypesChanged();
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            return p;
        }

        /// <summary>Sukuna's faction and the King's ideoligion, without SukunaFaction.Claim's assault lord.</summary>
        public static void Serve(Pawn p)
        {
            var f = SukunaFaction.Get();
            if (p.Faction != f) p.SetFaction(f);
            CharacterIdeos.Give(p, f.ideos?.PrimaryIdeo);
        }
    }

    public static class UraumeDebug
    {
        private const string Cat = "Jujutsu Kaisen";

        [DebugAction(Cat, "Uraume: spawn (hostile, Sukuna's)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnHostile()
        {
            var t = UraumeTracker.Instance; var map = Find.CurrentMap; var c = UI.MouseCell();
            if (t == null || map == null || !c.InBounds(map)) return;
            if (t.Dead) { Messages.Message("Uraume is dead.", MessageTypeDefOf.RejectInput, false); return; }
            var p = t.Get();
            if (p.Spawned) { Messages.Message("Uraume is already on a map.", MessageTypeDefOf.RejectInput, false); return; }
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            SukunaArc.KeepForever(p);
            GenSpawn.Spawn(p, c, map);
            SukunaFaction.Claim(p);
        }

        [DebugAction(Cat, "Uraume: spawn as colonist (testing)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnColonist()
        {
            var t = UraumeTracker.Instance; var map = Find.CurrentMap; var c = UI.MouseCell();
            if (t == null || map == null || !c.InBounds(map)) return;
            if (t.Dead) { Messages.Message("Uraume is dead.", MessageTypeDefOf.RejectInput, false); return; }
            var p = t.Get();
            if (p.Spawned) { Messages.Message("Uraume is already on a map.", MessageTypeDefOf.RejectInput, false); return; }
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(p, c, map);
        }

        [DebugAction(Cat, "Uraume: come for Sukuna now (Sukuna must be on the map)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ComeNow()
        {
            var king = SukunaArc.Instance?.sukuna;
            if (king == null || !king.Spawned) { Messages.Message("Sukuna isn't on a map.", MessageTypeDefOf.RejectInput, false); return; }
            if (UraumeTracker.Instance != null) UraumeTracker.Instance.arrived = false;
            UraumeTracker.Instance?.ComeFor(king);
        }
    }
}
