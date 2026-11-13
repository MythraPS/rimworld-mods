using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace SukunaPsycasts
{
    /// <summary>
    /// While Sukuna is loose (arc stage Hunting or Lair), the remaining fingers are his and Uraume's priority (her calls
    /// 2026-11-11; every number is a proposal):
    ///  1. Finger quests are raced: their timers are halved, and if the player doesn't get there first, Sukuna takes
    ///     the fingers (they're added to him, so he's stronger; a letter says he got there first).
    ///  2. Raids for the colony's fingers: while the colony holds any (lying on a home map or carried by its people, not
    ///     the ones Yuji ate), about once every 10 days Uraume comes to take them, with Sukuna 30% of the time; if Uraume
    ///     is dead, Sukuna comes alone. They take what they can and leave; fingers carried off go to Sukuna.
    ///  3. Fingers he takes are in him at the lair, so beating him there drops or destroys them as before.
    ///  4. Finger rumours come half again as often (stacks with the pace setting; see QuestlineTracker).
    ///  5. A finger quest site may have Sukuna and/or Uraume on it, hunting the same fingers (25% per site): Uraume, plus
    ///     Sukuna half the time (Sukuna alone if Uraume is dead). Beating or exorcising him there ends the arc as usual.
    ///  6. SukunaAllies: later characters who want Sukuna restored (Jogo, Kenjaku) join these raids and encounters.
    /// </summary>
    public class SukunaHunt : GameComponent
    {
        public const float RaceTimer = 0.5f;
        public const float RaidMtbDays = 10f;
        public const float SukunaJoinsRaid = 0.3f;
        public const float EncounterChance = 0.25f;
        public const float SukunaAtEncounter = 0.5f;
        public const float RumourFaster = 1.5f;

        /// <summary>Later characters who want Sukuna restored (her note 2026-11-11: Jogo, Kenjaku). Each returns a pawn
        /// ready to be spawned (not spawned yet, alive, in Sukuna's cause), or null if they can't come; the raids for the
        /// colony's fingers and the finger-site encounters spawn whoever answers next to Uraume and Sukuna, on Sukuna's
        /// side. Empty for now.</summary>
        public static readonly List<Func<Map, Pawn>> SukunaAllies = new List<Func<Map, Pawn>>();

        private Dictionary<int, int> raced = new Dictionary<int, int>();   // quest id -> fingers in it
        private List<int> huntedSites = new List<int>();                   // finger sites Sukuna's side will be at
        private List<int> sitesDone = new List<int>();
        private List<Pawn> hunters = new List<Pawn>();                      // on a map right now, after fingers

        public SukunaHunt(Game game) { }
        public static SukunaHunt Instance => Current.Game?.GetComponent<SukunaHunt>();

        public static bool Loose()
        {
            var a = SukunaArc.Instance;
            return a != null && (a.stage == ArcStage.Hunting || a.stage == ArcStage.Lair) && SukunaArc.FreeSukunaAlive();
        }

        private static Pawn King => SukunaArc.Instance?.sukuna;

        /// <summary>Fingers that reach Sukuna without him eating them in front of you (carried off, a raced quest).</summary>
        public static void Gain(int n)
        {
            var k = King;
            if (k == null || k.Dead || n <= 0) return;
            n = Math.Min(n, 20 - Scaling.FingersEaten(k));
            if (n > 0) VesselMaker.Make(k, n);
        }

        // ------------------------------------------------------------------ 1 + 5: finger quests

        /// <summary>A finger quest has just been made: while he's loose, halve its timer and remember how many fingers
        /// it holds, and maybe put Sukuna's side on the site.</summary>
        public static void OnFingerQuest(Quest quest, int fingers)
        {
            var h = Instance;
            if (h == null || quest == null || !Loose()) return;
            foreach (var d in quest.PartsListForReading.OfType<QuestPart_Delay>())
                d.delayTicks = Math.Max(GenDate.TicksPerDay, (int)(d.delayTicks * RaceTimer));
            h.raced[quest.id] = fingers;
            var site = quest.PartsListForReading.OfType<QuestPart_SpawnWorldObject>().Select(p => p.worldObject as Site).FirstOrDefault(s => s != null);
            if (site != null && Rand.Chance(EncounterChance)) h.huntedSites.Add(site.ID);
        }

        public void QuestEnded(Quest quest, QuestEndOutcome outcome)
        {
            if (quest == null || !raced.TryGetValue(quest.id, out int n)) return;
            raced.Remove(quest.id);
            if (outcome != QuestEndOutcome.Fail || !Loose()) return;
            Gain(n);
            Find.LetterStack.ReceiveLetter("Sukuna got there first",
                $"The King of Curses reached the fingers before you did: {n} more of them are his now. He is stronger for it.",
                LetterDefOf.NegativeEvent);
        }

        // ------------------------------------------------------------------ ticking

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 60 == 29) Steer();
            if (now % 250 == 131) CheckSites();
            if (now % GenTicks.TickLongInterval != 900) return;
            if (!Loose() || hunters.Any(p => p != null && p.Spawned)) return;
            var map = Find.Maps.Where(m => m.IsPlayerHome && ColonyFingers(m) > 0).RandomElementWithFallback();
            if (map != null && Rand.MTBEventOccurs(RaidMtbDays, GenDate.TicksPerDay, GenTicks.TickLongInterval)) Raid(map);
        }

        /// <summary>Fingers on a home map that aren't inside anyone (lying around, stored, or carried in packs).</summary>
        public static int ColonyFingers(Map map)
        {
            var buf = new List<Thing>();
            ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForDef(SukunaThingDefOf.Sukuna_Finger), buf, true, null, false);
            return buf.Sum(t => t.stackCount);
        }

        private void Raid(Map map)
        {
            var ut = UraumeTracker.Instance;
            bool uraume = ut != null && !ut.Dead;
            bool king = !uraume || Rand.Chance(SukunaJoinsRaid);
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, 0.2f)) return;
            var came = new List<Pawn>();
            if (uraume) Bring(ut.Get(), map, cell, came);
            if (king) Bring(King, map, cell, came);
            foreach (var f in SukunaAllies) Bring(f(map), map, cell, came);
            if (came.Count == 0) return;
            string who = string.Join(" and ", came.Select(p => p.LabelShortCap));
            Find.LetterStack.ReceiveLetter("They have come for the fingers",
                $"{who} {(came.Count > 1 ? "have" : "has")} come for the fingers your colony holds. They will take every one they can, from "
                + "the floor or from whoever carries them, and leave. Any finger they carry off goes to the King of Curses.",
                LetterDefOf.ThreatBig, new LookTargets(came));
        }

        private void Bring(Pawn p, Map map, IntVec3 near, List<Pawn> came)
        {
            if (!SukunaArc.CanSpawnFromWorld(p)) return;   // dead, on a map (even carried or held), or in a caravan
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            if (p == King || UraumeTracker.Is(p)) SukunaArc.KeepForever(p);   // RemovePawn dropped them from the kept set
            var cell = CellFinder.RandomClosewalkCellNear(near, map, 4);
            GenSpawn.Spawn(p, cell, map);
            SukunaFaction.Claim(p);
            if (SukunaBehaviour.IsSukuna(p)) VesselMaker.ArmForAI(p);
            if (!hunters.Contains(p)) hunters.Add(p);
            came.Add(p);
        }

        private void CheckSites()
        {
            if (huntedSites.Count == 0) return;
            foreach (var map in Find.Maps)
            {
                if (!(map.Parent is Site site) || !huntedSites.Contains(site.ID) || sitesDone.Contains(site.ID)) continue;
                sitesDone.Add(site.ID);
                huntedSites.Remove(site.ID);
                if (!Loose()) continue;
                var finger = map.listerThings.ThingsOfDef(SukunaThingDefOf.Sukuna_Finger).FirstOrDefault();
                var near = finger?.Position ?? map.Center;
                var ut = UraumeTracker.Instance;
                bool uraume = ut != null && !ut.Dead;
                bool king = !uraume || Rand.Chance(SukunaAtEncounter);
                var came = new List<Pawn>();
                if (uraume) Bring(ut.Get(), map, near, came);
                if (king) Bring(King, map, near, came);
                foreach (var f in SukunaAllies) Bring(f(map), map, near, came);
                if (came.Count > 0)
                    Find.LetterStack.ReceiveLetter("Someone else is here for the fingers",
                        $"{string.Join(" and ", came.Select(p => p.LabelShortCap))} got here too, hunting the same fingers.",
                        LetterDefOf.ThreatBig, new LookTargets(came));
            }
        }

        /// <summary>Hunters go for the fingers: Sukuna eats any lying around himself (SukunaBehaviour); everyone takes
        /// them from whoever carries them; Uraume picks up loose ones. Holding fingers, or none left here: they leave.
        /// Gone off the map with fingers: those go to Sukuna.</summary>
        private void Steer()
        {
            for (int i = hunters.Count - 1; i >= 0; i--)
            {
                var p = hunters[i];
                if (p == null || p.Dead || p.Discarded) { hunters.RemoveAt(i); continue; }
                // captured or recruited: not hunting any more (their fingers stay with the colony)
                if (p.IsPrisonerOfColony || p.IsSlaveOfColony || p.Faction == Faction.OfPlayer) { hunters.RemoveAt(i); continue; }
                if (!p.Spawned && p.MapHeld != null) continue;   // carried or held on the map, not gone
                if (!p.Spawned)
                {
                    var carried = p.inventory?.innerContainer.Where(t => t.def == SukunaThingDefOf.Sukuna_Finger).ToList();
                    int n = carried?.Sum(t => t.stackCount) ?? 0;
                    if (n > 0 && !SukunaBehaviour.IsSukuna(p))
                    {
                        foreach (var t in carried) t.Destroy();
                        Gain(n);
                        Messages.Message($"{p.LabelShortCap} carried {n} finger{(n > 1 ? "s" : "")} off to the King of Curses.", MessageTypeDefOf.NegativeEvent, false);
                    }
                    hunters.RemoveAt(i);
                    continue;
                }
                if (p.Downed || p.InMentalState) continue;
                var map = p.Map;
                bool holding = p.inventory != null && p.inventory.innerContainer.Any(t => t.def == SukunaThingDefOf.Sukuna_Finger);
                Thing loose = GenClosest.ClosestThingReachable(p.Position, map, ThingRequest.ForDef(SukunaThingDefOf.Sukuna_Finger),
                    PathEndMode.ClosestTouch, TraverseParms.For(p), 9999f, t => t.Spawned);
                Pawn carrier = map.mapPawns.AllPawnsSpawned.Where(o => o != p && !o.Dead && o.HostileTo(p) && o.inventory != null
                        && o.inventory.innerContainer.Any(t => t.def == SukunaThingDefOf.Sukuna_Finger))
                    .OrderBy(o => o.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (holding || (loose == null && carrier == null))
                {
                    if (!(p.GetLord()?.LordJob is LordJob_ExitMapBest))
                    {
                        p.GetLord()?.RemovePawn(p);
                        LordMaker.MakeNewLord(p.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, true), map, new[] { p });
                    }
                    continue;
                }
                if (p.CurJobDef == JobDefOf.AttackMelee || p.CurJobDef == JobDefOf.Ingest || p.CurJobDef == JobDefOf.TakeInventory) continue;
                if (carrier != null && (loose == null || carrier.Position.DistanceToSquared(p.Position) < loose.Position.DistanceToSquared(p.Position)))
                {
                    var job = JobMaker.MakeJob(JobDefOf.AttackMelee, carrier);
                    job.expiryInterval = 300;
                    p.jobs.StartJob(job, JobCondition.InterruptForced);
                }
                else if (loose != null && !SukunaBehaviour.IsSukuna(p))
                {
                    var job = JobMaker.MakeJob(JobDefOf.TakeInventory, loose);
                    job.count = loose.stackCount;
                    p.jobs.StartJob(job, JobCondition.InterruptForced);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref raced, "sukunaHuntRaced", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref huntedSites, "sukunaHuntSites", LookMode.Value);
            Scribe_Collections.Look(ref sitesDone, "sukunaHuntSitesDone", LookMode.Value);
            Scribe_Collections.Look(ref hunters, "sukunaHunters", LookMode.Reference);
            if (raced == null) raced = new Dictionary<int, int>();
            if (huntedSites == null) huntedSites = new List<int>();
            if (sitesDone == null) sitesDone = new List<int>();
            if (hunters == null) hunters = new List<Pawn>();
            hunters.RemoveAll(p => p == null);
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.End))]
    public static class Patch_SukunaRacedQuest
    {
        public static void Postfix(Quest __instance, QuestEndOutcome outcome) => SukunaHunt.Instance?.QuestEnded(__instance, outcome);
    }
}
