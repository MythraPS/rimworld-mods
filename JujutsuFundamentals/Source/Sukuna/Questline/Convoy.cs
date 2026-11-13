using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace SukunaPsycasts
{
    /// <summary>
    /// The convoy (her call 2026-11-11: it used to be the same outpost site as "held"). A group from another faction,
    /// friendly or neutral if any exist, else hostile, crosses the colony's home map from one edge to the other, one of
    /// them carrying fingers or Death Paintings. A letter warns a few hours ahead (proposal: 3-6 in-game hours). Taking
    /// them is the player's choice: attacking the group angers its faction the vanilla way. If they leave the map with
    /// them, the relics go back out into the world (the census counts them as unfound again).
    /// </summary>
    public class RelicConvoy : GameComponent
    {
        public static readonly IntRange WarnTicks = new IntRange(7500, 15000);   // 3-6 in-game hours

        private class Run : IExposable
        {
            public ThingDef def;
            public int count, arriveTick = -1, mapId = -1;
            public Faction faction;
            public Pawn carrier;
            public List<Pawn> pawns = new List<Pawn>();
            public bool arrived;

            public void ExposeData()
            {
                Scribe_Defs.Look(ref def, "def");
                Scribe_Values.Look(ref count, "count");
                Scribe_Values.Look(ref arriveTick, "arriveTick", -1);
                Scribe_Values.Look(ref mapId, "mapId", -1);
                Scribe_References.Look(ref faction, "faction");
                Scribe_References.Look(ref carrier, "carrier");
                Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
                Scribe_Values.Look(ref arrived, "arrived");
                if (pawns == null) pawns = new List<Pawn>();
            }
        }

        private List<Run> runs = new List<Run>();

        public RelicConvoy(Game game) { }

        public static RelicConvoy Instance => Current.Game?.GetComponent<RelicConvoy>();

        private static bool IsFinger(ThingDef d) => d == SukunaThingDefOf.Sukuna_Finger;

        /// <summary>Relics announced but not on the map yet: the census counts them as found, so no other rumour hands them out.</summary>
        public static int Pending(ThingDef def) => Instance?.runs.Where(r => !r.arrived && r.def == def).Sum(r => r.count) ?? 0;

        public static int Count(int remaining) => Mathf.Clamp(QuestStash.PerQuest.RandomInRange, 1, Mathf.Max(1, remaining));

        public static Faction PickFaction(bool allowHostile)
        {
            var all = Find.FactionManager.AllFactionsListForReading
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.def.pawnGroupMakers != null).ToList();
            return all.Where(f => !f.HostileTo(Faction.OfPlayer)).RandomElementWithFallback()
                   ?? (allowHostile ? all.RandomElementWithFallback() : null);
        }

        /// <summary>Announces a convoy carrying <paramref name="count"/> of <paramref name="def"/> across <paramref name="map"/>.</summary>
        public static bool Announce(Map map, ThingDef def, int count)
        {
            var inst = Instance;
            if (inst == null || map == null || def == null || count <= 0) return false;
            var faction = PickFaction(Find.Storyteller.difficulty.allowViolentQuests);
            if (faction == null) return false;
            var r = new Run { def = def, count = count, faction = faction, mapId = map.uniqueID,
                              arriveTick = Find.TickManager.TicksGame + WarnTicks.RandomInRange };
            inst.runs.Add(r);
            int hours = Mathf.Max(1, Mathf.RoundToInt((r.arriveTick - Find.TickManager.TicksGame) / 2500f));
            string what = IsFinger(def)
                ? (count == 1 ? "a cursed finger, sealed in a lacquered box" : $"{count} cursed fingers, sealed in a lacquered box")
                : (count == 1 ? "a Death Painting in a jar of black glass" : $"{count} Death Paintings in jars of black glass");
            Find.LetterStack.ReceiveLetter(IsFinger(def) ? "Finger convoy coming" : "Painting convoy coming",
                $"A group from {faction.Name} will cross your land in about {hours} hours, on their way somewhere else. One of them is "
                + $"carrying {what}.\n\nThey won't hand it over. If you want it, you'll have to take it from them before they leave the "
                + "map, and attacking them will anger " + faction.Name + ". If they get away with it, it will turn up again somewhere else.",
                LetterDefOf.NeutralEvent);
            return true;
        }

        public override void GameComponentTick()
        {
            if (runs.Count == 0 || Find.TickManager.TicksGame % 250 != 17) return;
            int now = Find.TickManager.TicksGame;
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                var r = runs[i];
                var map = Find.Maps.FirstOrDefault(m => m.uniqueID == r.mapId) ?? Find.AnyPlayerHomeMap;
                if (!r.arrived)
                {
                    if (now < r.arriveTick || map == null) continue;
                    if (!Arrive(r, map)) runs.RemoveAt(i);
                    continue;
                }
                // done: the carrier lost the relics (killed, downed and stripped, or they dropped), or left the map with them
                var c = r.carrier;
                bool holding = c != null && !c.Dead && c.inventory != null && c.inventory.innerContainer.Any(t => t.def == r.def);
                // captured or recruited with them: the colony has them now
                if (holding && (c.IsPrisonerOfColony || c.IsSlaveOfColony || c.Faction == Faction.OfPlayer)) { runs.RemoveAt(i); continue; }
                // carried (downed) or in a caravan: not gone with them
                if (holding && !c.Spawned && (c.MapHeld != null || c.GetCaravan() != null)) continue;
                if (holding && !c.Spawned)
                {
                    // gone off the map: the relics go back out into the world (destroyed here, counted as unfound again)
                    foreach (var t in c.inventory.innerContainer.Where(t => t.def == r.def).ToList()) t.Destroy();
                    Messages.Message($"The convoy from {r.faction?.Name ?? "another faction"} has left with the "
                                     + (IsFinger(r.def) ? "fingers" : "paintings") + ". They will turn up again.", MessageTypeDefOf.NeutralEvent, false);
                    runs.RemoveAt(i);
                }
                else if (!holding) runs.RemoveAt(i);
            }
        }

        private static bool Arrive(Run r, Map map)
        {
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Neutral)) return false;
            if (!RCellFinder.TryFindTravelDestFrom(entry, map, out var exit)) return false;
            var faction = r.faction != null && !r.faction.defeated ? r.faction : PickFaction(true);
            if (faction == null) return false;
            var kind = !faction.HostileTo(Faction.OfPlayer) && faction.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Peaceful)
                ? PawnGroupKindDefOf.Peaceful : PawnGroupKindDefOf.Combat;
            var parms = new PawnGroupMakerParms
            {
                groupKind = kind, faction = faction, tile = map.Tile,
                points = Mathf.Max(200f, StorytellerUtility.DefaultThreatPointsNow(map) * 0.6f),
            };
            var pawns = PawnGroupMakerUtility.GeneratePawns(parms).ToList();
            if (pawns.Count == 0) return false;
            foreach (var p in pawns)
                GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 5), map);
            LordMaker.MakeNewLord(faction, new LordJob_TravelAndExit(exit), map, pawns);
            var carrier = pawns.Where(p => p.RaceProps.Humanlike && p.inventory != null).RandomElementWithFallback() ?? pawns[0];
            for (int k = 0; k < r.count; k++) carrier.inventory?.innerContainer.TryAdd(ThingMaker.MakeThing(r.def));
            r.carrier = carrier; r.pawns = pawns; r.arrived = true; r.faction = faction;
            Find.LetterStack.ReceiveLetter(IsFinger(r.def) ? "Finger convoy" : "Painting convoy",
                $"The group from {faction.Name} is crossing your land. {carrier.LabelShortCap} is carrying the "
                + (IsFinger(r.def) ? "fingers" : "paintings") + ".", LetterDefOf.NeutralEvent, new LookTargets(carrier));
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref runs, "relicConvoys", LookMode.Deep);
            if (runs == null) runs = new List<Run>();
        }
    }
}
