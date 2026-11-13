using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace SukunaPsycasts
{
    /// <summary>
    /// Yuji's regard follows what actually happens, not the button pressed (her call 2026-11-11; numbers are proposals):
    ///  - the wounded stranger: +8 once they are rescued (in a bed) or all their wounds are tended, alive; -8 if they die
    ///    first, or nobody has helped them within 2 days;
    ///  - the curse user: Yuji comes and fights beside the colony (if he hasn't joined yet he walks in as a friendly
    ///    fighter and leaves after); +10 once the raid is beaten (none of them left standing on the map) within a day.
    /// </summary>
    public class DilemmaWatch : GameComponent
    {
        public const int WoundedDeadline = 2 * GenDate.TicksPerDay;
        public const int RaidDeadline = GenDate.TicksPerDay;

        public Pawn wounded;
        public int woundedSince = -1;
        public Faction raidFaction;
        public int raidSince = -1;
        public bool raidSeen;
        public Pawn helper;          // Yuji, when he came only for this fight
        public Faction helperFaction;
        public bool restoreHelperFaction;   // set his faction back to helperFaction once he has left

        public DilemmaWatch(Game game) { }
        public static DilemmaWatch Instance => Current.Game?.GetComponent<DilemmaWatch>();

        public static void WatchWounded(Pawn p)
        {
            var w = Instance;
            if (w == null || p == null) return;
            w.wounded = p; w.woundedSince = Find.TickManager.TicksGame;
        }

        /// <summary>Yuji comes to fight beside the colony against <paramref name="faction"/>'s raid.</summary>
        public static void WatchRaid(Map map, Faction faction)
        {
            var w = Instance;
            if (w == null || map == null || faction == null) return;
            w.raidFaction = faction; w.raidSince = Find.TickManager.TicksGame; w.raidSeen = false;
            var t = QuestlineTracker.Instance;
            if (t == null || t.stage == YujiStage.Joined || t.stage == YujiStage.Gone) return;   // a colonist already, or dead
            var yuji = t.GetOrMakeYuji();
            if (yuji == null || yuji.Dead || yuji.MapHeld != null) return;
            var ally = Find.FactionManager.RandomNonHostileFaction(false, false, false, TechLevel.Undefined);
            if (ally == null) return;
            if (yuji.IsWorldPawn()) Find.WorldPawns.RemovePawn(yuji);
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, 0.2f, false, null)) cell = CellFinder.RandomEdgeCell(map);
            GenSpawn.Spawn(yuji, cell, map);
            w.helperFaction = yuji.Faction;
            w.restoreHelperFaction = true;
            yuji.SetFaction(ally);
            LordMaker.MakeNewLord(ally, new LordJob_AssistColony(ally, map.Center), map, new[] { yuji });
            w.helper = yuji;
            if (!Find.WorldPawns.ForcefullyKeptPawns.Contains(yuji)) Find.WorldPawns.ForcefullyKeptPawns.Add(yuji);
            Messages.Message("Yuji is here to fight the curse user with you.", yuji, MessageTypeDefOf.PositiveEvent, false);
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 != 83) return;
            var t = QuestlineTracker.Instance;
            if (t == null) return;

            if (wounded != null)
            {
                bool helped = !wounded.Dead && (wounded.InBed() || !wounded.health.HasHediffsNeedingTend());
                if (helped) { t.ChangeRegard(8f, "you took in a wounded stranger"); wounded = null; }
                else if (wounded.Dead || now - woundedSince > WoundedDeadline)
                {
                    t.ChangeRegard(-8f, "the stranger you agreed to take in was left to die");
                    wounded = null;
                }
            }

            if (raidFaction != null)
            {
                var map = helper?.MapHeld ?? Find.AnyPlayerHomeMap;
                bool standing = map != null && map.mapPawns.AllPawnsSpawned.Any(p => p.Faction == raidFaction && !p.Dead && !p.Downed && !p.IsPrisoner);
                if (standing) raidSeen = true;
                if (raidSeen && !standing)
                {
                    t.ChangeRegard(10f, "you stood with him against a curse user");
                    EndRaid();
                }
                else if (now - raidSince > RaidDeadline) EndRaid();
            }

            // Yuji came only for the fight: once he is off the map he no longer belongs to the faction he fought with
            if (restoreHelperFaction && helper == null)
            {
                var y = t.yuji;
                if (y == null || y.Dead || t.stage == YujiStage.Joined) restoreHelperFaction = false;
                else if (y.MapHeld == null)
                {
                    if (y.Faction != helperFaction) y.SetFaction(helperFaction);
                    restoreHelperFaction = false;
                }
            }
        }

        /// <summary>The fight is over: Yuji, if he only came for it, heads off.</summary>
        private void EndRaid()
        {
            raidFaction = null; raidSince = -1; raidSeen = false;
            var y = helper;
            helper = null;
            if (y == null || y.Dead || !y.Spawned || QuestlineTracker.Instance?.stage == YujiStage.Joined) return;
            y.GetLord()?.RemovePawn(y);
            LordMaker.MakeNewLord(y.Faction, new LordJob_ExitMapBest(Verse.AI.LocomotionUrgency.Walk, false, true), y.Map, new[] { y });
            Messages.Message("Yuji waves and heads off.", y, MessageTypeDefOf.NeutralEvent, false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref wounded, "yujiDilemmaWounded");
            Scribe_Values.Look(ref woundedSince, "yujiDilemmaWoundedSince", -1);
            Scribe_References.Look(ref raidFaction, "yujiDilemmaRaidFaction");
            Scribe_Values.Look(ref raidSince, "yujiDilemmaRaidSince", -1);
            Scribe_Values.Look(ref raidSeen, "yujiDilemmaRaidSeen");
            Scribe_References.Look(ref helper, "yujiDilemmaHelper", true);
            Scribe_References.Look(ref helperFaction, "yujiDilemmaHelperFaction");
            Scribe_Values.Look(ref restoreHelperFaction, "yujiDilemmaRestoreFaction");
        }
    }
}
